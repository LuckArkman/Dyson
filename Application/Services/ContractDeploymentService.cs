using System.Numerics;
using System.Text;
using System.Text.Json;
using Dtos;
using Interfaces;
using Thirdweb;

namespace Services;

/// <summary>
/// Serviço de implantação de contratos usando Thirdweb.
/// </summary>
public class ContractDeploymentService
{
    private readonly WalletService _walletService;
    private readonly ThirdwebClient _thirdwebClient; 
    private readonly HttpClient _httpClient;
    private IRepositorio<ContractDocument> _repositorio;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContractDeploymentService> _logger;
    private WalletDocument? _walletAddress = null;

    public ContractDeploymentService(
        WalletService walletService, 
        IConfiguration configuration,
        HttpClient httpClient,
        IRepositorio<ContractDocument> repositorio,
        ILogger<ContractDeploymentService> logger)
    {
        _walletService = walletService;
        _configuration = configuration;
        _httpClient = httpClient;
        _repositorio = repositorio;
        _logger = logger;

        // Inicialização do ThirdwebClient
        var secretKey = _configuration["ThirdwebSettings:SecretKey"];
        var clientId = _configuration["ThirdwebSettings:ClientId"];
        
        if (!string.IsNullOrEmpty(secretKey))
        {
            _thirdwebClient = ThirdwebClient.Create(secretKey: secretKey);
            _logger.LogInformation("ThirdwebClient inicializado com SecretKey (Backend mode)");
        }
        else if (!string.IsNullOrEmpty(clientId))
        {
            _thirdwebClient = ThirdwebClient.Create(clientId: clientId);
            _logger.LogInformation("ThirdwebClient inicializado com ClientId (Frontend mode)");
        }
        else
        {
            throw new InvalidOperationException(
                "ThirdwebSettings:ClientId ou ThirdwebSettings:SecretKey deve ser configurado."
            );
        }

        _repositorio.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            "Contracts");
    }

    public async Task<(bool success, string result)> DeployContractAsync(
        string userAddress, 
        ContractCreationRequestModel model)
    {
        
        _logger.LogInformation(
            "Iniciando deploy de contrato para usuário {UserAddress}. " +
            "Nome: {ContractName}, Tipo: {ContractType}", 
            userAddress, model.ContractName, model.ContractType);

        // 1. Verificar saldo APENAS se DeploymentCost > 0
        if (model.DeploymentCost > 0)
        {
            _walletAddress = await _walletService.GetUserWalletAsync(userAddress);
            var walletDoc = await _walletService.GetUserWalletAsync(userAddress);
            if (walletDoc == null)
            {
                return (false, "Wallet não encontrada para o usuário.");
            }

            _walletAddress = await _walletService.GetUserWalletAsync(userAddress);
            var balance = await _walletService.GetBalanceAsync(_walletAddress.Address);
            if (balance < model.DeploymentCost)
            {
                _logger.LogWarning("Saldo insuficiente. Necessário: {Cost}, Disponível: {Balance}", 
                    model.DeploymentCost, balance);
                return (false, $"Saldo insuficiente. Você tem {balance} DTC, mas precisa de {model.DeploymentCost} DTC.");
            }
        }

        // 2. Verificar se Factory está configurado
        var factoryAddress = _configuration["ThirdwebSettings:FactoryContractAddress"];
        var isSimulated = string.IsNullOrEmpty(factoryAddress);

        if (isSimulated)
        {
            //_logger.LogWarning("Factory Contract não configurado. Usando simulação.");
        }

        try
        {
            string contractAddress;
            string deploymentMode;
            string transactionHash = "";

            if (isSimulated)
            {
                // Deploy simulado
                contractAddress = await DeploySimulatedAsync(userAddress, model);
                deploymentMode = "simulated";
            }
            else
            {
                // Deploy via Factory
                contractAddress = await DeployViaFactoryAsync(userAddress, model, factoryAddress);
                deploymentMode = "factory";
            }
            
            _logger.LogInformation(
                "Contrato deployado com sucesso! Endereço: {ContractAddress}", 
                contractAddress);

            // 3. Debitar custo do usuário (apenas se > 0)
            if (model.DeploymentCost > 0)
            {
                var walletDoc = await _walletService.GetUserWalletAsync(userAddress);
                await _walletService.DebitBalanceAsync(walletDoc.Address, model.DeploymentCost, 
                    $"Deploy de contrato: {model.ContractName}");
            }

            // 4. ⭐ SALVAR CONTRATO NO MONGODB ⭐
            var contractDoc = new ContractDocument
            {
                walletAndress = _walletAddress.Address,
                ContractAddress = contractAddress,
                ContractName = model.ContractName,
                Symbol = model.Symbol ?? "TKN",
                ContractType = model.ContractType ?? "token",
                Blockchain = model.Blockchain ?? "sepolia",
                ChainId = GetChainId(model.Blockchain ?? "sepolia"),
                Status = "Active",
                DeploymentCost = model.DeploymentCost,
                GasUsed = 0, // TODO: Extrair do receipt se disponível
                TransactionHash = transactionHash,
                ExplorerUrl = GetExplorerUrl(model.Blockchain ?? "sepolia", contractAddress),
                DeploymentMode = deploymentMode,
                Notes = $"Deployado via {deploymentMode} em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            };

            await _repositorio.SaveContractAsync(contractDoc);
            
            _logger.LogInformation(
                "✅ Contrato salvo no MongoDB! ID: {ContractId}, Endereço: {ContractAddress}", 
                contractDoc.Id, contractDoc.ContractAddress);

            return (true, contractAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer deploy do contrato");
            return (false, $"Falha na implantação: {ex.Message}");
        }
    }

    /// <summary>
    /// Deploy simulado - retorna apenas o endereço gerado
    /// </summary>
    private async Task<string> DeploySimulatedAsync(
        string userAddress,
        ContractCreationRequestModel model)
    {
        _logger.LogInformation("MODO SIMULAÇÃO: Deployando contrato {ContractName}", 
            model.ContractName);

        // Simular delay de deploy
        await Task.Delay(2000);

        // Gerar endereço válido
        var contractAddress = GenerateAddress(model.ContractName);

        return contractAddress;
    }

    private string GetExplorerUrl(string blockchain, string contractAddress)
    {
        var baseUrls = new Dictionary<string, string>
        {
            { "sepolia", "https://sepolia.etherscan.io/address/" },
            { "base-sepolia", "https://sepolia.basescan.org/address/" },
            { "polygon", "https://polygonscan.com/address/" },
            { "mumbai", "https://mumbai.polygonscan.com/address/" },
            { "ethereum", "https://etherscan.io/address/" },
            { "base", "https://basescan.org/address/" },
            { "arbitrum", "https://arbiscan.io/address/" },
            { "optimism", "https://optimistic.etherscan.io/address/" }
        };

        var key = blockchain.ToLower();
        return baseUrls.ContainsKey(key) 
            ? baseUrls[key] + contractAddress 
            : $"https://sepolia.etherscan.io/address/{contractAddress}";
    }

    private async Task<string> DeployViaFactoryAsync(
        string userAddress,
        ContractCreationRequestModel model,
        string factoryAddress)
    {
        var chainId = GetChainId(model.Blockchain ?? "sepolia");
        
        var deployerPrivateKey = _configuration["ThirdwebSettings:DeployerPrivateKey"];
        if (string.IsNullOrEmpty(deployerPrivateKey))
        {
            throw new InvalidOperationException("DeployerPrivateKey não configurada");
        }

        var deployerWallet = await PrivateKeyWallet.Create(
            client: _thirdwebClient, 
            privateKeyHex: deployerPrivateKey
        );
        
        var factoryContract = await ThirdwebContract.Create(
            client: _thirdwebClient,
            address: factoryAddress,
            chain: chainId
        );

        _logger.LogInformation("Chamando Factory Contract em {FactoryAddress}", factoryAddress);

        var methodName = GetFactoryMethodName(model.ContractType);
        
        var receipt = await ThirdwebContract.Write(
            wallet: deployerWallet,
            contract: factoryContract,
            method: methodName,
            weiValue: 0,
            model.ContractName,
            model.Symbol ?? "TKN",
            userAddress
        );

        var newContractAddress = ExtractContractAddressFromReceipt(receipt);
        
        return newContractAddress;
    }

    /// <summary>
    /// MODO SIMULAÇÃO - Para testes sem custo
    /// </summary>
    private async Task<(bool success, string result)> DeployContractSimulatedAsync(
        string userAddress,
        ContractCreationRequestModel model)
    {
        _logger.LogInformation("MODO SIMULAÇÃO: Deployando contrato {ContractName}", 
            model.ContractName);

        // Simular delay de deploy
        await Task.Delay(2000);

        // Gerar endereço simulado porém válido
        var contractAddress = GenerateAddress(model.ContractName);

        // Debitar custo APENAS se > 0 e usuário tiver saldo
        if (model.DeploymentCost > 0)
        {
            var balance = await _walletService.GetBalanceAsync(userAddress);
            if (balance >= model.DeploymentCost)
            {
                await _walletService.DebitBalanceAsync(userAddress, model.DeploymentCost,
                    $"Deploy simulado: {model.ContractName}");
                _logger.LogInformation("Custo de {Cost} Dtc debitado", model.DeploymentCost);
            }
            else
            {
                _logger.LogWarning(
                    "Deploy simulado GRATUITO (saldo insuficiente: {Balance} < {Cost})", 
                    balance, model.DeploymentCost);
            }
        }
        else
        {
            _logger.LogInformation("Deploy simulado GRATUITO (DeploymentCost = 0)");
        }

        _logger.LogInformation(
            "Contrato simulado deployado: {Address} (TESTNET SIMULADA)", 
            contractAddress);

        return (true, contractAddress);
    }

    public async Task<(bool success, string message)> RegisterExistingContractAsync(
        string userAddress,
        string contractAddress,
        string contractName)
    {
        try
        {
            var chainId = GetChainId(_configuration["ThirdwebSettings:TestnetChain"] ?? "sepolia");
            
            var contract = await ThirdwebContract.Create(
                client: _thirdwebClient,
                address: contractAddress,
                chain: chainId
            );

            try
            {
                var name = await ThirdwebContract.Read<string>(contract, "name");
                _logger.LogInformation("Contrato validado: {Name} em {Address}", name, contractAddress);
            }
            catch
            {
                _logger.LogWarning("Não foi possível validar nome do contrato. Pode não ser ERC20/721.");
            }

            return (true, $"Contrato {contractAddress} registrado com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar contrato");
            return (false, $"Erro ao validar contrato: {ex.Message}");
        }
    }

    // === MÉTODOS AUXILIARES ===

    private int GetChainId(string chainIdentifier)
    {
        return chainIdentifier.ToLower() switch
        {
            "sepolia" => 11155111,
            "base-sepolia" => 84532,
            "mumbai" => 80001,
            "polygon-amoy" => 80002,
            "goerli" => 5,
            "polygon" => 137,
            "ethereum" => 1,
            "base" => 8453,
            "arbitrum" => 42161,
            "optimism" => 10,
            _ => int.TryParse(chainIdentifier, out var id) ? id : 11155111
        };
    }

    private string GetFactoryMethodName(string contractType)
    {
        return contractType?.ToLower() switch
        {
            "token" or "erc20" => "deployToken",
            "nft" or "erc721" => "deployNFT",
            "marketplace" => "deployMarketplace",
            _ => "deployToken"
        };
    }

    private string ExtractContractAddressFromReceipt(ThirdwebTransactionReceipt receipt)
    {
        try
        {
            if (receipt.Logs != null && receipt.Logs.Count > 0)
            {
                // Implementar parsing de eventos aqui
            }

            _logger.LogWarning("Usando fallback para extração de endereço.");
            return receipt.ContractAddress ?? "0x" + Guid.NewGuid().ToString("N").Substring(0, 40);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair endereço do receipt");
            throw new InvalidOperationException("Não foi possível extrair o endereço do contrato deployado", ex);
        }
    }

    private string GenerateAddress(string contractName)
    {
        var hash = contractName.GetHashCode();
        var random = new Random(hash);
        var bytes = new byte[20];
        random.NextBytes(bytes);
        return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}

public class DeployApiResponse
{
    public string ContractAddress { get; set; }
    public string TransactionHash { get; set; }
}