namespace Dtos;

public class AIModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal CostPer1kTokens { get; set; }
    public int MaxTokens { get; set; }
    public bool SupportsStreaming { get; set; }
}