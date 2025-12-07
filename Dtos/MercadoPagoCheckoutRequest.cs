using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Dtos;

/// <summary>
/// Request de checkout com Mercado Pago
/// </summary>
public class MercadoPagoCheckoutRequest
{
    [Required(ErrorMessage = "O método de pagamento é obrigatório")]
    [RegularExpression("^(credit_card|pix|boleto)$", ErrorMessage = "Método de pagamento inválido")]
    public string PaymentMethod { get; set; } = string.Empty;

    // Dados do Cliente
    [Required(ErrorMessage = "Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "CPF é obrigatório")]
    [CpfValidation(ErrorMessage = "CPF inválido")]
    public string Document { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nome é obrigatório")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sobrenome é obrigatório")]
    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    // Dados de Cartão de Crédito (opcional - apenas se PaymentMethod == "credit_card")
    public CreditCardData? CreditCard { get; set; }

    // Endereço (obrigatório para alguns métodos)
    public AddressData? Address { get; set; }
}
