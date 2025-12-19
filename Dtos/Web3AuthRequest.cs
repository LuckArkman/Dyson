namespace Dtos;

/// <summary>
/// Requisição de autenticação via carteira
/// </summary>
public class Web3AuthRequest
{
    public string WalletAddress { get; set; }
    public string Signature { get; set; }
    public string Message { get; set; }
}