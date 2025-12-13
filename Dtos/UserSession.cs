using System.ComponentModel.DataAnnotations;

namespace Dtos;

public class UserSession
{
    [Key] // Chave Primária
    [MaxLength(256)]
    public string SessionToken { get; set; }

    [Required]
    public string UserId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public UserSession(string sessionToken, string userId, DateTime expiresAtUtc)
    {
        SessionToken = sessionToken;
        UserId = userId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public UserSession()
    {
        
    }
}