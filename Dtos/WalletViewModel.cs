namespace Dtos;

public class WalletViewModel
{
    public string WalletAddress { get; set; }
    public decimal Balance { get; set; }
    public decimal StakedBalance { get; set; }
    public List<TransactionDocument> History { get; set; }
    public decimal CurrentTokenPrice { get; set; }
}