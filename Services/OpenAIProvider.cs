using Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Dtos;

namespace Services;

/// <summary>
/// Implementação do provedor OpenAI
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;

    public string ProviderName => "OpenAI";

    public List<AIModel> AvailableModels => new()
    {
        new AIModel
        {
            Id = "gpt-4-turbo",
            Name = "GPT-4 Turbo",
            Description = "Mais poderoso e atual",
            CostPer1kTokens = 0.01m,
            MaxTokens = 128000,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "gpt-4",
            Name = "GPT-4",
            Description = "Raciocínio avançado",
            CostPer1kTokens = 0.03m,
            MaxTokens = 8192,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "gpt-3.5-turbo",
            Name = "GPT-3.5 Turbo",
            Description = "Rápido e eficiente",
            CostPer1kTokens = 0.002m,
            MaxTokens = 16385,
            SupportsStreaming = true
        }
    };

    public OpenAIProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _apiKey = _configuration["AIProviders:OpenAI:ApiKey"];
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<AIResponse> CompleteAsync(AIRequest request)
    {
        if (!IsConfigured())
            throw new InvalidOperationException("OpenAI API Key não configurada");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        var requestBody = new
        {
            model = request.Model,
            messages = request.Messages ?? new List<AIMessage>
            {
                new AIMessage { Role = "user", Content = request.Prompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenAI API Error: {responseText}");
        }

        var result = JsonDocument.Parse(responseText);
        var root = result.RootElement;

        return new AIResponse
        {
            Content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString(),
            Model = root.GetProperty("model").GetString(),
            TokensUsed = root.GetProperty("usage").GetProperty("total_tokens").GetInt32(),
            Cost = CalculateCost(request.Model, root.GetProperty("usage").GetProperty("total_tokens").GetInt32()),
            Provider = ProviderName,
            Metadata = new Dictionary<string, object>
            {
                ["finish_reason"] = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
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