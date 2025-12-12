namespace Dtos;

public class AIResponse
{
    public string Content { get; set; }
    public string Model { get; set; }
    public int TokensUsed { get; set; }
    public decimal Cost { get; set; }
    public string Provider { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}