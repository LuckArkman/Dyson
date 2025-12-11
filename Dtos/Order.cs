using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

/// <summary>
/// Classe Order completa com suporte para pagamentos tradicionais e Web3
/// </summary>
public class Order
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [BsonElement("userId")]
    public string? UserId { get; set; }
    
    [BsonElement("items")]
    public List<OrderItem> Items { get; set; } = new();
    
    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }
    
    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = "";
    
    [BsonElement("status")]
    public string Status { get; set; } = "Pending"; // Pending, Paid, Canceled, Expired
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    // Campos para pagamentos tradicionais (PIX, Boleto, Cartão)
    [BsonElement("transactionId")]
    public string? TransactionId { get; set; }
    
    [BsonElement("paymentData")]
    public PaymentMetadata? PaymentData { get; set; }
    
    // Campos para pagamentos Web3 / Blockchain
    [BsonElement("walletAddress")]
    public string? WalletAddress { get; set; }
    
    [BsonElement("blockchainNetwork")]
    public string? BlockchainNetwork { get; set; }
    
    [BsonElement("blockchainTxHash")]
    public string? BlockchainTxHash { get; set; }
    
    [BsonElement("tokenContractAddress")]
    public string? TokenContractAddress { get; set; }
    
    [BsonElement("blockNumber")]
    public long? BlockNumber { get; set; }
    
    [BsonElement("blockchainConfirmedAt")]
    public DateTime? BlockchainConfirmedAt { get; set; }
    
    [BsonElement("gasFee")]
    public decimal? GasFee { get; set; }
}

/// <summary>
/// Item individual do pedido
/// </summary>
public class OrderItem
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// IMPORTANTE: ProductId (com P maiúsculo) deve corresponder ao MongoDB
    /// Se no MongoDB está como "ProductId", use exatamente isso
    /// Se está como "productId", mude para [BsonElement("productId")]
    /// </summary>
    [BsonElement("ProductId")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string? ProductId { get; set; }
    
    [BsonElement("ProductName")]
    public string ProductName { get; set; } = "";
    
    [BsonElement("_priceType")]
    [BsonRepresentation(MongoDB.Bson.BsonType.Int32)]
    public Enums.PriceType _priceType { get; set; }
    
    [BsonElement("Price")]
    [BsonRepresentation(MongoDB.Bson.BsonType.Decimal128)]
    public decimal Price { get; set; }
    
    [BsonElement("Quantity")]
    [BsonRepresentation(MongoDB.Bson.BsonType.Int32)]
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// ViewModel para checkout PIX/Boleto
/// </summary>
public class PixPaymentViewModel
{
    public string TransactionId { get; set; } = "";
    public string PixCode { get; set; } = "";
    public string QrCodeBase64 { get; set; } = "";
    public DateTime ExpirationDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = "";
}

/// <summary>
/// ViewModel para checkout Boleto
/// </summary>
public class BoletoPaymentViewModel
{
    public string TransactionId { get; set; } = "";
    public string BarCode { get; set; } = "";
    public string BoletoNumber { get; set; } = "";
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = "";
    public string PdfDownloadUrl { get; set; } = "";
}

/// <summary>
/// Request para checkout com CPF
/// </summary>
public class CheckoutWithCpfRequest
{
    public string PaymentMethod { get; set; } = "";
    public string? Cpf { get; set; }
    public string? CardName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvv { get; set; }
}