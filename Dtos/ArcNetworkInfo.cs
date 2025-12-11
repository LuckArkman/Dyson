namespace Dtos;

public class ArcNetworkInfo
{
    public long ChainId { get; set; }
    public long BlockNumber { get; set; }
    public decimal GasPrice { get; set; }
    public bool IsTestnet { get; set; }
    public string NetworkName { get; set; } = "";
}