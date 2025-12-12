namespace Dtos;

public class AIRequest
{
    public string Model { get; set; }
    public string Prompt { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1000;
    public List<AIMessage>? Messages { get; set; }
    public Dictionary<string, object>? AdditionalParameters { get; set; }
}