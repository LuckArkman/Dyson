using Enums;

namespace Dtos;

public class OrderItem
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    
    public PriceType _priceType { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}