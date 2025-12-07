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
    private readonly IRepositorio<Order> _orderRepo;
    private readonly ILogger<ProfileController> _logger;

    // Simulando um "Banco de Dados" de Staking em memória para este exemplo
    // Em produção, isso seria uma Collection no Mongo chamada "Stakes"
    private static readonly Dictionary<string, decimal> _mockStakingStore = new();

    public ProfileController(
        IRepositorio<Order> orderRepo,
        IRepositorio<Product> productRepo, 
        WalletService walletService,
        RewardContractService contractService,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _walletService = walletService;
        _contractService = contractService;
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
            ActiveProducts = products.Count, // Filtrar por usuário se Product tivesse UserId
            TotalContracts = 5, // Mock
            StakedAmount = staked,
            RecentTransactions = transactions.Take(20).ToList()
        };

        ViewData["Title"] = "Visão Geral";
        return View(model);
    }

    // --- SMART WALLET & TRADING ---
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
    public IActionResult Contracts()
    {
        // Lista mockada de contratos do usuário
        var contracts = new List<dynamic>
        {
            new { Id = Guid.NewGuid(), Name = "Royalty Distribution", Status = "Active", Type = "Payment", GasUsed = 0.0042m },
            new { Id = Guid.NewGuid(), Name = "Content Copyright", Status = "Pending", Type = "NFT", GasUsed = 0.0000m },
            new { Id = Guid.NewGuid(), Name = "DAO Vote", Status = "Executed", Type = "Governance", GasUsed = 0.0120m }
        };

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