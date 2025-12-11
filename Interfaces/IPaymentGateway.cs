using Dtos;
using Nethereum.RPC.Eth.DTOs;

namespace Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResponse> CreatePaymentAsync(Order order, string cpf, string paymentMethod);
    Task<PaymentInfo?> GetPaymentAsync(string transactionId);
    Task<TransactionReceipt?> GetTransactionReceiptAsync(string txHash);

    Task<bool> VerifyUsdcTransferAsync(
        string txHash,
        string expectedReceiver,
        decimal expectedAmount);

    Task<bool> ConfirmPaymentAsync(string orderId, string txHash);
    decimal CalculateGasFee(TransactionReceipt receipt, Transaction transaction);
    Task<decimal> GetUsdcBalanceAsync(string address);
    Task<ArcNetworkInfo> GetNetworkInfoAsync();
}