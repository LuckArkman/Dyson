namespace Dtos;

public class NebulaAction
{
    public string Type { get; set; }
    public string TransactionHash { get; set; }
    public string Status { get; set; }
    public Dictionary<string, object> Details { get; set; }
}