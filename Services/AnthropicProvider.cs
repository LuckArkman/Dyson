using Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Dtos;

namespace Services;

/// <summary>
/// Implementação do provedor Anthropic (Claude)
/// </summary>
public class AnthropicProvider : IAIProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;

    public string ProviderName => "Anthropic Claude";

    public List<AIModel> AvailableModels => new()
    {
        new AIModel
        {
            Id = "claude-opus-4-20250514",
            Name = "Claude Opus 4",
            Description = "Mais inteligente e capaz",
            CostPer1kTokens = 0.015m,
            MaxTokens = 200000,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "claude-sonnet-4-20250514",
            Name = "Claude Sonnet 4",
            Description = "Equilíbrio ideal",
            CostPer1kTokens = 0.003m,
            MaxTokens = 200000,
            SupportsStreaming = true
        },
        new AIModel
        {
            Id = "claude-haiku-4-20250514",
            Name = "Claude Haiku 4",
            Description = "Rápido e econômico",
            CostPer1kTokens = 0.0008m,
            MaxTokens = 200000,
            SupportsStreaming = true
        }
    };

    public AnthropicProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _apiKey = _configuration["AIProviders:Anthropic:ApiKey"];
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<AIResponse> CompleteAsync(AIRequest request)
    {
        if (!IsConfigured())
            throw new InvalidOperationException("Anthropic API Key não configurada");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = request.Model,
            messages = request.Messages ?? new List<AIMessage>
            {
                new AIMessage { Role = "user", Content = request.Prompt }
            },
            max_tokens = request.MaxTokens,
            temperature = request.Temperature
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Anthropic API Error: {responseText}");
        }

        var result = JsonDocument.Parse(responseText);
        var root = result.RootElement;

        var contentArray = root.GetProperty("content");
        string responseContent = "";
        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.GetProperty("type").GetString() == "text")
            {
                responseContent = item.GetProperty("text").GetString();
                break;
            }
        }

        int inputTokens = root.GetProperty("usage").GetProperty("input_tokens").GetInt32();
        int outputTokens = root.GetProperty("usage").GetProperty("output_tokens").GetInt32();
        int totalTokens = inputTokens + outputTokens;

        return new AIResponse
        {
            Content = responseContent,
            Model = root.GetProperty("model").GetString(),
            TokensUsed = totalTokens,
            Cost = CalculateCost(request.Model, totalTokens),
            Provider = ProviderName,
            Metadata = new Dictionary<string, object>
            {
                ["stop_reason"] = root.GetProperty("stop_reason").GetString(),
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens
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