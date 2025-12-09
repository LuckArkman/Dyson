using System.Security.Cryptography;
using System.Text;
using Application.Models;
using Dtos;
using Interfaces;
using MongoDB.Driver;
using Nethereum.Signer;
using Nethereum.Util;

namespace Services;

/// <summary>
/// Serviço para autenticação Web3 via MetaMask
/// VERSÃO OTIMIZADA - Acesso direto ao MongoDB
/// </summary>
public class Web3AuthService : IWeb3AuthService
{
    private readonly IRepositorio<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Web3AuthService> _logger;
    private readonly EthereumMessageSigner _signer;
    private readonly IMongoCollection<User> _userCollection;

    public Web3AuthService(
        IConfiguration configuration, 
        IRepositorio<User> userRepository,
        ILogger<Web3AuthService> logger)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _logger = logger;
        _signer = new EthereumMessageSigner();
        
        // Inicializa repositório
        _userRepository.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            _configuration["MongoDbSettings:DbUserCollection"]);
        
        // Acesso direto ao MongoDB para consultas personalizadas
        var client = new MongoClient(_configuration["MongoDbSettings:ConnectionString"]);
        var database = client.GetDatabase(_configuration["MongoDbSettings:DataBaseName"]);
        _userCollection = database.GetCollection<User>(_configuration["MongoDbSettings:DbUserCollection"]);
        
        // Criar índice no WalletAddress se não existir
        CreateWalletAddressIndex();
    }

    /// <summary>
    /// Cria índice no campo WalletAddress
    /// </summary>
    private void CreateWalletAddressIndex()
    {
        try
        {
            var indexKeys = Builders<User>.IndexKeys.Ascending(u => u.WalletAddress);
            var indexOptions = new CreateIndexOptions { Sparse = true };
            var indexModel = new CreateIndexModel<User>(indexKeys, indexOptions);
            
            _userCollection.Indexes.CreateOne(indexModel);
            _logger.LogInformation("WalletAddress index created or already exists");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create WalletAddress index");
        }
    }

    public string GenerateNonce(string walletAddress)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var randomString = Convert.ToBase64String(randomBytes);
        
        return $"Dyson.AI Login\nTimestamp: {timestamp}\nNonce: {randomString}\nWallet: {walletAddress}";
    }

    public bool VerifySignature(string message, string signature, string walletAddress)
    {
        try
        {
            var recoveredAddress = _signer.EncodeUTF8AndEcRecover(message, signature);
            
            _logger.LogInformation(
                "Signature verification - Expected: {Expected}, Recovered: {Recovered}", 
                walletAddress.ToLower(), 
                recoveredAddress.ToLower());

            return string.Equals(
                recoveredAddress.Trim(), 
                walletAddress.Trim(), 
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature for wallet {Wallet}", walletAddress);
            return false;
        }
    }

    public async Task<User> AuthenticateWithWallet(string walletAddress, string signature, string message)
    {
        try
        {
            if (!VerifySignature(message, signature, walletAddress))
            {
                _logger.LogWarning("Invalid signature for wallet {Wallet}", walletAddress);
                throw new UnauthorizedAccessException("Assinatura inválida");
            }

            walletAddress = walletAddress.ToLower();

            var existingUser = await GetUserByWalletAddress(walletAddress);

            if (existingUser != null)
            {
                existingUser.BackupDate = DateTime.UtcNow;
                existingUser.LastWalletAuth = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(existingUser);
                
                _logger.LogInformation("User authenticated via wallet {Wallet}", walletAddress);
                return existingUser;
            }

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                WalletAddress = walletAddress,
                UserName = $"User_{walletAddress.Substring(0, 8)}",
                Email = $"{walletAddress}@wallet.dyson.ai",
                PhoneNumber = "",
                PasswordHash = "",
                PersonalName = $"Wallet User",
                Nickname = $"User_{walletAddress.Substring(0, 8)}",
                BackupDate = DateTime.UtcNow,
                AuthMethod = "MetaMask",
                LastWalletAuth = DateTime.UtcNow
            };

            var createdUser = await _userRepository.InsertOneAsync(newUser) as User;
            
            _logger.LogInformation("New user created via wallet {Wallet}", walletAddress);
            
            return createdUser;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with wallet {Wallet}", walletAddress);
            throw new Exception("Erro ao autenticar com carteira", ex);
        }
    }

    public async Task<bool> LinkWalletToUser(string userId, string walletAddress, string signature, string message)
    {
        try
        {
            if (!VerifySignature(message, signature, walletAddress))
            {
                _logger.LogWarning("Invalid signature when linking wallet {Wallet} to user {UserId}", 
                    walletAddress, userId);
                return false;
            }

            walletAddress = walletAddress.ToLower();

            var existingWalletUser = await GetUserByWalletAddress(walletAddress);
            if (existingWalletUser != null && existingWalletUser.Id != userId)
            {
                throw new InvalidOperationException("Esta carteira já está vinculada a outra conta");
            }

            var user = await _userRepository.GetByIdAsync(userId, CancellationToken.None) as User;
            if (user == null)
            {
                throw new Exception("Usuário não encontrado");
            }

            user.WalletAddress = walletAddress;
            user.BackupDate = DateTime.UtcNow;
            user.LastWalletAuth = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(user.AuthMethod))
            {
                user.AuthMethod = "MetaMask";
            }
            else if (!user.AuthMethod.Contains("MetaMask"))
            {
                user.AuthMethod = "Both";
            }
            
            await _userRepository.UpdateUserAsync(user);
            
            _logger.LogInformation("Wallet {Wallet} linked to user {UserId}", walletAddress, userId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking wallet {Wallet} to user {UserId}", walletAddress, userId);
            throw;
        }
    }

    /// <summary>
    /// Busca usuário pelo endereço da carteira usando MongoDB diretamente
    /// </summary>
    public async Task<User?> GetUserByWalletAddress(string walletAddress)
    {
        try
        {
            walletAddress = walletAddress?.ToLower();
            
            if (string.IsNullOrEmpty(walletAddress))
                return null;

            var filter = Builders<User>.Filter.Eq(u => u.WalletAddress, walletAddress);
            
            var user = await _userCollection
                .Find(filter)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogInformation("No user found with wallet {Wallet}", walletAddress);
            }
            
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by wallet {Wallet}", walletAddress);
            return null;
        }
    }

    public bool IsValidEthereumAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        if (!address.StartsWith("0x") || address.Length != 42)
            return false;

        var hexChars = address.Substring(2);
        return hexChars.All(c => 
            (c >= '0' && c <= '9') || 
            (c >= 'a' && c <= 'f') || 
            (c >= 'A' && c <= 'F'));
    }

    public string GenerateSignatureMessage(string walletAddress, string action = "login")
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var nonce = Guid.NewGuid().ToString("N").Substring(0, 16);

        return $@"Bem-vindo à Dyson.AI!

Ação: {action}
Carteira: {walletAddress}
Timestamp: {timestamp}
Nonce: {nonce}

Esta assinatura não realizará nenhuma transação na blockchain.";
    }
}