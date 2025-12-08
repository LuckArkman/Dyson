namespace Dtos;

public class TransactionHistory
{
    public string Hash { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public string Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; }
}