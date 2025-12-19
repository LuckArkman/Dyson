using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Order
{
    [BsonId]
    public string id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; }

    [BsonElement("items")]
    public List<OrderItem> Items { get; set; } = new();

    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } // "CreditCard", "Pix", "Boleto"

    [BsonElement("status")]
    public string Status { get; set; } // "Pending", "Paid", "Canceled"

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string? TransactionId { get; set; } = String.Empty;

    // --- NOVO: Dados persistidos para pagamento posterior ---
    public PaymentMetadata? PaymentData { get; set; } = null;
}