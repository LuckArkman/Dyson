using System.Security.Claims;
using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IRepositorio<Product> _productRepo;
    private readonly WalletService _walletService;
    private readonly RewardContractService _contractService;
    private readonly ContractDeploymentService _deploymentService;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly IRepositorio<ContractDocument> _contractRepo;
    private readonly ILogger<ProfileController> _logger;

    // Simulando um "Banco de Dados" de Staking em memória para este exemplo
    // Em produção, isso seria uma Collection no Mongo chamada "Stakes"
    private static readonly Dictionary<string, decimal> _mockStakingStore = new();

    public ProfileController(
        IRepositorio<Order> orderRepo,
        IRepositorio<ContractDocument> contractRepo,
        IRepositorio<Product> productRepo, 
        WalletService walletService,
        RewardContractService contractService,
        ContractDeploymentService deploymentService,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _orderRepo = orderRepo;
        _contractRepo = contractRepo;
        _productRepo = productRepo;
        _walletService = walletService;
        _contractService = contractService;
        _deploymentService = deploymentService;
        _logger = logger;
        
        // Inicializa repo de produtos se necessário (garantia)
        _productRepo.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            "Products");
        _orderRepo.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            "Orders");
        _contractRepo.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            "Orders");
    }
    public async Task<IActionResult> Orders()
    {
        ViewData["Title"] = "Histórico de Compras";
        var userId = GetUserId();
        var myOrders = await _orderRepo.GetAllOrdersByUserAsync(userId);
        return View(myOrders);
    }
    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    // Assume que o ID do usuário é usado como chave da carteira para simplificar, 
    // ou busca o endereço real se estiver na Claim.
    private string GetWalletAddress() => GetUserId(); 

    // --- DASHBOARD HOME ---
    public async Task<IActionResult> Index()
    {
        var address = GetWalletAddress();
        var walletkey = await _walletService.GetUserWalletAsync(address);
        var balance = await _walletService.GetBalanceAsync(walletkey.Address);
        var products = await _productRepo.GetAllProductsAsync();
        var transactions = await _walletService.GetHistoryAsync(walletkey.Address);

        // Simulação de valores
        _mockStakingStore.TryGetValue(address, out var staked);

        var model = new DashboardViewModel
        {
            Balance = balance,
            ActiveProducts = products.Count,
            TotalContracts = 20, // Mock
            StakedAmount = staked,
            RecentTransactions = transactions.Take(20).ToList()
        };

        ViewData["Title"] = "Visão Geral";
        return View(model);
    }
    
    public async Task<IActionResult> Wallet()
    {
        var address = GetWalletAddress();
        var walletkey = await _walletService.GetUserWalletAsync(address);
        var balance = await _walletService.GetBalanceAsync(walletkey.Address);
        var history = await _walletService.GetHistoryAsync(walletkey.Address);
        _mockStakingStore.TryGetValue(walletkey.Address, out var staked);

        var model = new WalletViewModel
        {
            WalletAddress = walletkey.Address,
            Balance = balance,
            StakedBalance = staked,
            History = history.ToList(),
            CurrentTokenPrice = GenerateSimulatedPrice() 
        };

        ViewData["Title"] = "Smart Wallet & Trading";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateContract([FromBody] ContractCreationRequestModel model)
    {
        var address = GetWalletAddress();
        
        _logger.LogInformation(
            "Usuário {UserAddress} solicitou deploy de contrato: {ContractName} " +
            "Tipo: {ContractType}, Blockchain: {Blockchain}", 
            address, model.ContractName, model.ContractType, model.Blockchain);

        // 1. Validação básica
        if (string.IsNullOrWhiteSpace(model.ContractName))
        {
            return BadRequest(new { 
                success = false, 
                message = "Nome do contrato é obrigatório." 
            });
        }
        if (model.DeploymentCost <= 0)
        {
            return BadRequest(new { 
                success = false, 
                message = "O custo de implantação deve ser maior que zero." 
            });
        }

        try
        {
            // 2. Chamar serviço de deployment
            var (success, result) = await _deploymentService.DeployContractAsync(address, model);

            if (success)
            {
                // 'result' é o endereço do contrato deployado
                _logger.LogInformation(
                    "Contrato {ContractName} deployado com sucesso no endereço: {ContractAddress}", 
                    model.ContractName, result);

                return Ok(new { 
                    success = true, 
                    message = "Contrato Inteligente deployado com sucesso!", 
                    contractAddress = result, 
                    blockchain = model.Blockchain ?? "sepolia",
                    costCharged = model.DeploymentCost,
                    explorerUrl = GetExplorerUrl(model.Blockchain ?? "sepolia", result)
                });
            }
            else
            {
                // 'result' é a mensagem de erro
                _logger.LogWarning(
                    "Falha no deploy de contrato para usuário {UserAddress}: {Error}", 
                    address, result);

                return BadRequest(new { 
                    success = false, 
                    message = result 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Erro inesperado ao processar deploy de contrato para usuário {UserAddress}", 
                address);

            return StatusCode(500, new { 
                success = false, 
                message = "Erro interno ao processar deploy. Tente novamente." 
            });
        }
    }

    /// <summary>
    /// Retorna a URL do block explorer para verificar o contrato
    /// </summary>
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

    /// <summary>
    /// Endpoint para registrar um contrato já deployado
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterContract([FromBody] RegisterContractRequestModel model)
    {
        var address = GetWalletAddress();

        if (string.IsNullOrWhiteSpace(model.ContractAddress))
        {
            return BadRequest(new { 
                success = false, 
                message = "Endereço do contrato é obrigatório." 
            });
        }

        try
        {
            var (success, message) = await _deploymentService.RegisterExistingContractAsync(
                address,
                model.ContractAddress,
                model.ContractName ?? "Imported Contract"
            );

            if (success)
            {
                return Ok(new { success = true, message });
            }
            else
            {
                return BadRequest(new { success = false, message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar contrato");
            return StatusCode(500, new { 
                success = false, 
                message = "Erro ao registrar contrato." 
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> StakeTokens([FromBody] StakeRequestModel model)
    {
        var address = GetWalletAddress();
        var balance = await _walletService.GetBalanceAsync(address);

        if (balance < model.Amount)
            return BadRequest(new { success = false, message = "Saldo insuficiente para Staking." });

        // Queima tokens da carteira (Lock) e adiciona ao registro de Stake
        await _contractService.BurnTokensAsync(address, model.Amount, $"Staked for {model.DurationDays} days");
        
        if (!_mockStakingStore.ContainsKey(address)) _mockStakingStore[address] = 0;
        _mockStakingStore[address] += model.Amount;

        return Ok(new { success = true, message = "Stake realizado com sucesso!", newBalance = balance - model.Amount });
    }

    [HttpPost]
    public async Task<IActionResult> TradeTokens([FromBody] TradeRequestModel model)
    {
        var address = GetWalletAddress();
        
        if (model.Type == "BUY")
        {
            // Simula compra (Mint)
            await _contractService.MintTokensAsync(address, model.Amount, "Trade Buy Execution");
            return Ok(new { success = true, message = $"Compra de {model.Amount} GLU realizada." });
        }
        else if (model.Type == "SELL")
        {
            var balance = await _walletService.GetBalanceAsync(address);
            if (balance < model.Amount) return BadRequest(new { message = "Saldo insuficiente." });

            await _contractService.BurnTokensAsync(address, model.Amount, "Trade Sell Execution");
            return Ok(new { success = true, message = $"Venda de {model.Amount} GLU realizada." });
        }

        return BadRequest(new { message = "Operação inválida." });
    }
    
    [HttpGet]
    public IActionResult GetChartData()
    {
        // Gera dados simulados para o gráfico (Histórico de 30 dias)
        var labels = new List<string>();
        var data = new List<decimal>();
        var random = new Random();
        decimal currentPrice = 10.50m; // Preço inicial base

        for (int i = 30; i >= 0; i--)
        {
            labels.Add(DateTime.Now.AddDays(-i).ToString("dd/MM"));
            // Flutuação aleatória de -5% a +5%
            decimal change = (decimal)(random.NextDouble() * 0.10 - 0.05);
            currentPrice += currentPrice * change;
            if(currentPrice < 0.1m) currentPrice = 0.1m;
            data.Add(Math.Round(currentPrice, 2));
        }

        return Ok(new { labels, data });
    }

    // --- SMART CONTRACTS ---
    public async Task<IActionResult> Contracts()
    {
        var address = GetWalletAddress();
        var walletkey = await _walletService.GetUserWalletAsync(address);
        var balance = await _walletService.GetBalanceAsync(walletkey.Address);
        var contracts = await _contractRepo.GetUserContractsAsync(walletkey.Address);

        ViewData["Title"] = "Meus Contratos Inteligentes";
        return View(contracts);
    }

    // Helper
    private decimal GenerateSimulatedPrice()
    {
        decimal supply = _contractService.GetTotalSupply();
        if (supply == 0) return 1.0m;
        return 1000000m / supply;
    }
}