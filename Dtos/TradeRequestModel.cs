namespace Dtos;

public class TradeRequestModel
{
    public string Type { get; set; } // "BUY" or "SELL"
    public decimal Amount { get; set; }
}