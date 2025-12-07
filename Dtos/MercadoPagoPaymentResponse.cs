namespace Dtos;

public class MercadoPagoPaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? PaymentId { get; set; }
    public string? ExternalReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDetail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    // Dados específicos do método
    public MercadoPagoPaymentDetails? Details { get; set; }
}