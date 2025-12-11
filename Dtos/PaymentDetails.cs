namespace Dtos;

/// <summary>
/// Detalhes de pagamento
/// </summary>
public class PaymentDetails
{
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public string? RecipientAddress { get; set; }
    public string? AssetId { get; set; }
    public string? Network { get; set; }
    public string? BlockNumber { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public decimal? GasFee { get; set; }
    
    // PIX
    public string? PixQrCode { get; set; }
    public string? PixQrCodeImage { get; set; }
    public DateTime? PixExpirationDate { get; set; }
    
    // Boleto
    public string? BoletoBarCode { get; set; }
    public string? BoletoPdfUrl { get; set; }
    public DateTime? BoletoDueDate { get; set; }
    public decimal BoletoAmount { get; set; }
}