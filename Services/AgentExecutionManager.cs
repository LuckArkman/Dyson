using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Services;

/// <summary>
/// Serviço responsável por gerenciar o ciclo de vida e execução dos Smart Agents
/// </summary>
public class AgentExecutionManager
{
    private readonly IRepositorio<SmartAgent> _agentRepo;
    private readonly IRepositorio<AgentExecutionStatus> _statusRepo;
    private readonly IWorkflowEngine _engine;
    private readonly ILogger<AgentExecutionManager> _logger;
    private readonly IConfiguration _configuration;
    
    // Dicionário de agentes em execução (thread-safe)
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningAgents;
    
    public AgentExecutionManager(
        IRepositorio<SmartAgent> agentRepo,
        IRepositorio<AgentExecutionStatus> statusRepo,
        IWorkflowEngine engine,
        ILogger<AgentExecutionManager> logger,
        IConfiguration configuration)
    {
        _agentRepo = agentRepo;
        _statusRepo = statusRepo;
        _engine = engine;
        _logger = logger;
        _configuration = configuration;
        _runningAgents = new ConcurrentDictionary<string, CancellationTokenSource>();
        
        // Inicializa repositórios
        _statusRepo.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "AgentExecutionStatus");
    }
    
    /// <summary>
    /// Inicia a execução de um agente
    /// </summary>
    public async Task<bool> StartAgentAsync(string agentId)
    {
        try
        {
            var agent = await _agentRepo.GetByIdAsync(agentId);
            if (agent == null)
            {
                _logger.LogWarning($"Agente {agentId} não encontrado");
                return false;
            }
            
            // Verifica se já está rodando
            if (_runningAgents.ContainsKey(agentId))
            {
                _logger.LogWarning($"Agente {agentId} já está em execução");
                return false;
            }
            
            // Cria token de cancelamento
            var cts = new CancellationTokenSource();
            _runningAgents.TryAdd(agentId, cts);
            
            // Cria status de execução
            var status = new AgentExecutionStatus
            {
                AgentId = agentId,
                AgentName = agent.Name,
                Status = "running",
                StartedAt = DateTime.UtcNow,
                LastExecutedAt = DateTime.UtcNow
            };
            
            await _statusRepo.AddAsync(status);
            
            // Inicia execução em background
            _ = Task.Run(async () =>
            {
                try
                {
                    var logs = await _engine.RunWorkflowAsync(agent, "{}");
                    
                    status.Status = "stopped";
                    status.FinishedAt = DateTime.UtcNow;
                    status.ExecutionLogs = logs;
                    status.SuccessCount++;
                    
                    await _statusRepo.UpdateAsync(status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao executar agente {agentId}");
                    
                    status.Status = "failed";
                    status.ErrorMessage = ex.Message;
                    status.FinishedAt = DateTime.UtcNow;
                    status.FailureCount++;
                    
                    await _statusRepo.UpdateAsync(status);
                }
                finally
                {
                    _runningAgents.TryRemove(agentId, out _);
                }
            }, cts.Token);
            
            _logger.LogInformation($"Agente {agent.Name} iniciado com sucesso");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao iniciar agente {agentId}");
            return false;
        }
    }
    
    /// <summary>
    /// Para a execução de um agente
    /// </summary>
    public async Task<bool> StopAgentAsync(string agentId)
    {
        try
        {
            if (!_runningAgents.TryRemove(agentId, out var cts))
            {
                _logger.LogWarning($"Agente {agentId} não está em execução");
                return false;
            }
            
            cts.Cancel();
            
            // Atualiza status
            var status = await GetAgentStatusAsync(agentId);
            if (status != null)
            {
                status.Status = "stopped";
                status.FinishedAt = DateTime.UtcNow;
                await _statusRepo.UpdateAsync(status);
            }
            
            _logger.LogInformation($"Agente {agentId} parado com sucesso");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao parar agente {agentId}");
            return false;
        }
    }
    
    /// <summary>
    /// Reinicia um agente que falhou
    /// </summary>
    public async Task<bool> RestartAgentAsync(string agentId)
    {
        await StopAgentAsync(agentId);
        await Task.Delay(1000); // Aguarda 1 segundo
        return await StartAgentAsync(agentId);
    }
    
    /// <summary>
    /// Obtém o status atual de um agente
    /// </summary>
    public async Task<AgentExecutionStatus?> GetAgentStatusAsync(string agentId)
    {
        var statuses = await _statusRepo.SearchAsync(s => s.AgentId == agentId);
        return statuses.OrderByDescending(s => s.StartedAt).FirstOrDefault();
    }
    
    /// <summary>
    /// Lista todos os agentes em execução
    /// </summary>
    public async Task<List<AgentExecutionStatus>> GetRunningAgentsAsync(string userId)
    {
        var agents = await _agentRepo.SearchAsync(a => a.userId == userId);
        var agentIds = agents.Select(a => a.id).ToList();
        
        var statuses = new List<AgentExecutionStatus>();
        
        foreach (var agentId in agentIds)
        {
            var status = await GetAgentStatusAsync(agentId);
            if (status != null && status.Status == "running")
            {
                statuses.Add(status);
            }
        }
        
        return statuses;
    }
    
    /// <summary>
    /// Lista agentes por status
    /// </summary>
    public async Task<List<AgentExecutionStatus>> GetAgentsByStatusAsync(string userId, string status)
    {
        var agents = await _agentRepo.SearchAsync(a => a.userId == userId);
        var agentIds = agents.Select(a => a.id).ToList();
        
        var statuses = new List<AgentExecutionStatus>();
        
        foreach (var agentId in agentIds)
        {
            var agentStatus = await GetAgentStatusAsync(agentId);
            if (agentStatus != null && agentStatus.Status == status)
            {
                statuses.Add(agentStatus);
            }
        }
        
        return statuses;
    }
}