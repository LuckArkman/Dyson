namespace Dtos;

public class LinkWalletRequest
{
    public string WalletAddress { get; set; }
    public string Signature { get; set; }
    public string Message { get; set; }
}