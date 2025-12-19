namespace Dtos;

public class TestRequest
{
    public string ProviderModel { get; set; } // "OpenAI:gpt-4"
    public string Prompt { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 500;
}