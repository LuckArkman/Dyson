namespace Dtos;

/// <summary>
/// Resposta de autenticação via carteira
/// </summary>
public class Web3AuthResponse
{
    /// <summary>
    /// Indica se a autenticação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Mensagem de retorno
    /// </summary>
    public string Message { get; set; }
    
    /// <summary>
    /// ID do usuário autenticado
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// Nome de usuário
    /// </summary>
    public string UserName { get; set; }
    
    /// <summary>
    /// Email do usuário (pode ser null)
    /// </summary>
    public string Email { get; set; }
    
    /// <summary>
    /// Endereço da carteira
    /// </summary>
    public string WalletAddress { get; set; }
    
    /// <summary>
    /// Indica se é um novo usuário (conta foi criada agora)
    /// </summary>
    public bool IsNewUser { get; set; }
    
    /// <summary>
    /// URL para redirecionamento após login
    /// </summary>
    public string RedirectUrl { get; set; }
    
    /// <summary>
    /// Token JWT (opcional, se estiver usando JWT)
    /// </summary>
    public string Token { get; set; }
}