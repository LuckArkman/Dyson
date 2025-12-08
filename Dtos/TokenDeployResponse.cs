namespace Dtos;

public class TokenDeployResponse
{
    public string ContractAddress { get; set; }
    public string TransactionHash { get; set; }
    public string LiquidityPoolAddress { get; set; }
    public string Status { get; set; }
}