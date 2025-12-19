namespace Dtos;

public class TokenDeployRequest
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int ChainId { get; set; }
    public string InitialSupply { get; set; }
    public bool CreateLiquidityPool { get; set; }
    public string LiquidityAmount { get; set; }
    public string PairedToken { get; set; } = "ETH";
}