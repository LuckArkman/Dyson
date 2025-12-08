namespace Dtos;

/// <summary>
/// Modelo para requisição de criação de contrato
/// </summary>
public class ContractCreationRequestModel
{
    public string ContractName { get; set; }
    public string Symbol { get; set; }
    public string Description { get; set; }
    public string ContractType { get; set; }
    public string Blockchain { get; set; }
    public decimal DeploymentCost { get; set; }
    public string InitialSupply { get; set; }
    public bool CreateLiquidityPool { get; set; }
}