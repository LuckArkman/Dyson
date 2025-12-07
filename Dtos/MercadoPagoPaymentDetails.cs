namespace Dtos;

public class MercadoPagoPaymentDetails
{
    // PIX
    public string? PixQrCode { get; set; }
    public string? PixQrCodeBase64 { get; set; }
    public DateTime? PixExpirationDate { get; set; }

    // Boleto
    public string? BoletoUrl { get; set; }
    public string? BoletoBarcode { get; set; }
    public DateTime? BoletoDueDate { get; set; }
    public string? BoletoPdfUrl { get; set; }

    // Cartão de Crédito
    public string? Last4Digits { get; set; }
    public string? PaymentMethodId { get; set; }
    public int? Installments { get; set; }
}