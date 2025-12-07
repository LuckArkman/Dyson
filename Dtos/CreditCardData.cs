using System.ComponentModel.DataAnnotations;

namespace Dtos;

public class CreditCardData
{
    [Required(ErrorMessage = "Número do cartão é obrigatório")]
    public string CardNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nome no cartão é obrigatório")]
    public string CardholderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validade é obrigatória")]
    [RegularExpression(@"^\d{2}/\d{2}$", ErrorMessage = "Formato inválido. Use MM/AA")]
    public string ExpirationDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "CVV é obrigatório")]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV deve ter 3 ou 4 dígitos")]
    public string SecurityCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "CPF do titular é obrigatório")]
    public string DocumentNumber { get; set; } = string.Empty;

    // Token gerado pelo Mercado Pago.js no frontend
    public string? Token { get; set; }
}