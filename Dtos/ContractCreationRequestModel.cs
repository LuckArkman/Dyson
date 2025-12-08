namespace Dtos;

/// <summary>
/// Modelo para requisição de criação de contrato
/// </summary>
public class ContractCreationRequestModel
{
    public string ContractName { get; set; }
    public string Symbol { get; set; }
    public string ContractType { get; set; } // "token", "nft", "marketplace"
    public string Blockchain { get; set; } // "sepolia", "base-sepolia", etc
    public decimal DeploymentCost { get; set; }
    
    // Para deploy via API (opcional)
    public string ContractBytecode { get; set; }
    public string ContractAbi { get; set; }
}