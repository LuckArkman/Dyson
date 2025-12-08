namespace Dtos;

/// <summary>
/// Modelo para registrar um contrato já deployado
/// </summary>
public class RegisterContractRequestModel
{
    public string ContractAddress { get; set; }
    public string ContractName { get; set; }
    public string Blockchain { get; set; }
}