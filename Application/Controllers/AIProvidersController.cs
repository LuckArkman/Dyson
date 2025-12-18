using Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[Authorize]
[Route("api/[controller]")]
public class AIProvidersController : Controller
{
    private readonly AIProviderManager _aiManager;
    private readonly ILogger<AIProvidersController> _logger;

    public AIProvidersController(
        AIProviderManager aiManager,
        ILogger<AIProvidersController> logger)
    {
        _aiManager = aiManager;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/AIProviders/available
    /// Lista todos os provedores disponíveis
    /// </summary>
    [HttpGet("available")]
    public IActionResult GetAvailableProviders()
    {
        try
        {
            var providers = _aiManager.GetAvailableProviders();
            
            return Ok(new
            {
                success = true,
                count = providers.Count,
                providers = providers.Select(p => new
                {
                    name = p.Name,
                    isConfigured = p.IsConfigured,
                    modelsCount = p.Models.Count,
                    models = p.Models.Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        description = m.Description,
                        costPer1kTokens = m.CostPer1kTokens,
                        maxTokens = m.MaxTokens,
                        supportsStreaming = m.SupportsStreaming
                    })
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar provedores");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/AIProviders/models
    /// Lista todos os modelos de todos os provedores (ordenado por custo)
    /// </summary>
    [HttpGet("models")]
    public IActionResult GetAllModels()
    {
        try
        {
            var models = _aiManager.GetAllAvailableModels();
            
            return Ok(new
            {
                success = true,
                count = models.Count,
                models = models.Select(m => new
                {
                    fullId = m.FullId, // "OpenAI:gpt-4"
                    provider = m.Provider,
                    id = m.Model.Id,
                    name = m.Model.Name,
                    description = m.Model.Description,
                    costPer1kTokens = m.Model.CostPer1kTokens,
                    maxTokens = m.Model.MaxTokens,
                    supportsStreaming = m.Model.SupportsStreaming
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar modelos");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/AIProviders/test
    /// Testa uma requisição com um provedor específico
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestProvider([FromBody] TestRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderModel))
        {
            return BadRequest(new { error = "ProviderModel é obrigatório (ex: 'OpenAI:gpt-4')" });
        }

        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest(new { error = "Prompt é obrigatório" });
        }

        try
        {
            var response = await _aiManager.ExecuteAsync(
                request.ProviderModel,
                request.Prompt,
                request.Temperature,
                request.MaxTokens
            );

            return Ok(new
            {
                success = true,
                provider = response.Provider,
                model = response.Model,
                content = response.Content,
                tokensUsed = response.TokensUsed,
                cost = response.Cost,
                metadata = response.Metadata
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar provedor");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/AIProviders/costs
    /// Calcula custos estimados para diferentes modelos
    /// </summary>
    [HttpGet("costs")]
    public IActionResult GetCostEstimates([FromQuery] int tokens = 1000)
    {
        try
        {
            var models = _aiManager.GetAllAvailableModels();
            
            var estimates = models.Select(m => new
            {
                fullId = m.FullId,
                provider = m.Provider,
                model = m.Model.Name,
                costPer1kTokens = m.Model.CostPer1kTokens,
                estimatedCost = (tokens / 1000m) * m.Model.CostPer1kTokens,
                maxTokens = m.Model.MaxTokens
            }).OrderBy(x => x.estimatedCost).ToList();

            return Ok(new
            {
                success = true,
                tokensEstimated = tokens,
                models = estimates
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular custos");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}