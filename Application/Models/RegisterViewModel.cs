using System.ComponentModel.DataAnnotations;

namespace Application.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "O campo Nome Completo é obrigatório.")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "O campo E-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "O campo Senha é obrigatório.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "A senha e a confirmação de senha não coincidem.")]
    public string ConfirmPassword { get; set; }
}