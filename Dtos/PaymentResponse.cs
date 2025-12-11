namespace Dtos;

public class PaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string TransactionId { get; set; }
    public PaymentDetails Details { get; set; }
    public decimal Amount { get; set; }
}