using Dtos;

namespace Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResponse> CreatePaymentAsync(Order order, string cpf, string paymentMethod);
    Task<PaymentResponse?> GetPaymentAsync(string transactionId);
}