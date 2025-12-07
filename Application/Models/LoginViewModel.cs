using System.ComponentModel.DataAnnotations;

namespace Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "O campo E-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "O campo Senha é obrigatório.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}