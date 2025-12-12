using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Services;

namespace Controllers;

[Authorize] // Garante que apenas usuários logados acessem
public class SmartAgentController : Controller
{
    private readonly IRepositorio<SmartAgent> _repositorioSmartAgent;
    private readonly ILogger<ProfileController> _logger;
    readonly IConfiguration _configuration;

    public SmartAgentController(
        IRepositorio<SmartAgent> repositorioSmartAgent,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _repositorioSmartAgent = repositorioSmartAgent;
        _configuration = configuration;
        _logger = logger;
        
        // Inicializa conexão com a coleção "SmartAgents" no Mongo
        _repositorioSmartAgent.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "SmartAgents");
    }

    // Helper para obter o ID do usuário logado (via Claims do Cookie/JWT)
    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // ============================================================
    // 1. VISUALIZAÇÃO (VIEWS)
    // ============================================================

    // GET: Lista visual (Cards) dos agentes do usuário
    public async Task<IActionResult> MyAgents()
    {
        var userId = GetUserId();
        
        // Busca todos os agentes onde OwnerId é igual ao usuário logado
        // Assumindo que seu IRepositorio tenha um método Search ou GetAll que aceite lambda
        var agents = await _repositorioSmartAgent.SearchAsync(x => x.userId == userId);
        
        // Ordena por data de atualização (mais recentes primeiro)
        return View(agents.OrderByDescending(x => x.UpdatedAt));
    }

    // GET: Lista administrativa (Tabela DataTables)
    public async Task<IActionResult> ManageAgents()
    {
        var userId = GetUserId();
        var agents = await _repositorioSmartAgent.SearchAsync(x => x.userId == userId);
        
        // Ordena por data de criação
        return View(agents.OrderByDescending(x => x.CreatedAt));
    }

    // GET: Editor Visual (Drawflow)
    // Se receber ID, carrega para edição. Se não, abre vazio.
    [HttpGet]
    public async Task<IActionResult> Make(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            // Cria um novo modelo vazio para o formulário
            return View(new SmartAgent { Name = "Novo Agente" }); 
        }

        var userId = GetUserId();
        
        // Busca o agente pelo ID
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        // Segurança: Verifica se o agente existe e se pertence ao usuário logado
        if (agent == null || agent.userId != userId)
        {
            return NotFound("Agente não encontrado ou você não tem permissão para editá-lo.");
        }

        return View(agent);
    }

    // ============================================================
    // 2. AÇÕES DE API (SALVAR / DELETAR / EXECUTAR)
    // ============================================================

    // POST: Salva o JSON do Drawflow no Banco de Dados
    // Chamado via fetch/AJAX no Make.cshtml
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SmartAgent model)
    {
        if (model == null) return BadRequest("Dados inválidos.");

        try 
        {
            var userId = GetUserId();
            model.userId = userId; // Garante a propriedade
            model.UpdatedAt = DateTime.UtcNow;

            // Verifica se é um INSERT (Novo) ou UPDATE (Existente)
            if (string.IsNullOrEmpty(model.id))
            {
                // Novo Agente
                // Gera ID (se o DTO não gerar no construtor) e Data de Criação
                if(string.IsNullOrEmpty(model.id)) model.id = Guid.NewGuid().ToString();
                
                model.CreatedAt = DateTime.UtcNow;
                
                await _repositorioSmartAgent.AddAsync(model);
            }
            else
            {
                // Atualização
                var existing = await _repositorioSmartAgent.GetByIdAsync(model.id);
                
                // Validação de Segurança antes de sobrescrever
                if (existing == null || existing.userId != userId) 
                    return Unauthorized("Acesso negado.");

                // Mantém a data de criação original
                model.CreatedAt = existing.CreatedAt;
                
                await _repositorioSmartAgent.UpdateAsync(model);
            }

            return Ok(new { id = model.id, message = "Agente salvo com sucesso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar agente.");
            return StatusCode(500, "Erro interno ao salvar o agente.");
        }
    }

    // POST: Exclui um agente
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(id);

        if (agent == null) return NotFound();
        if (agent.userId != userId) return Unauthorized();

        await _repositorioSmartAgent.DeleteAsync(id);

        return Ok(new { message = "Agente excluído." });
    }

    // POST: Endpoint para executar o workflow (Gatilho Manual)
    [HttpPost("api/[controller]/{id}/execute")]
    public async Task<IActionResult> ExecuteManual(string id)
    {
        var userId = GetUserId();
        var agent = await _repositorioSmartAgent.GetByIdAsync(
            id : id,
            none: CancellationToken.None);

        if (agent == null || agent.userId != userId) return NotFound();

        // AQUI ENTRARIA A LÓGICA DA ENGINE DE EXECUÇÃO
        // Por enquanto, apenas simulamos o sucesso para o Frontend
        _logger.LogInformation($"Executando workflow manual para o agente: {agent.Name}");

        // TODO: Injetar o WorkflowEngine e chamar _engine.RunWorkflowAsync(agent, null);

        return Ok(new { message = "Execução iniciada em background." });
    }
}