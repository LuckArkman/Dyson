using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services;

public class RewardListner : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly RewardContractService _rewardContractService;
    private readonly IRepositorio<User> _repositorio;
    private readonly ChatService _chatService;
    private readonly ILogger<RewardListner> _logger;
    
    public RewardListner(
        IConfiguration configuration,
        RewardContractService rewardContractService,
        IRepositorio<User> repositorio,
        ChatService chatService,
        ILogger<RewardListner> logger)
    {
        _configuration = configuration;
        _rewardContractService = rewardContractService;
        _repositorio = repositorio;
        _chatService = chatService;
        _logger = logger;
        _chatService.BlockAdded += OnBlockAdded;
        _repositorio.InitializeCollection(_configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DatabaseName"],
            "Users");
    }

    private async void OnBlockAdded(object? sender, NodeClient node)
    {
        
        var user = await _repositorio.GetUserByIdAsync(node._session.UserId.ToString(), CancellationToken.None);
        if (user == null)
        {
            Console.WriteLine($"[RewardListner]  {node!._session == null}");
            Console.WriteLine($"[RewardListner] ⚠️ Aviso: Tentativa de recompensa falhou. NodeClient {node!.id} and UserId : {node._session.UserId} tem user nulo.");
            return; 
        }
        await _rewardContractService.RewardUserWallet(user.Id);
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Serviço de monitoramento de blocos iniciado.");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _chatService.BlockAdded -= OnBlockAdded;
        return base.StopAsync(cancellationToken);
    }
}