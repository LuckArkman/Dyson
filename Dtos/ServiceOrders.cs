using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class ServiceOrders
{
    [BsonId]
    public Guid Type { get; set; }
    
    [BsonElement("transactionId")]
    public string? TransactionId { get; set; } // ID da transação no Mercado Pago

    [BsonElement("pixCode")]
    public string? PixCode { get; set; } // Código "Copia e Cola" do PIX

    [BsonElement("pixQrCodeImage")]
    public string? PixQrCodeImage { get; set; } // Base64 da imagem QR Code
}