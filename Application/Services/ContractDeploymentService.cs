using System.Numerics;
using System.Text;
using System.Text.Json;
using Dtos;
using Interfaces;

namespace Services;

/// <summary>
/// Serviço de deployment de contratos usando thirdweb + Arc Testnet
/// Arc Testnet: Chain ID 5042002, USDC como gas nativo
/// RPC: https://5042002.rpc.thirdweb.com ou https://rpc.testnet.arc.network
/// Explorer: https://testnet.arcscan.app
/// Faucet: https://faucet.circle.com
/// </summary>
public class ContractDeploymentService
{
    private readonly HttpClient _httpClient;
    private readonly WalletService _walletService;
    private readonly IRepositorio<ContractDocument> _contractRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContractDeploymentService> _logger;
    private readonly string _clientId;
    private readonly string _secretKey;

    public ContractDeploymentService(
        IHttpClientFactory httpClientFactory,
        WalletService walletService,
        IRepositorio<ContractDocument> contractRepo,
        IConfiguration configuration,
        ILogger<ContractDeploymentService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ThirdwebArc");
        _walletService = walletService;
        _contractRepo = contractRepo;
        _configuration = configuration;
        _logger = logger;

        _clientId = configuration["ThirdwebSettings:ClientId"] 
            ?? throw new InvalidOperationException("ClientId não configurado");
        _secretKey = configuration["ThirdwebSettings:SecretKey"] 
            ?? throw new InvalidOperationException("SecretKey não configurado");

        // Configurar headers do HTTP client
        _httpClient.DefaultRequestHeaders.Add("x-client-id", _clientId);
        _httpClient.DefaultRequestHeaders.Add("x-secret-key", _secretKey);
        _httpClient.BaseAddress = new Uri("https://5042002.rpc.thirdweb.com/");

        _contractRepo.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            configuration["MongoDbSettings:DbContracts"] ?? "Contracts"
        );
    }

    /// <summary>
    /// Deploy de contrato na Arc Testnet
    /// Suporta: ERC-20 Tokens, ERC-721 NFTs, ERC-1155, Custom Contracts
    /// </summary>
    public async Task<(bool success, string result)> DeployContractAsync(
        string userAddress,
        ContractCreationRequestModel model)
    {
        _logger.LogInformation(
            "🚀 [ARC] Deploy iniciado - Usuário: {User}, Contrato: {Name}, Tipo: {Type}",
            userAddress, model.ContractName, model.ContractType);

        try
        {
            var wallet = await _walletService.GetUserWalletAsync(userAddress);
            if (wallet == null)
            {
                return (false, "Wallet não encontrada para o usuário.");
            }

            // Verificar saldo se houver custo
            if (model.DeploymentCost > 0)
            {
                var balance = await _walletService.GetBalanceAsync(wallet.Address);
                if (balance < model.DeploymentCost)
                {
                    _logger.LogWarning(
                        "💰 Saldo insuficiente - Necessário: {Cost}, Disponível: {Balance}",
                        model.DeploymentCost, balance);
                    return (false, $"Saldo insuficiente. Você tem {balance} tokens, mas precisa de {model.DeploymentCost}.");
                }
            }

            // Determinar modo de deployment
            var deploymentMode = _configuration["ThirdwebSettings:Deployment:Mode"] ?? "simulation";
            
            ContractDocument contractDoc;

            if (deploymentMode == "arc-testnet")
            {
                _logger.LogInformation("🌐 [ARC] Deploy real na Arc Testnet");
                contractDoc = await DeployToArcTestnetAsync(wallet, model);
            }
            else if (deploymentMode == "simulation")
            {
                _logger.LogInformation("🧪 [ARC] Modo simulação ativado");
                contractDoc = await DeploySimulatedAsync(wallet, model);
            }
            else
            {
                _logger.LogWarning("⚠️ Modo '{Mode}' não suportado", deploymentMode);
                return (false, $"Modo de deployment '{deploymentMode}' não suportado. Use 'arc-testnet' ou 'simulation'");
            }

            // Debitar custo se houver
            if (model.DeploymentCost > 0)
            {
                await _walletService.DebitBalanceAsync(
                    wallet.Address,
                    model.DeploymentCost,
                    $"Deploy de contrato: {model.ContractName}");
                
                _logger.LogInformation(
                    "💸 Custo de {Cost} debitado da wallet {Address}",
                    model.DeploymentCost, wallet.Address);
            }

            // Adicionar evento ao log
            contractDoc.EventLog.Add(new ContractEvent
            {
                EventType = "deployed",
                Description = $"Contrato deployado via {deploymentMode}",
                Metadata = new Dictionary<string, object>
                {
                    { "deploymentCost", model.DeploymentCost },
                    { "userAddress", userAddress },
                    { "mode", deploymentMode },
                    { "chain", "arc-testnet" },
                    { "chainId", 5042002 }
                }
            });

            // Salvar no MongoDB
            await _contractRepo.SaveContractAsync(contractDoc);

            _logger.LogInformation(
                "✅ [ARC] Contrato salvo - ID: {Id}, Endereço: {Address}",
                contractDoc.Id, contractDoc.ContractAddress);

            return (true, contractDoc.ContractAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [ARC] Erro ao fazer deploy do contrato");
            return (false, $"Falha na implantação: {ex.Message}");
        }
    }

    /// <summary>
    /// Deploy REAL na Arc Testnet usando thirdweb RPC
    /// </summary>
    private async Task<ContractDocument> DeployToArcTestnetAsync(
        WalletDocument wallet,
        ContractCreationRequestModel model)
    {
        _logger.LogInformation("🌐 [ARC] Deploying to Arc Testnet (Chain ID: 5042002)");

        // Selecionar bytecode e constructor args baseado no tipo
        var (bytecode, constructorArgs) = GetContractBytecodeAndArgs(model);

        // Preparar transação de deploy
        var deployRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "eth_sendTransaction",
            @params = new[]
            {
                new
                {
                    from = wallet.Address,
                    data = bytecode + constructorArgs,
                    gas = "0x" + (3000000).ToString("X") // 3M gas limit
                }
            }
        };

        var json = JsonSerializer.Serialize(deployRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("📤 [ARC] Enviando transação de deploy...");

        var response = await _httpClient.PostAsync("", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("❌ [ARC] Deploy falhou: {Response}", responseBody);
            throw new Exception($"Deploy falhou: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var txHash = doc.RootElement.GetProperty("result").GetString();

        _logger.LogInformation("📝 [ARC] Transaction hash: {Hash}", txHash);

        // Aguardar confirmação (5-10 segundos na Arc Testnet)
        await Task.Delay(8000);

        // Obter receipt para pegar o endereço do contrato
        var contractAddress = await GetContractAddressFromTx(txHash);

        _logger.LogInformation("✅ [ARC] Contrato deployado: {Address}", contractAddress);

        var contractDoc = CreateContractDocument(wallet, model, contractAddress, txHash, "arc-testnet");
        contractDoc.Notes = $"Contrato deployado na Arc Testnet em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.\n" +
                           $"Transaction: {txHash}\n" +
                           $"Explorer: https://testnet.arcscan.app/tx/{txHash}";

        return contractDoc;
    }

    /// <summary>
    /// Obtém o endereço do contrato a partir do transaction hash
    /// </summary>
    private async Task<string> GetContractAddressFromTx(string txHash)
    {
        var receiptRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "eth_getTransactionReceipt",
            @params = new[] { txHash }
        };

        var json = JsonSerializer.Serialize(receiptRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var contractAddress = doc.RootElement
            .GetProperty("result")
            .GetProperty("contractAddress")
            .GetString();

        return contractAddress ?? throw new Exception("Não foi possível obter o endereço do contrato");
    }

    /// <summary>
    /// Retorna bytecode e constructor args para o tipo de contrato
    /// </summary>
    private (string bytecode, string constructorArgs) GetContractBytecodeAndArgs(
        ContractCreationRequestModel model)
    {
        var contractType = model.ContractType?.ToLower();

        // Bytecodes simplificados (em produção, usar contracts compilados com solc)
        switch (contractType)
        {
            case "token":
            case "erc20":
                return GetERC20Bytecode(model);
            
            case "nft":
            case "erc721":
                return GetERC721Bytecode(model);
            
            case "erc1155":
                return GetERC1155Bytecode(model);
            
            default:
                _logger.LogWarning("⚠️ Tipo '{Type}' não tem bytecode definido, usando ERC20", contractType);
                return GetERC20Bytecode(model);
        }
    }

    /// <summary>
    /// Bytecode de ERC-20 Token (SimpleToken)
    /// </summary>
    private (string bytecode, string constructorArgs) GetERC20Bytecode(
        ContractCreationRequestModel model)
    {
        // Bytecode compilado de um SimpleToken.sol
        // Este é um exemplo - em produção, compile seu próprio contrato
        var bytecode = "0x608060405234801561001057600080fd5b50..."; // Bytecode completo aqui

        // Constructor args: name, symbol, initialSupply
        var constructorArgs = EncodeConstructorArgs(
            model.ContractName ?? "Token",
            model.Symbol ?? "TKN",
            model.InitialSupply ?? "1000000"
        );

        return (bytecode, constructorArgs);
    }

    /// <summary>
    /// Bytecode de ERC-721 NFT
    /// </summary>
    private (string bytecode, string constructorArgs) GetERC721Bytecode(
        ContractCreationRequestModel model)
    {
        var bytecode = "0x608060405234801561001057600080fd5b50..."; // NFT bytecode
        var constructorArgs = EncodeConstructorArgs(
            model.ContractName ?? "NFT",
            model.Symbol ?? "NFT"
        );
        return (bytecode, constructorArgs);
    }

    /// <summary>
    /// Bytecode de ERC-1155
    /// </summary>
    private (string bytecode, string constructorArgs) GetERC1155Bytecode(
        ContractCreationRequestModel model)
    {
        var bytecode = "0x608060405234801561001057600080fd5b50..."; // ERC1155 bytecode
        var constructorArgs = EncodeConstructorArgs(
            model.ContractName ?? "MultiToken"
        );
        return (bytecode, constructorArgs);
    }

    /// <summary>
    /// Encode constructor arguments (ABI encoding simplificado)
    /// </summary>
    private string EncodeConstructorArgs(params string[] args)
    {
        // Em produção, usar biblioteca de ABI encoding adequada
        // Por enquanto, retornamos vazio (funciona para contratos simples)
        return "";
    }

    /// <summary>
    /// Deploy simulado para testes (sem custo real)
    /// </summary>
    private async Task<ContractDocument> DeploySimulatedAsync(
        WalletDocument wallet,
        ContractCreationRequestModel model)
    {
        _logger.LogInformation("🧪 [ARC] SIMULAÇÃO - Deploy de teste para {Name}", model.ContractName);

        await Task.Delay(1500); // Simular tempo de deploy

        var contractAddress = GenerateAddress(model.ContractName);
        var txHash = GenerateTransactionHash();

        var contractDoc = CreateContractDocument(wallet, model, contractAddress, txHash, "simulated");
        contractDoc.Notes = $"⚠️ CONTRATO SIMULADO - Não existe na blockchain real.\n" +
                           $"Deployado em modo teste em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.\n" +
                           $"Para deploy real na Arc Testnet, configure Mode='arc-testnet' no appsettings.json";

        contractDoc.Metadata["simulated"] = true;
        contractDoc.Metadata["warningMessage"] = "Este é um contrato de teste e não existe na blockchain real";

        _logger.LogInformation("✅ [ARC] Contrato simulado criado: {Address}", contractAddress);

        return contractDoc;
    }

    /// <summary>
    /// Cria documento de contrato com dados comuns
    /// </summary>
    private ContractDocument CreateContractDocument(
        WalletDocument wallet,
        ContractCreationRequestModel model,
        string contractAddress,
        string txHash,
        string deploymentMode)
    {
        return new ContractDocument
        {
            WalletAddress = wallet.Address,
            UserId = wallet.Address,
            ContractAddress = contractAddress,
            ContractName = model.ContractName,
            Symbol = model.Symbol ?? GenerateSymbol(model.ContractName),
            Description = model.Description,
            ContractType = NormalizeContractType(model.ContractType),
            Category = GetCategory(model.ContractType),
            
            // Arc Testnet specifics
            Blockchain = "arc-testnet",
            ChainId = 5042002,
            IsTestnet = true,
            
            Status = "active",
            DeploymentMode = deploymentMode,
            ApiVersion = "v1",
            DeploymentCost = model.DeploymentCost,
            TransactionHash = txHash,
            
            TotalSupply = decimal.TryParse(model.InitialSupply, out var supply) ? supply : 1000000,
            Decimals = 18,
            
            ExplorerUrl = $"https://testnet.arcscan.app/address/{contractAddress}",
            ThirdwebApiUrl = $"https://5042002.rpc.thirdweb.com/",
            
            DeployedAt = DateTime.UtcNow,
            
            Tags = new List<string> 
            { 
                deploymentMode,
                model.ContractType?.ToLower() ?? "unknown",
                "arc-testnet",
                "circle",
                "usdc-gas"
            },
            
            // Arc features
            Metadata = new Dictionary<string, object>
            {
                { "gasToken", "USDC" },
                { "chainName", "Arc Testnet" },
                { "rpcUrl", "https://5042002.rpc.thirdweb.com/" },
                { "faucet", "https://faucet.circle.com" },
                { "issuer", "Circle" }
            }
        };
    }

    // === MÉTODOS AUXILIARES ===

    private string NormalizeContractType(string contractType)
    {
        return contractType?.ToLower() switch
        {
            "nft" => "erc721",
            "token" => "erc20",
            "payment" => "erc20",
            "governance" => "erc20",
            _ => contractType?.ToLower() ?? "unknown"
        };
    }

    private string GetCategory(string contractType)
    {
        return contractType?.ToLower() switch
        {
            "token" or "erc20" => "defi",
            "nft" or "erc721" or "erc1155" => "nft",
            "payment" => "payment",
            "governance" => "dao",
            _ => "other"
        };
    }

    private string GenerateAddress(string seed)
    {
        var combined = seed + DateTime.UtcNow.Ticks.ToString();
        var hash = combined.GetHashCode();
        var random = new Random(hash);
        var bytes = new byte[20];
        random.NextBytes(bytes);
        return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    private string GenerateTransactionHash()
    {
        var random = new Random();
        var bytes = new byte[32];
        random.NextBytes(bytes);
        return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    private string GenerateSymbol(string contractName)
    {
        if (string.IsNullOrEmpty(contractName))
            return "UNK";

        var words = contractName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            return (words[0][0].ToString() + words[1][0].ToString()).ToUpper();
        }
        return contractName.Length >= 3
            ? contractName.Substring(0, 3).ToUpper()
            : contractName.ToUpper();
    }
}