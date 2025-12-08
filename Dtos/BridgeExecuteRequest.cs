namespace Dtos;

public class BridgeExecuteRequest
{
    public int FromChain { get; set; }
    public int ToChain { get; set; }
    public string FromToken { get; set; }
    public string ToToken { get; set; }
    public string Amount { get; set; }
    public string WalletAddress { get; set; }
}