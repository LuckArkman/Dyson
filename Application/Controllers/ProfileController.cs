using System.Security.Claims;
using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
        
        // Inicializa repos
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
            configuration["MongoDbSettings:DbContracts"] ?? "Contracts");
    }

    public async Task<IActionResult> Orders()
    {
        ViewData["Title"] = "Histórico de Compras";
        var userId = GetUserId();
        var myOrders = await _orderRepo.GetAllOrdersByUserAsync(userId);
        return View(myOrders);
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private string GetWalletAddress() => GetUserId(); 

    // --- DASHBOARD HOME ---
    public IActionResult Index()
    {
        try
        {
            // CORREÇÃO: Verificar se User e Identity não são nulos
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("Unauthenticated access attempt to Profile/Index");
                return RedirectToAction("Login", "Account");
            }

            // CORREÇÃO: Obter claims de forma segura
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuário";
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var walletAddress = User.FindFirst("WalletAddress")?.Value;

            // CORREÇÃO: Criar ViewBag com valores seguros
            ViewBag.UserId = userId ?? "";
            ViewBag.UserName = userName;
            ViewBag.UserEmail = userEmail;
            ViewBag.WalletAddress = walletAddress;
            ViewBag.HasWallet = !string.IsNullOrEmpty(walletAddress);

            _logger.LogInformation("Profile page loaded for user {UserId}", userId);

            return View();
        }
        catch (NullReferenceException ex)
        {
            _logger.LogError(ex, "NullReferenceException in Profile/Index - Line 74 area");
            
            // Redirecionar para login se houver problema com claims
            return RedirectToAction("Login", "Account");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profile page");
            
            // Retornar página de erro ou redirecionar
            return View("Error");
        }
    }
    
    /// <summary>
    /// Faz logout do usuário
    /// POST: /Profile/Logout
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            _logger.LogInformation("User {UserId} ({UserName}) is logging out", userId, userName);

            // Realizar sign out do cookie de autenticação
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("User {UserId} logged out successfully", userId);

            // Redirecionar para página de login
            return RedirectToAction("Login", "Account");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            
            // Mesmo com erro, tentar fazer logout
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch { }

            return RedirectToAction("Login", "Account");
        }
    }
    
    /// <summary>
    /// Página de gerenciamento de carteira e trading
    /// GET: /Profile/Wallet
    /// </summary>
    [HttpGet]
    public IActionResult Wallet()
    {
        try
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            PopulateViewBag();

            _logger.LogInformation("Wallet page loaded for user {UserId}", GetUserId());

            // TODO: Buscar dados reais da carteira do banco de dados
            var model = new Dtos.WalletViewModel
            {
                CurrentTokenPrice = 5.50m,
                TokenBalance = 0,
                TokenValue = 0,
                WalletAddress = GetWalletAddress()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading wallet page");
            return View("Error");
        }
    }
    
    /// <summary>
    /// Página de segurança
    /// GET: /Profile/Security
    /// </summary>
    [HttpGet]
    public IActionResult Security()
    {
        try
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            PopulateViewBag();

            _logger.LogInformation("Security page loaded for user {UserId}", GetUserId());

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading security page");
            return View("Error");
        }
    }
    
    /// <summary>
    /// Página de configurações
    /// GET: /Profile/Settings
    /// </summary>
    [HttpGet]
    public IActionResult Settings()
    {
        try
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            PopulateViewBag();

            _logger.LogInformation("Settings page loaded for user {UserId}", GetUserId());

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings page");
            return View("Error");
        }
    }
    
    /// <summary>
    /// Popula ViewBag com informações do usuário
    /// </summary>
    private void PopulateViewBag()
    {
        ViewBag.UserId = GetUserId();
        ViewBag.UserName = GetUserName();
        ViewBag.UserEmail = GetUserEmail();
        ViewBag.WalletAddress = GetWalletAddress();
        ViewBag.HasWallet = !string.IsNullOrEmpty(GetWalletAddress());
    }
    
    /// <summary>
    /// Obtém nome do usuário
    /// </summary>
    private string GetUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuário";
    }

    /// <summary>
    /// Obtém email do usuário
    /// </summary>
    private string GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    }

    /// <summary>
    /// Deploy de novo contrato (via Arc Testnet ou modo simulação)
    /// </summary>
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

        // Deployment cost pode ser 0 em modo simulação
        if (model.DeploymentCost < 0)
        {
            return BadRequest(new { 
                success = false, 
                message = "O custo de implantação não pode ser negativo." 
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

                // Determinar blockchain
                var blockchain = string.IsNullOrWhiteSpace(model.Blockchain) 
                    ? "arc-testnet" 
                    : model.Blockchain;

                return Ok(new { 
                    success = true, 
                    message = "Contrato Inteligente deployado com sucesso!", 
                    contractAddress = result, 
                    blockchain = blockchain,
                    chainId = GetChainId(blockchain),
                    costCharged = model.DeploymentCost,
                    explorerUrl = GetExplorerUrl(blockchain, result),
                    transactionHash = GetTransactionHashFromResult(result)
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
    /// Atualizado com suporte a Arc Testnet
    /// </summary>
    private string GetExplorerUrl(string blockchain, string contractAddress)
    {
        var baseUrls = new Dictionary<string, string>
        {
            // Arc Testnet (Priority)
            { "arc-testnet", "https://testnet.arcscan.app/address/" },
            { "arc", "https://testnet.arcscan.app/address/" },
            
            // Ethereum
            { "sepolia", "https://sepolia.etherscan.io/address/" },
            { "ethereum", "https://etherscan.io/address/" },
            { "mainnet", "https://etherscan.io/address/" },
            { "eth", "https://etherscan.io/address/" },
            
            // Base
            { "base-sepolia", "https://sepolia.basescan.org/address/" },
            { "base", "https://basescan.org/address/" },
            
            // Polygon
            { "polygon", "https://polygonscan.com/address/" },
            { "mumbai", "https://mumbai.polygonscan.com/address/" },
            { "polygon-amoy", "https://amoy.polygonscan.com/address/" },
            { "matic", "https://polygonscan.com/address/" },
            
            // Arbitrum
            { "arbitrum", "https://arbiscan.io/address/" },
            { "arbitrum-sepolia", "https://sepolia.arbiscan.io/address/" },
            
            // Optimism
            { "optimism", "https://optimistic.etherscan.io/address/" },
            { "optimism-sepolia", "https://sepolia-optimistic.etherscan.io/address/" }
        };

        var key = blockchain?.ToLower() ?? "arc-testnet";
        return baseUrls.ContainsKey(key) 
            ? baseUrls[key] + contractAddress 
            : $"https://testnet.arcscan.app/address/{contractAddress}";
    }

    /// <summary>
    /// Retorna o Chain ID baseado no blockchain
    /// </summary>
    private int GetChainId(string blockchain)
    {
        var chainIds = new Dictionary<string, int>
        {
            { "arc-testnet", 5042002 },
            { "arc", 5042002 },
            { "ethereum", 1 },
            { "mainnet", 1 },
            { "sepolia", 11155111 },
            { "base", 8453 },
            { "base-sepolia", 84532 },
            { "polygon", 137 },
            { "matic", 137 },
            { "mumbai", 80001 },
            { "polygon-amoy", 80002 },
            { "arbitrum", 42161 },
            { "arbitrum-sepolia", 421614 },
            { "optimism", 10 },
            { "optimism-sepolia", 11155420 }
        };

        var key = blockchain?.ToLower() ?? "arc-testnet";
        return chainIds.ContainsKey(key) ? chainIds[key] : 5042002;
    }

    /// <summary>
    /// Extrai transaction hash do resultado (se disponível)
    /// </summary>
    private string GetTransactionHashFromResult(string result)
    {
        // Se result é um endereço (0x...), não temos o hash ainda
        // O hash seria retornado separadamente pelo serviço
        // Por enquanto, retorna vazio
        return string.Empty;
    }

    /// <summary>
    /// Endpoint para registrar um contrato já deployado
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterContract([FromBody] RegisterContractRequestModel model)
    {
        var address = GetWalletAddress();

        _logger.LogInformation(
            "Usuário {UserAddress} solicitou registro de contrato: {ContractAddress}", 
            address, model.ContractAddress);

        // Validação
        if (string.IsNullOrWhiteSpace(model.ContractAddress))
        {
            return BadRequest(new { 
                success = false, 
                message = "Endereço do contrato é obrigatório." 
            });
        }

        // Validar formato do endereço
        if (!model.ContractAddress.StartsWith("0x") || model.ContractAddress.Length != 42)
        {
            return BadRequest(new { 
                success = false, 
                message = "Endereço do contrato inválido. Deve começar com 0x e ter 42 caracteres." 
            });
        }

        try
        {
            // Usar método de registro de contrato existente
            var contractModel = new ContractCreationRequestModel
            {
                ContractName = model.ContractName ?? "Imported Contract",
                ContractType = "imported",
                Blockchain = model.Blockchain ?? "arc-testnet",
                DeploymentCost = 0
            };

            // Aqui você precisaria de um método específico para importar
            // Por enquanto, vou simular o registro
            var contract = new ContractDocument
            {
                WalletAddress = address,
                UserId = address,
                ContractAddress = model.ContractAddress,
                ContractName = model.ContractName ?? "Imported Contract",
                ContractType = "imported",
                Category = "imported",
                Blockchain = model.Blockchain ?? "arc-testnet",
                ChainId = GetChainId(model.Blockchain ?? "arc-testnet"),
                Status = "active",
                DeploymentMode = "imported",
                DeployedAt = DateTime.UtcNow,
                ExplorerUrl = GetExplorerUrl(model.Blockchain ?? "arc-testnet", model.ContractAddress),
                Notes = $"Contrato importado em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                Tags = new List<string> { "imported", model.Blockchain?.ToLower() ?? "arc-testnet" }
            };

            await _contractRepo.SaveContractAsync(contract);

            _logger.LogInformation(
                "Contrato {ContractAddress} registrado com sucesso", 
                model.ContractAddress);

            return Ok(new { 
                success = true, 
                message = "Contrato registrado com sucesso!",
                contractAddress = model.ContractAddress,
                explorerUrl = contract.ExplorerUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar contrato {ContractAddress}", model.ContractAddress);
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
        
        try
        {
            var contracts = await _contractRepo.GetUserContractsAsync(walletkey.Address);

            // Adicionar estatísticas
            var stats = new
            {
                Total = contracts.Count(),
                Active = contracts.Count(c => c.Status == "active"),
                Simulated = contracts.Count(c => c.DeploymentMode == "simulated"),
                RealDeployed = contracts.Count(c => c.DeploymentMode != "simulated"),
                ArcTestnet = contracts.Count(c => c.Blockchain == "arc-testnet"),
                OtherChains = contracts.Count(c => c.Blockchain != "arc-testnet")
            };

            ViewData["Stats"] = stats;
            ViewData["Title"] = "Meus Contratos Inteligentes";
            
            return View(contracts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar contratos do usuário {UserAddress}", address);
            ViewData["Title"] = "Meus Contratos Inteligentes";
            return View(new List<ContractDocument>());
        }
    }

    /// <summary>
    /// Busca detalhes de um contrato específico
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ContractDetails([FromQuery] string contractAddress)
    {
        Console.WriteLine(contractAddress);
        var userId = GetUserId();
        var walletAddress = await _walletService.GetUserWalletAsync(userId);

        // 1. Validação
        if (string.IsNullOrWhiteSpace(contractAddress))
        {
            return BadRequest(new { message = "Endereço do contrato é obrigatório." });
        }

        try
        {
            // 2. Buscar o contrato pelo endereço e garantir que pertence ao usuário logado
            var contract = await _contractRepo.GetContractByAddressAsync(contractAddress);

            if (contract == null)
            {
                return NotFound(new { message = "Contrato não encontrado." });
            }

            // 3. Verificação de propriedade (Segurança)
            if (contract.WalletAddress != walletAddress.Address)
            {
                _logger.LogWarning(
                    "Tentativa de acesso não autorizado ao contrato {ContractAddress} pelo usuário {UserId}", 
                    contractAddress, userId);
                return Forbid();
            }

            // 4. Enriquecer dados
            var enrichedContract = new
            {
                contract.Id,
                contract.ContractAddress,
                contract.ContractName,
                contract.Symbol,
                contract.ContractType,
                contract.Blockchain,
                contract.ChainId,
                contract.Status,
                contract.DeploymentMode,
                contract.DeployedAt,
                contract.TransactionHash,
                contract.ExplorerUrl,
                contract.TotalSupply,
                contract.Decimals,
                contract.Description,
                contract.Notes,
                contract.Tags,
                
                // Informações adicionais
                IsSimulated = contract.DeploymentMode == "simulated",
                IsArcTestnet = contract.Blockchain == "arc-testnet",
                CanVerifyOnExplorer = contract.DeploymentMode != "simulated",
                GasToken = contract.Blockchain == "arc-testnet" ? "USDC" : "ETH",
                
                // URLs úteis
                ExplorerLink = contract.ExplorerUrl,
                FaucetLink = contract.Blockchain == "arc-testnet" 
                    ? "https://faucet.circle.com" 
                    : null
            };
            return Ok(contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar detalhes do contrato {ContractAddress}", contractAddress);
            return StatusCode(500, new { message = "Erro interno ao buscar detalhes." });
        }
    }

    /// <summary>
    /// Busca analytics de um contrato (se disponível)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContractAnalytics([FromQuery] string contractAddress)
    {
        try
        {
            var contract = await _contractRepo.GetContractByAddressAsync(contractAddress);
            
            if (contract == null)
            {
                return NotFound(new { message = "Contrato não encontrado." });
            }

            // Retornar analytics (mock se não estiver disponível)
            var analytics = new
            {
                contract.TransactionCount,
                contract.UniqueHolders,
                contract.TotalVolume,
                contract.LastInteraction,
                contract.DeployedAt,
                DaysActive = (DateTime.UtcNow - contract.DeployedAt),
                IsActive = contract.Status == "active"
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar analytics");
            return StatusCode(500, new { message = "Erro ao buscar analytics." });
        }
    }

    // Helper
    private decimal GenerateSimulatedPrice()
    {
        decimal supply = _contractService.GetTotalSupply();
        if (supply == 0) return 1.0m;
        return 1000000m / supply;
    }
}