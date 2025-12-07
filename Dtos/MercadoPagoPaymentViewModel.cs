namespace Dtos;

public class MercadoPagoPaymentViewModel
{
    public string PaymentId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    // PIX
    public string? PixQrCode { get; set; }
    public string? PixQrCodeBase64 { get; set; }
    public DateTime? PixExpirationDate { get; set; }
    
    // Boleto
    public string? BoletoUrl { get; set; }
    public string? BoletoBarcode { get; set; }
    public DateTime? BoletoDueDate { get; set; }
    
    // Cartão
    public string? CardLast4Digits { get; set; }
    public int? Installments { get; set; }
}