namespace Dtos;

public class TransactionResponse
{
    public string TransactionHash { get; set; }
    public string Status { get; set; }
    public string BlockNumber { get; set; }
    public decimal GasUsed { get; set; }
}