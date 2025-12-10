using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Serviço de background que escuta eventos de inferência/mineração
/// e distribui recompensas em tokens ARC-20 via blockchain
/// VERSÃO ATUALIZADA - Compatível com TransactionDocument existente
/// </summary>
public class RewardListner : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly RewardContractService _rewardContractService;
    private readonly ArcTokenService _arcTokenService;
    private readonly IRepositorio<User> _repositorioUser;
    private readonly IRepositorio<WalletDocument> _repositorioWallet;
    private readonly IRepositorio<TransactionDocument> _repositorioTransaction;
    private readonly ChatService _chatService;
    private readonly ILogger<RewardListner> _logger;
    
    public RewardListner(
        IConfiguration configuration,
        RewardContractService rewardContractService,
        ArcTokenService arcTokenService,
        IRepositorio<User> repositorioUser,
        IRepositorio<WalletDocument> repositorioWallet,
        IRepositorio<TransactionDocument> repositorioTransaction,
        ChatService chatService,
        ILogger<RewardListner> logger)
    {
        _configuration = configuration;
        _rewardContractService = rewardContractService;
        _arcTokenService = arcTokenService;
        _repositorioUser = repositorioUser;
        _repositorioWallet = repositorioWallet;
        _repositorioTransaction = repositorioTransaction;
        _chatService = chatService;
        _logger = logger;
        
        // Inscrever no evento de bloco adicionado
        _chatService.BlockAdded += OnBlockAdded;
        
        // Inicializar coleções do MongoDB
        InitializeCollections();
        
        _logger.LogInformation("RewardListner inicializado e conectado ao ChatService");
    }

    /// <summary>
    /// Inicializa as coleções do MongoDB
    /// </summary>
    private void InitializeCollections()
    {
        var connectionString = _configuration["MongoDbSettings:ConnectionString"];
        var databaseName = _configuration["MongoDbSettings:DatabaseName"];

        _repositorioUser.InitializeCollection(connectionString, databaseName, "Users");
        _repositorioWallet.InitializeCollection(connectionString, databaseName, "Wallets");
        _repositorioTransaction.InitializeCollection(connectionString, databaseName, "Transactions");
        
        _logger.LogInformation("Coleções MongoDB inicializadas: Users, Wallets, Transactions");
    }

    /// <summary>
    /// Evento disparado quando um bloco é adicionado (inferência realizada)
    /// </summary>
    private async void OnBlockAdded(object? sender, NodeClient node)
    {
        var userId = node._session?.UserId.ToString();
        
        try
        {
            _logger.LogInformation(
                "🔔 Evento de inferência detectado. NodeClient: {NodeId}, UserId: {UserId}", 
                node.id, 
                userId
            );

            // 1. VALIDAR USUÁRIO
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "⚠️ NodeClient {NodeId} não possui UserId válido. Recompensa ignorada.",
                    node.id
                );
                return;
            }

            var user = await _repositorioUser.GetUserByIdAsync(userId, CancellationToken.None);
            if (user == null)
            {
                _logger.LogWarning(
                    "⚠️ Usuário {UserId} não encontrado no banco. NodeClient: {NodeId}",
                    userId,
                    node.id
                );
                return;
            }

            _logger.LogInformation("✅ Usuário encontrado: {UserName} ({UserId})", user.UserName, userId);

            // 2. RECUPERAR OU CRIAR CARTEIRA
            var wallet = await _repositorioWallet.GetUserWalletAsync(userId, CancellationToken.None);
            
            if (wallet == null)
            {
                _logger.LogInformation("📝 Carteira não encontrada. Criando nova carteira para usuário {UserId}", userId);
                wallet = await CreateNewWalletAsync(userId);
            }

            // Validar endereço da carteira
            if (!_arcTokenService.IsValidAddress(wallet.Address))
            {
                _logger.LogError(
                    "❌ Endereço de carteira inválido para usuário {UserId}: {Address}",
                    userId,
                    wallet.Address
                );
                return;
            }

            _logger.LogInformation("✅ Carteira recuperada: {Address}", wallet.Address);

            // 3. CALCULAR RECOMPENSA
            var random = new Random();
            var rewardAmount = random.Next(2, 8); // Entre 2 e 7 tokens
            
            _logger.LogInformation(
                "💰 Recompensa calculada: {Amount} tokens para {Address}",
                rewardAmount,
                wallet.Address
            );

            // 4. TRANSFERIR TOKENS VIA BLOCKCHAIN (ARC TESTNET)
            _logger.LogInformation("🔄 Iniciando transferência blockchain para {Address}...", wallet.Address);
            
            var txHash = await _arcTokenService.TransferTokensAsync(
                wallet.Address,
                rewardAmount,
                $"Inference Reward - NodeClient: {node.id}"
            );

            _logger.LogInformation(
                "✅ Transferência blockchain bem-sucedida! TxHash: {TxHash}",
                txHash
            );

            // 5. SALVAR TRANSAÇÃO NO MONGODB
            var transaction = await SaveTransactionToDatabase(
                userId,
                wallet.Address,
                rewardAmount,
                txHash,
                node.id.ToString()
            );

            _logger.LogInformation(
                "✅ Transação salva no MongoDB. TransactionId: {TransactionId}",
                transaction.Id
            );

            // 6. ATUALIZAR ÚLTIMA AUTENTICAÇÃO DA CARTEIRA (OPCIONAL)
            await UpdateUserLastWalletAuth(user);

            // Log final de sucesso
            _logger.LogInformation(
                "🎉 RECOMPENSA PROCESSADA COM SUCESSO!\n" +
                "   └─ Usuário: {UserName} ({UserId})\n" +
                "   └─ Carteira: {Address}\n" +
                "   └─ Valor: {Amount} tokens\n" +
                "   └─ TxHash: {TxHash}\n" +
                "   └─ Explorer: https://testnet.arcscan.app/tx/{TxHash}",
                user.UserName,
                userId,
                wallet.Address,
                rewardAmount,
                txHash,
                txHash
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ ERRO ao processar recompensa para NodeClient {NodeId}, UserId: {UserId}. Detalhes: {Message}",
                node.id,
                userId,
                ex.Message
            );
        }
    }

    /// <summary>
    /// Cria uma nova carteira para o usuário
    /// </summary>
    private async Task<WalletDocument> CreateNewWalletAsync(string userId)
    {
        try
        {
            // Gerar par de chaves Ethereum
            var account = new Nethereum.Web3.Accounts.Account(
                Nethereum.Signer.EthECKey.GenerateKey().GetPrivateKeyAsBytes()
            );

            var newWallet = new WalletDocument
            {
                userId = userId,
                Address = account.Address,
                CreatedAt = DateTime.UtcNow
            };

            await _repositorioWallet.InsertOneAsync(newWallet);

            _logger.LogInformation(
                "✅ Nova carteira criada para usuário {UserId}: {Address}",
                userId,
                account.Address
            );

            return newWallet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao criar carteira para usuário {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Salva a transação no banco de dados MongoDB
    /// ATUALIZADO: Usa a estrutura existente do TransactionDocument
    /// </summary>
    private async Task<TransactionDocument> SaveTransactionToDatabase(
        string userId,
        string walletAddress,
        decimal amount,
        string txHash,
        string nodeClientId)
    {
        try
        {
            var transaction = new TransactionDocument
            {
                // Campos obrigatórios (estrutura existente)
                fromAddress = "SYSTEM_MINT",
                toAddress = walletAddress,
                amount = amount,
                hash = txHash,
                timestamp = DateTime.UtcNow,
                notes = $"Recompensa por inferência - NodeClient: {nodeClientId}",
                
                // Campos opcionais (novos, para enriquecer os dados)
                type = "Inference Reward",
                status = "Confirmed",
                blockchainNetwork = "ARC Testnet",
                contractAddress = "0xDD7Fb93DC67D5715BbF55bAc41d7c9202d8951A7"
            };

            await _repositorioTransaction.InsertOneAsync(transaction);

            _logger.LogInformation(
                "💾 Transação salva no MongoDB:\n" +
                "   └─ TransactionId: {TransactionId}\n" +
                "   └─ From: {From}\n" +
                "   └─ To: {To}\n" +
                "   └─ Amount: {Amount}\n" +
                "   └─ TxHash: {TxHash}\n" +
                "   └─ Timestamp: {Timestamp}",
                transaction.Id,
                transaction.fromAddress,
                transaction.toAddress,
                transaction.amount,
                transaction.hash,
                transaction.timestamp
            );

            return transaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ Erro ao salvar transação no MongoDB. TxHash: {TxHash}",
                txHash
            );
            throw;
        }
    }

    /// <summary>
    /// Atualiza a última autenticação da carteira do usuário
    /// </summary>
    private async Task UpdateUserLastWalletAuth(User user)
    {
        try
        {
            user.LastWalletAuth = DateTime.UtcNow;
            await _repositorioUser.UpdateUserAsync(user);
            
            _logger.LogDebug(
                "📝 LastWalletAuth atualizado para usuário {UserId}",
                user.Id
            );
        }
        catch (Exception ex)
        {
            // Não propagar exceção - é uma operação secundária
            _logger.LogWarning(
                ex,
                "⚠️ Não foi possível atualizar LastWalletAuth para usuário {UserId}",
                user.Id
            );
        }
    }

    /// <summary>
    /// Inicia o serviço de background
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🚀 RewardListner iniciado e monitorando eventos de inferência.\n" +
            "   └─ Conectado ao ARC Testnet: https://testnet-rpc.arcscan.app\n" +
            "   └─ Contrato ARC-20: 0xDD7Fb93DC67D5715BbF55bAc41d7c9202d8951A7\n" +
            "   └─ MongoDB Database: {DatabaseName}\n" +
            "   └─ Coleções: Users, Wallets, Transactions",
            _configuration["MongoDbSettings:DatabaseName"]
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Para o serviço e desinscreve dos eventos
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Parando RewardListner...");
        
        // Desinscrever do evento
        _chatService.BlockAdded -= OnBlockAdded;
        
        _logger.LogInformation("✅ RewardListner parado com sucesso");
        
        return base.StopAsync(cancellationToken);
    }
}