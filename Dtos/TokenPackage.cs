namespace Dtos;

public class TokenPackage
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public decimal Amount { get; set; }
    public decimal PriceDTC { get; set; }
    public decimal TotalPriceUSD { get; set; }
}