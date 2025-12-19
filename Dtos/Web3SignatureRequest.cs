namespace Dtos;

/// <summary>
/// Requisição para obter mensagem de assinatura
/// </summary>
public class Web3SignatureRequest
{
    public string WalletAddress { get; set; }
    public string Action { get; set; } = "login"; // login, register, link
}