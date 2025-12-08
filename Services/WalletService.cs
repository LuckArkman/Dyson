using Data;
using Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Services;

public class WalletService
{
    private readonly IMongoCollection<TransactionDocument> _transactions;
    private readonly IMongoCollection<WalletDocument> _wallets;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IConfiguration configuration, ILogger<WalletService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // --- LOGS DE DEPURAÇÃO ---
        var connectionString = _configuration["MongoDbSettings:ConnectionString"];
        var databaseName = _configuration["MongoDbSettings:DataBaseName"];
    
        _logger.LogInformation("--- WalletService CONSTRUCTOR ---");
        _logger.LogInformation("ConnectionString: {ConnStr}", connectionString);
        _logger.LogInformation("DatabaseName: {DbName}", databaseName);

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
        {
            _logger.LogCritical("CONFIGURAÇÃO DO MONGODB ESTÁ FALTANDO! Verifique o appsettings.json.");
            throw new ArgumentNullException("A configuração do MongoDB não foi carregada.");
        }
        // --- FIM DOS LOGS DE DEPURAÇÃO ---

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);

        _transactions = database.GetCollection<TransactionDocument>("Transactions");
        _wallets = database.GetCollection<WalletDocument>("Wallets");
    
        _logger.LogInformation("WalletService conectado ao MongoDB com sucesso.");
    }
    
    // Método para criar e armazenar uma nova carteira
    public async Task CreateWalletAsync(string address)
    {
        var wallet = new WalletDocument
        {
            Address = address,
            CreatedAt = DateTime.UtcNow
        };
        await _wallets.InsertOneAsync(wallet);
        _logger.LogInformation("Nova carteira criada e armazenada no DB para o endereço: {Address}", address);
    }
    
    public async Task CreateTransactionAsync(string fromAddress, string toAddress, decimal amount, string? notes = null)
    {
        if (fromAddress != RewardContractService.SystemMintAddress)
        {
            var balance = await GetBalanceAsync(fromAddress);
            if (balance < amount)
            {
                throw new InvalidOperationException("Saldo insuficiente.");
            }
        }

        var transaction = new TransactionDocument
        {
            timestamp = DateTime.UtcNow,
            fromAddress = fromAddress,
            toAddress = toAddress,
            amount = amount,
            notes = notes,
            hash = CryptoUtils.CreateTransactionHash(Guid.NewGuid(), DateTime.UtcNow, fromAddress, toAddress, amount)
        };

        await _transactions.InsertOneAsync(transaction);
        _logger.LogInformation("Transação de {Amount} de {From} para {To} registrada no MongoDB.", amount, fromAddress, toAddress);
    }
    
    private string NormalizeWalletAddress(string walletAddress)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
            return string.Empty;

        // Corrige espaços que vieram de '+'
        string corrected = walletAddress.Replace(" ", "+");

        // Decodifica caracteres URL (%2F -> /, %2B -> +)
        string decoded = System.Web.HttpUtility.UrlDecode(corrected);

        return decoded;
    }
    
    public async Task<decimal> GetBalanceAsync(string walletAddress)
    {
        try
        {
            var receivedTask = _transactions.AsQueryable()
                .Where(t => t.toAddress == walletAddress)
                .SumAsync(t => t.amount);

            var sentTask = _transactions.AsQueryable()
                .Where(t => t.fromAddress == walletAddress)
                .SumAsync(t => t.amount);
                
            await Task.WhenAll(receivedTask, sentTask);

            var balance = receivedTask.Result - sentTask.Result;
            
            _logger.LogDebug("Saldo calculado para carteira (hash: {Hash}): Recebido={Received}, Enviado={Sent}, Saldo={Balance}", 
                walletAddress.GetHashCode(), receivedTask.Result, sentTask.Result, balance);

            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular saldo da carteira (hash: {Hash})", walletAddress.GetHashCode());
            throw;
        }
    }

    public async Task<IEnumerable<TransactionDocument>> GetHistoryAsync(string walletAddress)
    {
        try
        {
            var filter = Builders<TransactionDocument>.Filter.Or(
                Builders<TransactionDocument>.Filter.Eq(t => t.fromAddress, walletAddress),
                Builders<TransactionDocument>.Filter.Eq(t => t.toAddress, walletAddress)
            );

            var transactions = await _transactions.Find(filter)
                .SortByDescending(t => t.timestamp)
                .ToListAsync();

            _logger.LogDebug("Histórico obtido para carteira (hash: {Hash}): {Count} transações", 
                walletAddress.GetHashCode(), transactions.Count);

            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter histórico da carteira (hash: {Hash})", walletAddress.GetHashCode());
            throw;
        }
    }
    /// <summary>
    /// Obtém o número total de transações de uma carteira
    /// </summary>
    public async Task<int> GetTransactionCountAsync(string walletAddress)
    {
        try
        {
            var filter = Builders<TransactionDocument>.Filter.Or(
                Builders<TransactionDocument>.Filter.Eq(t => t.fromAddress, walletAddress),
                Builders<TransactionDocument>.Filter.Eq(t => t.toAddress, walletAddress)
            );

            var count = (int)await _transactions.CountDocumentsAsync(filter);

            _logger.LogDebug("Contagem de transações para carteira (hash: {Hash}): {Count}", 
                walletAddress.GetHashCode(), count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao contar transações da carteira (hash: {Hash})", walletAddress.GetHashCode());
            throw;
        }
    }

    /// <summary>
    /// Verifica se uma carteira existe
    /// </summary>
    public async Task<bool> WalletExistsAsync(string walletAddress)
    {
        try
        {
            var wallet = await _wallets.Find(w => w.Address == walletAddress).FirstOrDefaultAsync();
            var exists = wallet != null;

            _logger.LogDebug("Verificação de existência da carteira (hash: {Hash}): {Exists}", 
                walletAddress.GetHashCode(), exists);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência da carteira (hash: {Hash})", walletAddress.GetHashCode());
            throw;
        }
    }

    /// <summary>
    /// Obtém informações básicas da carteira
    /// </summary>
    public async Task<WalletDocument?> GetWalletInfoAsync(string walletAddress)
    {
        try
        {
            var wallet = await _wallets.Find(w => w.Address == walletAddress).FirstOrDefaultAsync();

            _logger.LogDebug("Informações da carteira obtidas (hash: {Hash}): {Found}", 
                walletAddress.GetHashCode(), wallet != null);

            return wallet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações da carteira (hash: {Hash})", walletAddress.GetHashCode());
            throw;
        }
    }
    
    public async Task<IEnumerable<TransactionDocument>> GetFullLedgerAsync()
    {
        try
        {
            // Retorna todas as transações, ordenadas pela mais recente primeiro
            var transactions = await _transactions.Find(_ => true)
                .SortByDescending(t => t.timestamp)
                .ToListAsync();

            _logger.LogDebug("Ledger completo obtido: {Count} transações", transactions.Count);

            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ledger completo");
            throw;
        }
    }

    public async Task<WalletDocument> GetUserWalletAsync(string Id)
    {
        var wallet = await _wallets.Find(w => w.userId == Id).FirstOrDefaultAsync();
        return wallet;

    }

    /// <summary>
    /// Debita um valor da carteira do usuário (cria transação de saída)
    /// </summary>
    public async Task DebitBalanceAsync(string walletAddress, decimal amount, string reason)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Valor deve ser maior que zero.", nameof(amount));
        }

        var balance = await GetBalanceAsync(walletAddress);
        if (balance < amount)
        {
            throw new InvalidOperationException($"Saldo insuficiente. Disponível: {balance}, Necessário: {amount}");
        }

        // Criar transação de débito (saída)
        // Endereço de destino é "SYSTEM" para indicar que é um débito de serviço
        await CreateTransactionAsync(
            fromAddress: walletAddress,
            toAddress: "SYSTEM_DEBIT",
            amount: amount,
            notes: reason
        );

        _logger.LogInformation(
            "Débito de {Amount} realizado na carteira (hash: {Hash}). Razão: {Reason}", 
            amount, walletAddress.GetHashCode(), reason);
    }
    
    /// <summary>
    /// Credita um valor na carteira do usuário (cria transação de entrada)
    /// </summary>
    public async Task CreditBalanceAsync(string walletAddress, decimal amount, string reason)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Valor deve ser maior que zero.", nameof(amount));
        }

        // Criar transação de crédito (entrada)
        // Endereço de origem é "SYSTEM" para indicar que é um crédito de serviço
        await CreateTransactionAsync(
            fromAddress: "SYSTEM_CREDIT",
            toAddress: walletAddress,
            amount: amount,
            notes: reason
        );

        _logger.LogInformation(
            "Crédito de {Amount} realizado na carteira (hash: {Hash}). Razão: {Reason}", 
            amount, walletAddress.GetHashCode(), reason);
    }
    
    /// <summary>
    /// Gera um endereço de carteira único baseado no userId
    /// </summary>
    private string GenerateWalletAddress(string userId)
    {
        // Gera um endereço estilo Ethereum baseado no hash do userId
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userId + DateTime.UtcNow.Ticks)
        );
        
        // Pega os primeiros 20 bytes (40 caracteres hex) = endereço Ethereum
        var addressBytes = hash.Take(20).ToArray();
        var address = "0x" + BitConverter.ToString(addressBytes).Replace("-", "").ToLower();
        
        return address;
    }
    
    /// <summary>
    /// Transfere saldo entre duas carteiras
    /// </summary>
    public async Task<bool> TransferAsync(string fromAddress, string toAddress, decimal amount, string reason = null)
    {
        try
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Valor deve ser maior que zero.", nameof(amount));
            }

            var balance = await GetBalanceAsync(fromAddress);
            if (balance < amount)
            {
                _logger.LogWarning("Tentativa de transferência com saldo insuficiente. From: {From}, Amount: {Amount}, Balance: {Balance}", 
                    fromAddress.GetHashCode(), amount, balance);
                return false;
            }

            await CreateTransactionAsync(fromAddress, toAddress, amount, reason ?? "Transferência");

            _logger.LogInformation("Transferência de {Amount} de {From} para {To} realizada com sucesso", 
                amount, fromAddress.GetHashCode(), toAddress.GetHashCode());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao realizar transferência de {From} para {To}", 
                fromAddress.GetHashCode(), toAddress.GetHashCode());
            return false;
        }
    }
}