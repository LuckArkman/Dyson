namespace Dtos;

public static class MockPaymentGateway
{
    public static Task<bool> ProcessPayment(string token, decimal amount)
    {
        Console.WriteLine($"Processando pagamento de {amount} com o token {token}...");
        return Task.FromResult(true);
    }
}