using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

public class SmartAgentController : Controller
{
    private readonly IRepositorio<SmartAgent> _repositorioSmartAgent;
    private readonly ILogger<ProfileController> _logger;
    readonly IConfiguration _configuration;

    // Simulando um "Banco de Dados" de Staking em memória para este exemplo
    // Em produção, isso seria uma Collection no Mongo chamada "Stakes"
    private static readonly Dictionary<string, decimal> _mockStakingStore = new();
    public SmartAgentController(
        IRepositorio<SmartAgent> repositorioSmartAgent,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _repositorioSmartAgent = repositorioSmartAgent;
        _configuration = configuration;
        _logger = logger;
        
        // Inicializa repos
        _repositorioSmartAgent.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "SmartAgents");
    }
    
    public async Task<IActionResult> MyAgents()
    {
        return View();
    }
    public async Task<IActionResult> ManageAgents()
    {
        return View();
    }
    public async Task<IActionResult> make()
    {
        return View();
    }
}