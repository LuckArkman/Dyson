using Dtos;
using Interfaces;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Serviço que gerencia múltiplos provedores de IA
/// </summary>
public class AIProviderManager
{
    private readonly Dictionary<string, IAIProvider> _providers;
    private readonly ILogger<AIProviderManager> _logger;

    public AIProviderManager(
        IEnumerable<IAIProvider> providers,
        ILogger<AIProviderManager> logger)
    {
        _providers = providers.ToDictionary(p => p.ProviderName, p => p);
        _logger = logger;
    }

    /// <summary>
    /// Obtém um provedor específico pelo nome
    /// </summary>
    public IAIProvider GetProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException($"Provedor '{providerName}' não encontrado");
        }

        if (!provider.IsConfigured())
        {
            throw new InvalidOperationException($"Provedor '{providerName}' não está configurado. Verifique as API Keys no appsettings.json");
        }

        return provider;
    }

    /// <summary>
    /// Lista todos os provedores disponíveis
    /// </summary>
    public List<ProviderInfo> GetAvailableProviders()
    {
        return _providers.Values.Select(p => new ProviderInfo
        {
            Name = p.ProviderName,
            IsConfigured = p.IsConfigured(),
            Models = p.AvailableModels
        }).ToList();
    }

    /// <summary>
    /// Obtém todos os modelos de todos os provedores
    /// </summary>
    public List<ModelInfo> GetAllAvailableModels()
    {
        var models = new List<ModelInfo>();

        foreach (var provider in _providers.Values)
        {
            if (!provider.IsConfigured()) continue;

            foreach (var model in provider.AvailableModels)
            {
                models.Add(new ModelInfo
                {
                    Provider = provider.ProviderName,
                    Model = model,
                    FullId = $"{provider.ProviderName}:{model.Id}"
                });
            }
        }

        return models.OrderBy(m => m.Model.CostPer1kTokens).ToList();
    }

    /// <summary>
    /// Executa uma requisição de IA usando o provedor especificado
    /// </summary>
    public async Task<AIResponse> ExecuteAsync(string providerName, AIRequest request)
    {
        var provider = GetProvider(providerName);
        
        _logger.LogInformation($"Executando requisição com {providerName} - Modelo: {request.Model}");
        
        try
        {
            var response = await provider.CompleteAsync(request);
            
            _logger.LogInformation($"✅ Sucesso | Tokens: {response.TokensUsed} | Custo: ${response.Cost:F4}");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Erro ao executar {providerName}");
            throw;
        }
    }

    /// <summary>
    /// Executa usando o formato "Provider:Model" (ex: "OpenAI:gpt-4")
    /// </summary>
    public async Task<AIResponse> ExecuteAsync(string providerModelId, string prompt, double temperature = 0.7, int maxTokens = 1000)
    {
        var parts = providerModelId.Split(':');
        
        if (parts.Length != 2)
        {
            throw new ArgumentException("Formato inválido. Use 'Provider:Model' (ex: 'OpenAI:gpt-4')");
        }

        var providerName = parts[0];
        var modelId = parts[1];

        var request = new AIRequest
        {
            Model = modelId,
            Prompt = prompt,
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        return await ExecuteAsync(providerName, request);
    }
}

/// <summary>
/// Informações sobre um provedor
/// </summary>
public class ProviderInfo
{
    public string Name { get; set; }
    public bool IsConfigured { get; set; }
    public List<AIModel> Models { get; set; }
}

/// <summary>
/// Informações sobre um modelo específico
/// </summary>
public class ModelInfo
{
    public string Provider { get; set; }
    public AIModel Model { get; set; }
    public string FullId { get; set; } // "OpenAI:gpt-4"
}