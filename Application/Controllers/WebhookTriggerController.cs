using Microsoft.AspNetCore.Mvc;
using Interfaces;
using Dtos;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Controllers;

[Route("webhook")]
public class WebhookTriggerController : Controller
{
    private readonly IRepositorio<SmartAgent> _agentRepo;
    private readonly IWorkflowEngine _engine;
    private readonly ILogger<WebhookTriggerController> _logger;

    public WebhookTriggerController(
        IRepositorio<SmartAgent> agentRepo, 
        IWorkflowEngine engine,
        ILogger<WebhookTriggerController> logger)
    {
        _agentRepo = agentRepo;
        _engine = engine;
        _logger = logger;
    }

    // URL para chamar: POST /webhook/{agentId}
    [HttpPost("{agentId}")]
    public async Task<IActionResult> Invoke(string agentId)
    {
        // 1. Ler o corpo da requisição (Payload do GitHub/Stripe/Etc)
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        
        // 2. Buscar o Agente
        var agent = await _agentRepo.GetByIdAsync(agentId);
        if (agent == null) return NotFound(new { error = "Agente não encontrado" });

        // 3. Verificar se tem um nó Webhook no fluxo
        var webhookNode = agent.Workflow.Nodes.FirstOrDefault(n => n.Type.Contains("webhook", StringComparison.OrdinalIgnoreCase));
        
        if (webhookNode == null) 
            return BadRequest(new { error = "Este agente não possui um gatilho Webhook configurado." });

        _logger.LogInformation($"Webhook recebido para {agent.Name}. Payload: {body}");

        // 4. Injetar o Payload no motor (Precisaríamos atualizar o Engine para aceitar input inicial)
        // Por enquanto, disparamos o fluxo padrão
        // Idealmente: _engine.RunWorkflowAsync(agent, initialData: body);
        
        // Fire-and-forget (Executa em background para não travar quem chamou)
        _ = Task.Run(() => _engine.RunWorkflowAsync(agent));

        return Ok(new { message = "Workflow iniciado com sucesso", executionId = Guid.NewGuid() });
    }
}