using System.ComponentModel.DataAnnotations;

namespace Dtos;

public class User
{
    public string Id { get; set; }
    [Required(ErrorMessage = "O campo Nome é obrigatório.")]
    public string? UserName { get; set; }
    [Required(ErrorMessage = "O campo E-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? PersonalName { get; set; }
    public string? Nickname { get; set; }
    public DateTime BackupDate { get; set; }
}