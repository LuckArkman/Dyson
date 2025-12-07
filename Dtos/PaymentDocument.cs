using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

[BsonIgnoreExtraElements]
public class PaymentDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("mercadoPagoPaymentId")]
    public long? MercadoPagoPaymentId { get; set; }

    [BsonElement("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = "pending"; // pending, approved, rejected, cancelled

    [BsonElement("statusDetail")]
    public string? StatusDetail { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "BRL";

    [BsonElement("customerEmail")]
    public string CustomerEmail { get; set; } = string.Empty;

    [BsonElement("customerDocument")]
    public string CustomerDocument { get; set; } = string.Empty;

    [BsonElement("customerName")]
    public string? CustomerName { get; set; }

    // Dados específicos de cada método
    [BsonElement("pixQrCode")]
    public string? PixQrCode { get; set; }

    [BsonElement("pixExpirationDate")]
    public DateTime? PixExpirationDate { get; set; }

    [BsonElement("boletoUrl")]
    public string? BoletoUrl { get; set; }

    [BsonElement("boletoBarcode")]
    public string? BoletoBarcode { get; set; }

    [BsonElement("boletoDueDate")]
    public DateTime? BoletoDueDate { get; set; }

    [BsonElement("cardLast4Digits")]
    public string? CardLast4Digits { get; set; }

    [BsonElement("installments")]
    public int? Installments { get; set; }

    // Timestamps
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("approvedAt")]
    public DateTime? ApprovedAt { get; set; }

    // Webhook
    [BsonElement("webhookNotifications")]
    public List<WebhookNotification> WebhookNotifications { get; set; } = new();
}