using Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Dtos;

namespace Services;

/// <summary>
/// Implementação do provedor Google Gemini
/// </summary>
public class GoogleGeminiProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;

    public string ProviderName => "Google Gemini";

    public List<AIModel> AvailableModels => new()
    {
        new AIModel
        {
            Id = "gemini-2.0-flash-exp",
            Name = "Gemini 2.0 Flash",
            Description = "Mais recente e rápido",
            CostPer1kTokens = 0.0m, // Gratuito em preview
            MaxTokens = 1048576,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "gemini-1.5-pro",
            Name = "Gemini 1.5 Pro",
            Description = "Contexto extremamente longo",
            CostPer1kTokens = 0.0035m,
            MaxTokens = 2097152,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "gemini-1.5-flash",
            Name = "Gemini 1.5 Flash",
            Description = "Rápido e econômico",
            CostPer1kTokens = 0.00015m,
            MaxTokens = 1048576,
            SupportsStreaming = true
        }
    };

    public GoogleGeminiProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _apiKey = _configuration["AIProviders:Google:ApiKey"];
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<AIResponse> CompleteAsync(AIRequest request)
    {
        if (!IsConfigured())
            throw new InvalidOperationException("Google API Key não configurada");

        var client = _httpClientFactory.CreateClient();

        // Converte mensagens para o formato do Gemini
        var contents = new List<object>();
        
        if (request.Messages != null && request.Messages.Any())
        {
            foreach (var msg in request.Messages)
            {
                contents.Add(new
                {
                    role = msg.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = msg.Content } }
                });
            }
        }
        else
        {
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = request.Prompt } }
            });
        }

        var requestBody = new
        {
            contents = contents,
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:generateContent?key={_apiKey}";
        var response = await client.PostAsync(url, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Google Gemini API Error: {responseText}");
        }

        var result = JsonDocument.Parse(responseText);
        var root = result.RootElement;

        var candidates = root.GetProperty("candidates");
        string responseContent = "";
        
        if (candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            var contentProp = firstCandidate.GetProperty("content");
            var parts = contentProp.GetProperty("parts");
            
            if (parts.GetArrayLength() > 0)
            {
                responseContent = parts[0].GetProperty("text").GetString();
            }
        }

        // Gemini não retorna contagem de tokens diretamente na resposta
        // Precisaria fazer uma chamada adicional ou estimar
        int estimatedTokens = responseContent.Length / 4; // Estimativa aproximada

        return new AIResponse
        {
            Content = responseContent,
            Model = request.Model,
            TokensUsed = estimatedTokens,
            Cost = CalculateCost(request.Model, estimatedTokens),
            Provider = ProviderName,
            Metadata = new Dictionary<string, object>
            {
                ["finish_reason"] = candidates[0].GetProperty("finishReason").GetString()
            }
        };
    }

    private decimal CalculateCost(string model, int tokens)
    {
        var modelInfo = AvailableModels.FirstOrDefault(m => m.Id == model);
        if (modelInfo == null) return 0;
        
        return (tokens / 1000m) * modelInfo.CostPer1kTokens;
    }
}