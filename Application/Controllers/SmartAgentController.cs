using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Services;

namespace Controllers;

[Authorize]
public class SmartAgentController : Controller
{
    private readonly IRepositorio<SmartAgent> _repositorioSmartAgent;
    private readonly ILogger<SmartAgentController> _logger;
    private readonly IConfiguration _configuration;
    private readonly AgentExecutionManager _executionManager;

    public SmartAgentController(
        IRepositorio<SmartAgent> repositorioSmartAgent,
        ILogger<SmartAgentController> logger,
        IConfiguration configuration,
        AgentExecutionManager executionManager)
    {
        _repositorioSmartAgent = repositorioSmartAgent;
        _configuration = configuration;
        _logger = logger;
        _executionManager = executionManager;
        
        _repositorioSmartAgent.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "SmartAgents");
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // ============================================================
    // 1. PÁGINA: MY AGENTS (Visualização por Status)
    // ============================================================
    
    /// <summary>
    /// Página principal: exibe agentes categorizados por status
    /// </summary>
    public async Task<IActionResult> MyAgents()
    {
        var userId = GetUserId();
        var agents = await _repositorioSmartAgent.SearchAsync(x => x.userId == userId);
        
        // Organiza agentes por status
        var viewModel = new
        {
            Running = new List<SmartAgent>(),
            Stopped = new List<SmartAgent>(),
            Pending = new List<SmartAgent>(),
            Failed = new List<SmartAgent>()
        };
        
        foreach (var agent in agents)
        {
            var status = await _executionManager.GetAgentStatusAsync(agent.id);
            
            if (status == null || status.Status == "stopped")
                viewModel.Stopped.Add(agent);
            else if (status.Status == "running")
                viewModel.Running.Add(agent);
            else if (status.Status == "pending")
                viewModel.Pending.Add(agent);
            else if (status.Status == "failed")
                viewModel.Failed.Add(agent);
        }
        
        return View(viewModel);
    }

    // ============================================================
    // 2. PÁGINA: MANAGE AGENTS (Gerenciamento Operacional)
    // ============================================================
    
    /// <summary>
    /// Página de gerenciamento: controle total de agentes
    /// </summary>
    public async Task<IActionResult> ManageAgents()
    {
        var userId = GetUserId();
        var agents = await _repositorioSmartAgent.SearchAsync(x => x.userId == userId);
        
        // Adiciona informações de status para cada agente
        var agentsWithStatus = new List<object>();
        
        foreach (var agent in agents)
        {
            var status = await _executionManager.GetAgentStatusAsync(agent.id);
            agentsWithStatus.Add(new
            {
                Agent = agent,
                Status = status,
                CanStart = status == null || status.Status != "running",
                CanStop = status?.Status == "running",
                CanRestart = status?.Status == "failed" || status?.Status == "stopped"
            });
        }
        
        return View(agentsWithStatus.OrderByDescending(x => ((dynamic)x).Agent.UpdatedAt));
    }
    
    /// <summary>
    /// API: Inicia um agente
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StartAgent(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId) 
            return NotFound("Agente não encontrado");

        var success = await _executionManager.StartAgentAsync(id);
        
        if (success)
            return Ok(new { message = $"Agente '{agent.Name}' iniciado com sucesso" });
        else
            return BadRequest(new { error = "Não foi possível iniciar o agente" });
    }
    
    /// <summary>
    /// API: Para um agente
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StopAgent(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId) 
            return NotFound();

        var success = await _executionManager.StopAgentAsync(id);
        
        if (success)
            return Ok(new { message = $"Agente '{agent.Name}' parado" });
        else
            return BadRequest(new { error = "Agente não está em execução" });
    }
    
    /// <summary>
    /// API: Reinicia um agente
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RestartAgent(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId) 
            return NotFound();

        var success = await _executionManager.RestartAgentAsync(id);
        
        if (success)
            return Ok(new { message = $"Agente '{agent.Name}' reiniciado" });
        else
            return BadRequest(new { error = "Não foi possível reiniciar o agente" });
    }
    
    /// <summary>
    /// API: Obtém logs de execução
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAgentLogs(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId) 
            return NotFound();

        var status = await _executionManager.GetAgentStatusAsync(id);
        
        return Ok(new
        {
            agentName = agent.Name,
            status = status?.Status ?? "unknown",
            logs = status?.ExecutionLogs ?? "Nenhum log disponível",
            errorMessage = status?.ErrorMessage,
            lastExecuted = status?.LastExecutedAt,
            executionCount = status?.ExecutionCount ?? 0,
            successCount = status?.SuccessCount ?? 0,
            failureCount = status?.FailureCount ?? 0
        });
    }

    // ============================================================
    // 3. PÁGINA: MAKE (Editor Visual de Workflows)
    // ============================================================
    
    /// <summary>
    /// Editor visual de workflows (Drawflow)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Make(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return View(new SmartAgent 
            { 
                Name = "Novo Agente",
                Workflow = new WorkflowData()
            }); 
        }

        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId)
            return NotFound("Agente não encontrado");

        return View(agent);
    }
    
    /// <summary>
    /// API: Salva workflow completo
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SmartAgent model)
    {
        if (model == null) return BadRequest("Dados inválidos");

        try 
        {
            var userId = GetUserId();
            model.userId = userId;
            model.UpdatedAt = DateTime.UtcNow;

            if (string.IsNullOrEmpty(model.id))
            {
                // Novo Agente
                model.id = Guid.NewGuid().ToString();
                model.CreatedAt = DateTime.UtcNow;
                await _repositorioSmartAgent.AddAsync(model);
                
                _logger.LogInformation($"Novo agente criado: {model.Name} ({model.id})");
            }
            else
            {
                // Atualização
                var existing = await _repositorioSmartAgent.GetByIdAsync(model.id);
                
                if (existing == null || existing.userId != userId) 
                    return Unauthorized("Acesso negado");

                model.CreatedAt = existing.CreatedAt;
                await _repositorioSmartAgent.UpdateAsync(model);
                
                _logger.LogInformation($"Agente atualizado: {model.Name} ({model.id})");
            }

            return Ok(new 
            { 
                id = model.id, 
                message = "Agente salvo com sucesso",
                nodes = model.Workflow?.Nodes?.Count ?? 0,
                connections = model.Workflow?.Connections?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar agente");
            return StatusCode(500, new { error = "Erro ao salvar agente", details = ex.Message });
        }
    }

    /// <summary>
    /// API: Deleta um agente
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null) return NotFound();
        if (agent.userId != userId) return Unauthorized();

        // Para o agente se estiver rodando
        await _executionManager.StopAgentAsync(id);
        
        // Deleta do banco
        await _repositorioSmartAgent.DeleteAsync(id);

        _logger.LogInformation($"Agente deletado: {agent.Name} ({id})");
        return Ok(new { message = "Agente excluído" });
    }

    /// <summary>
    /// API: Execução manual (teste)
    /// </summary>
    [HttpPost("api/[controller]/{id}/execute")]
    public async Task<IActionResult> ExecuteManual(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null || agent.userId != userId) 
            return NotFound();

        _logger.LogInformation($"Execução manual do agente: {agent.Name}");

        var success = await _executionManager.StartAgentAsync(id);
        
        if (success)
            return Ok(new { message = "Execução iniciada" });
        else
            return BadRequest(new { error = "Não foi possível iniciar a execução" });
    }
    
    /// <summary>
    /// API: Valida workflow antes de salvar
    /// </summary>
    [HttpPost]
    public IActionResult ValidateWorkflow([FromBody] WorkflowData workflow)
    {
        var errors = new List<string>();
        
        // Valida se tem pelo menos um trigger
        var hasTrigger = workflow.Nodes.Any(n => 
            n.Type.Contains("webhook", StringComparison.OrdinalIgnoreCase) ||
            n.Type.Contains("trigger", StringComparison.OrdinalIgnoreCase));
            
        if (!hasTrigger)
            errors.Add("O workflow deve ter pelo menos um nó Trigger (Webhook, Schedule, etc)");
        
        // Valida se todos os nós têm conexões
        var connectedNodes = new HashSet<string>();
        foreach (var conn in workflow.Connections)
        {
            connectedNodes.Add(conn.SourceNodeId);
            connectedNodes.Add(conn.TargetNodeId);
        }
        
        var orphanNodes = workflow.Nodes
            .Where(n => !connectedNodes.Contains(n.Id))
            .Select(n => n.Name)
            .ToList();
            
        if (orphanNodes.Any() && workflow.Nodes.Count > 1)
            errors.Add($"Nós desconectados: {string.Join(", ", orphanNodes)}");
        
        if (errors.Any())
            return BadRequest(new { valid = false, errors });
        else
            return Ok(new { valid = true, message = "Workflow válido" });
    }
}