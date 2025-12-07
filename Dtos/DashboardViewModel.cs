namespace Dtos;

public class DashboardViewModel
{
    public decimal Balance { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalContracts { get; set; }
    public decimal StakedAmount { get; set; }
    public List<TransactionDocument> RecentTransactions { get; set; } = new();
}
