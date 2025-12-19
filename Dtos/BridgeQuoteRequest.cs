namespace Dtos;

public class BridgeQuoteRequest
{
    public int FromChain { get; set; }
    public int ToChain { get; set; }
    public string FromToken { get; set; }
    public string ToToken { get; set; }
    public string Amount { get; set; }
}