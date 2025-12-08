using System.Text;
using System.Text.Json;
using Dtos;
using Microsoft.Extensions.Options;

namespace Services;
public class ThirdwebApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThirdwebApiService> _logger;
    private readonly ThirdwebSettings _settings;
    private readonly string _baseUrl;
    private readonly string _secretKey;

    public ThirdwebApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ThirdwebApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Carregar configurações
        _baseUrl = configuration["ThirdwebSettings:ApiBaseUrl"] ?? "https://api.thirdweb.com";
        _secretKey = configuration["ThirdwebSettings:SecretKey"];
        
        if (string.IsNullOrEmpty(_secretKey))
        {
            throw new InvalidOperationException(
                "ThirdwebSettings:SecretKey não configurado. Obtenha em https://thirdweb.com/dashboard/settings/api-keys");
        }

        // Configurar headers padrão
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-secret-key", _secretKey);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        
        _logger.LogInformation("ThirdwebApiService inicializado: {BaseUrl}", _baseUrl);
    }

    // === TOKENS ===

    /// <summary>
    /// Cria um token ERC-20 com pool de liquidez opcional
    /// POST /v1/contract/{chain_id}/deploy/token
    /// </summary>
    public async Task<TokenDeployResponse> DeployERC20TokenAsync(TokenDeployRequest request)
    {
        _logger.LogInformation("Deploying ERC-20 token: {Name} on chain {Chain}", 
            request.Name, request.ChainId);

        try
        {
            var endpoint = $"/v1/contract/{request.ChainId}/deploy/token";
            
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to deploy token: {Error}", error);
                throw new Exception($"Deploy failed: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<TokenDeployResponse>();
            
            _logger.LogInformation("Token deployed successfully: {Address}", result.ContractAddress);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying ERC-20 token");
            throw;
        }
    }

    /// <summary>
    /// Obtém informações de um token
    /// GET /v1/contract/{chain_id}/{contract_address}/erc20/get
    /// </summary>
    public async Task<TokenInfo> GetTokenInfoAsync(int chainId, string contractAddress)
    {
        try
        {
            var endpoint = $"/v1/contract/{chainId}/{contractAddress}/erc20/get";
            var response = await _httpClient.GetFromJsonAsync<TokenInfo>(endpoint);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting token info for {Address}", contractAddress);
            throw;
        }
    }

    /// <summary>
    /// Transfere tokens
    /// POST /v1/contract/{chain_id}/{contract_address}/erc20/transfer
    /// </summary>
    public async Task<TransactionResponse> TransferTokenAsync(
        int chainId, 
        string contractAddress, 
        TransferRequest request)
    {
        try
        {
            var endpoint = $"/v1/contract/{chainId}/{contractAddress}/erc20/transfer";
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Transfer failed: {error}");
            }
            
            return await response.Content.ReadFromJsonAsync<TransactionResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring tokens");
            throw;
        }
    }

    // === WALLETS ===

    /// <summary>
    /// Cria uma in-app wallet para o usuário
    /// POST /v1/wallet/in-app
    /// </summary>
    public async Task<WalletCreateResponse> CreateInAppWalletAsync(string userId, string email = null)
    {
        _logger.LogInformation("Creating in-app wallet for user: {UserId}", userId);

        try
        {
            var request = new
            {
                userId,
                email,
                type = "in-app"
            };

            var response = await _httpClient.PostAsJsonAsync("/v1/wallet/in-app", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Wallet creation failed: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<WalletCreateResponse>();
            
            _logger.LogInformation("In-app wallet created: {Address}", result.WalletAddress);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating in-app wallet");
            throw;
        }
    }

    /// <summary>
    /// Obtém saldo de uma wallet
    /// GET /v1/wallet/{chain_id}/{address}/balance
    /// </summary>
    public async Task<WalletBalance> GetWalletBalanceAsync(int chainId, string address)
    {
        try
        {
            var endpoint = $"/v1/wallet/{chainId}/{address}/balance";
            var response = await _httpClient.GetFromJsonAsync<WalletBalance>(endpoint);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet balance for {Address}", address);
            throw;
        }
    }

    /// <summary>
    /// Obtém histórico de transações de uma wallet
    /// GET /v1/wallet/{chain_id}/{address}/transactions
    /// </summary>
    public async Task<List<TransactionHistory>> GetWalletTransactionsAsync(
        int chainId, 
        string address, 
        int limit = 20)
    {
        try
        {
            var endpoint = $"/v1/wallet/{chainId}/{address}/transactions?limit={limit}";
            var response = await _httpClient.GetFromJsonAsync<TransactionHistoryResponse>(endpoint);
            return response?.Transactions ?? new List<TransactionHistory>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet transactions");
            throw;
        }
    }

    // === BRIDGE ===

    /// <summary>
    /// Obtém quote para bridge/swap
    /// GET /v1/bridge/quote
    /// </summary>
    public async Task<BridgeQuote> GetBridgeQuoteAsync(BridgeQuoteRequest request)
    {
        try
        {
            var queryString = $"fromChain={request.FromChain}&toChain={request.ToChain}" +
                            $"&fromToken={request.FromToken}&toToken={request.ToToken}" +
                            $"&amount={request.Amount}";
            
            var endpoint = $"/v1/bridge/quote?{queryString}";
            var response = await _httpClient.GetFromJsonAsync<BridgeQuote>(endpoint);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bridge quote");
            throw;
        }
    }

    /// <summary>
    /// Executa bridge/swap
    /// POST /v1/bridge/execute
    /// </summary>
    public async Task<TransactionResponse> ExecuteBridgeAsync(BridgeExecuteRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/v1/bridge/execute", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Bridge execution failed: {error}");
            }
            
            return await response.Content.ReadFromJsonAsync<TransactionResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing bridge");
            throw;
        }
    }

    // === AI (NEBULA) ===

    /// <summary>
    /// Chat com Nebula AI
    /// POST https://nebula-api.thirdweb.com/chat
    /// </summary>
    public async Task<NebulaResponse> ChatWithNebulaAsync(string message, string sessionId = null)
    {
        _logger.LogInformation("Nebula chat: {Message}", message);

        try
        {
            var request = new
            {
                message,
                session_id = sessionId,
                stream = false
            };

            var nebulaClient = new HttpClient
            {
                BaseAddress = new Uri("https://nebula-api.thirdweb.com")
            };
            nebulaClient.DefaultRequestHeaders.Add("x-secret-key", _secretKey);

            var response = await nebulaClient.PostAsJsonAsync("/chat", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Nebula chat failed: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<NebulaResponse>();
            
            _logger.LogInformation("Nebula response received");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error chatting with Nebula");
            throw;
        }
    }

    /// <summary>
    /// Executa ação via Nebula AI
    /// POST https://nebula-api.thirdweb.com/execute
    /// </summary>
    public async Task<NebulaExecuteResponse> ExecuteWithNebulaAsync(
        string message, 
        string walletAddress,
        string sessionId = null)
    {
        try
        {
            var request = new
            {
                message,
                session_id = sessionId,
                execute_config = new
                {
                    mode = "client",
                    signer_wallet_address = walletAddress
                }
            };

            var nebulaClient = new HttpClient
            {
                BaseAddress = new Uri("https://nebula-api.thirdweb.com")
            };
            nebulaClient.DefaultRequestHeaders.Add("x-secret-key", _secretKey);

            var response = await nebulaClient.PostAsJsonAsync("/execute", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Nebula execute failed: {error}");
            }

            return await response.Content.ReadFromJsonAsync<NebulaExecuteResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing with Nebula");
            throw;
        }
    }

    // === ANALYTICS ===

    /// <summary>
    /// Obtém analytics de um contrato
    /// GET /v1/contract/{chain_id}/{contract_address}/analytics
    /// </summary>
    public async Task<ContractAnalytics> GetContractAnalyticsAsync(int chainId, string contractAddress)
    {
        try
        {
            var endpoint = $"/v1/contract/{chainId}/{contractAddress}/analytics";
            var response = await _httpClient.GetFromJsonAsync<ContractAnalytics>(endpoint);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contract analytics");
            throw;
        }
    }
}