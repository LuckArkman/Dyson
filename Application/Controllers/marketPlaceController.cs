using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Collections.Generic; // Adicionar para o mock

namespace Controllers;

public class marketPlaceController : Controller
{
    private readonly IRepositorio<User> _repositorioUser;
    private readonly IRepositorio<SmartAgent> _productRepo;
    private readonly WalletService _walletService;
    private readonly RewardContractService _contractService;
    private readonly ContractDeploymentService _deploymentService;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly IRepositorio<ContractDocument> _contractRepo;
    private readonly ILogger<ProfileController> _logger;

    // Simulando um "Banco de Dados" de Staking em memória para este exemplo
    // Em produção, isso seria uma Collection no Mongo chamada "Stakes"
    private static readonly Dictionary<string, decimal> _mockStakingStore = new();
    
    // Mock de Produtos/Agentes para o Marketplace
    private static readonly List<SmartAgent> _mockProducts = new()
    {
        new SmartAgent
        {
            id = Guid.NewGuid().ToString(),
            Name = "Agente de Otimização de Trading",
            Description = "Analisa e otimiza trades em tempo real.",
            price = 500m,
            Type = "Agent"
        },
        new SmartAgent
        {
            id = Guid.NewGuid().ToString(),
            Name = "Agente de Análise de Sentimento",
            Description = "Monitora redes sociais para insights de mercado.",
            price = 350m, Type = "Agent"
        },
        new SmartAgent
            {
                id = Guid.NewGuid().ToString(),
                Name = "DTC Token",
                Description = "Token de utilidade da plataforma Dyson.AI.",
                price = 0.50m,
                Type = "Token"
                
            }
    };
    
    public marketPlaceController(
        IRepositorio<User> repositorioUser,
        IRepositorio<Order> orderRepo,
        IRepositorio<ContractDocument> contractRepo,
        IRepositorio<SmartAgent> productRepo, 
        WalletService walletService,
        RewardContractService contractService,
        ContractDeploymentService deploymentService,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _repositorioUser = repositorioUser;
        _orderRepo = orderRepo;
        _contractRepo = contractRepo;
        _productRepo = productRepo;
        _walletService = walletService;
        _contractService = contractService;
        _deploymentService = deploymentService;
        _logger = logger;
        
        // Inicializa repos
        _repositorioUser.InitializeCollection(
            configuration["MongoDbSettings:ConnectionString"],
            configuration["MongoDbSettings:DataBaseName"],
            "Users");
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
    
    // Ação Principal do Marketplace
    public IActionResult Market()
    {
        // Envia todos os produtos (Agents e o Token DTC) para a View
        ViewBag.Products = _mockProducts;
        return View();
    }
    
    // Ação para listar Agentes Inteligentes
    public IActionResult Agents()
    {
        ViewBag.Agents = _mockProducts;
        return View();
    }
    
    public IActionResult Sell(string type = "")
    {
        // Define o tipo de cadastro, se selecionado. ISSO É CRÍTICO.
        ViewBag.SellType = type; 

        ViewBag.UserAgents = new List<SmartAgent> 
        {
            new SmartAgent { id = "A001", Name = "Agente Otimizador de Trading (v1.2)", Description = "IA para negociação de alta frequência.", Type = "Trading", price = 1500.00m },
            new SmartAgent { id = "A002", Name = "Agente de Análise de Sentimento (Beta)", Description = "Monitora redes sociais e notícias.", Type = "Análise", price = 800.00m },
            new SmartAgent { id = "A003", Name = "Agente de Auditoria de Contrato (P)", Description = "Verifica vulnerabilidades em Smart Contracts.", Type = "Segurança", price = 2200.00m }
        };
        ViewBag.AgentCategories = new List<string> { 
            "Trading & Otimização", 
            "Análise de Sentimento", 
            "Segurança & Auditoria", 
            "Criação de Contratos",
            "Manutenção de Dados"
        };
        ViewBag.TokenPackages = new List<int> { 1000, 2500, 5000, 10000 };
    
        return View();
    }
    
    [HttpPost]
    public IActionResult RegisterAgent(SmartAgent model)
    {
        if (ModelState.IsValid)
        {
            // **Lógica de Back-end:** Salvar o agente no BD/Blockchain.
            // O valor deve ser verificado em relação ao preço de mercado.
            _logger.LogInformation($"Novo Smart Agent cadastrado para venda: {model.Name} por {model.price} DTC.");
            TempData["Message"] = $"O Smart Agent '{model.Name}' foi listado com sucesso no Marketplace!";
            TempData["MessageType"] = "success";
            return RedirectToAction("Index");
        }
    
        // Se houver erro, retorna para a mesma página com os dados
        TempData["Message"] = "Houve um erro no cadastro do Agente. Verifique os campos.";
        TempData["MessageType"] = "error";
        return RedirectToAction("Sell"); 
    }

// Ação de Cadastro de Pacote de Tokens (Simulação)
    [HttpPost]
    public IActionResult RegisterTokenPackage(TokenPackage model)
    {
        if (ModelState.IsValid)
        {
            // **Lógica de Back-end:** Criar a ordem de venda de tokens.
            // O usuário deve ter a quantidade de tokens na carteira para escrow.
            _logger.LogInformation($"Pacote de Tokens cadastrado para venda: {model.Amount} DTC por ${model.TotalPriceUSD}.");
            TempData["Message"] = $"Seu pacote de {model.Amount} DTC foi listado para venda por ${model.TotalPriceUSD}.";
            TempData["MessageType"] = "success";
            return RedirectToAction("Index");
        }

        // Se houver erro, retorna para a mesma página com os dados
        TempData["Message"] = "Houve um erro no cadastro do Pacote de Tokens. Verifique os campos.";
        TempData["MessageType"] = "error";
        return RedirectToAction("Sell"); 
    }
    
    // Ação de Compra de DTC (Simulação)
    [HttpPost]
    public IActionResult BuyDTC(decimal amount)
    {
        // Lógica de transação: Interação com _walletService e _contractService para compra de tokens.
        // **Neste ponto, a integração Web3 real é necessária.**
        _logger.LogInformation($"Tentativa de compra de {amount} DTC.");
        
        // Simulação de Sucesso
        TempData["Message"] = $"Compra de {amount} DTC iniciada. Aguardando confirmação da transação na Blockchain.";
        TempData["MessageType"] = "success";
        
        return RedirectToAction("Index");
    }

    // Ação de Venda de DTC (Simulação)
    [HttpPost]
    public IActionResult SellDTC(decimal amount)
    {
        // Lógica de transação: Interação com _walletService e _contractService para venda de tokens.
        // **Neste ponto, a integração Web3 real é necessária.**
        _logger.LogInformation($"Tentativa de venda de {amount} DTC.");

        // Simulação de Sucesso
        TempData["Message"] = $"Venda de {amount} DTC iniciada. Aguardando confirmação da transação na Blockchain.";
        TempData["MessageType"] = "success";
        
        return RedirectToAction("Index");
    }
    
    // Ação de Compra de Agente (Simulação)
    [HttpPost]
    public IActionResult BuyAgent(string agentId)
    {
        
        var agent = _mockProducts.Find(p => p.id == agentId && p.Type == "Agent");
        if (agent == null)
        {
            TempData["Message"] = "Agente não encontrado.";
            TempData["MessageType"] = "error";
            return RedirectToAction("Agents");
        }
        
        // Lógica de transação: Dedução de tokens/moeda, transferência do agente (via NFT/Smart Contract).
        _logger.LogInformation($"Tentativa de compra do Agente: {agent.Name}.");

        // Simulação de Sucesso
        TempData["Message"] = $"Compra do Agente '{agent.Name}' por {agent.price} DTC iniciada.";
        TempData["MessageType"] = "success";
        
        return RedirectToAction("Agents");
    }
}