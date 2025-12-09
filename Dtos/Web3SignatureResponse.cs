namespace Dtos;

/// <summary>
/// Resposta com mensagem para assinatura
/// </summary>
public class Web3SignatureResponse
{
    public string Message { get; set; }
    public string WalletAddress { get; set; }
    public long Timestamp { get; set; }
}