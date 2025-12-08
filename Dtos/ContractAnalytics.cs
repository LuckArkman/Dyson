namespace Dtos;

public class ContractAnalytics
{
    public int TransactionCount { get; set; }
    public int UniqueHolders { get; set; }
    public string TotalVolume { get; set; }
    public DateTime LastActivity { get; set; }
}