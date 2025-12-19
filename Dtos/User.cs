using System.ComponentModel.DataAnnotations;

namespace Dtos;

public class User
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string PasswordHash { get; set; }
    public string PersonalName { get; set; }
    public string Nickname { get; set; }
    public DateTime BackupDate { get; set; }
    
    // Campos para autenticação Web3
    public string? WalletAddress { get; set; }
    public string? AuthMethod { get; set; } // "Traditional", "MetaMask", "WalletConnect", etc.
    public DateTime? LastWalletAuth { get; set; }
    public DateTime? CreatedAt { get; set; }
}