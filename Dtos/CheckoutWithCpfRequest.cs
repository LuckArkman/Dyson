using System.ComponentModel.DataAnnotations;

namespace Dtos;

/// <summary>
/// Request para checkout com CPF obrigatório para PIX e Boleto
/// </summary>
public class CheckoutWithCpfRequest
{
    [Required(ErrorMessage = "O método de pagamento é obrigatório")]
    [RegularExpression("^(card|pix|boleto)$", ErrorMessage = "Método de pagamento inválido")]
    public string PaymentMethod { get; set; } = string.Empty;

    // CPF obrigatório para PIX e Boleto
    [RequiredIfPaymentMethod("pix", "boleto", ErrorMessage = "CPF é obrigatório para PIX e Boleto")]
    [CpfValidation(ErrorMessage = "CPF inválido")]
    public string? Cpf { get; set; }

    // Dados de cartão (opcional - apenas se PaymentMethod == "card")
    public string? CardName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvv { get; set; }
}

/// <summary>
/// Resposta após processamento do pagamento
/// </summary>
public class PaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string? TransactionId { get; set; }
    public PaymentDetails? Details { get; set; }
}

/// <summary>
/// Detalhes específicos do pagamento (PIX ou Boleto)
/// </summary>
public class PaymentDetails
{
    // Para PIX
    public string? PixQrCode { get; set; }          // Código PIX (string para copiar)
    public string? PixQrCodeImage { get; set; }     // Base64 da imagem QR Code
    public DateTime? PixExpirationDate { get; set; }

    // Para Boleto
    public string? BoletoBarCode { get; set; }      // Código de barras
    public string? BoletoPdfUrl { get; set; }       // URL para download do PDF
    public DateTime? BoletoDueDate { get; set; }    // Data de vencimento
    public decimal? BoletoAmount { get; set; }
    
    // Comum
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// ViewModel para exibir PIX
/// </summary>
public class PixPaymentViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string PixCode { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel para exibir Boleto
/// </summary>
public class BoletoPaymentViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string BarCode { get; set; } = string.Empty;
    public string BoletoNumber { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string PdfDownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// Atributo customizado para validação condicional de CPF
/// </summary>
public class RequiredIfPaymentMethodAttribute : ValidationAttribute
{
    private readonly string[] _requiredMethods;

    public RequiredIfPaymentMethodAttribute(params string[] requiredMethods)
    {
        _requiredMethods = requiredMethods;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var paymentMethodProperty = instance.GetType().GetProperty("PaymentMethod");
        
        if (paymentMethodProperty == null)
            return ValidationResult.Success;

        var paymentMethod = paymentMethodProperty.GetValue(instance)?.ToString()?.ToLower();
        
        if (paymentMethod != null && _requiredMethods.Contains(paymentMethod))
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
            {
                return new ValidationResult(ErrorMessage ?? "Campo obrigatório para este método de pagamento");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Atributo de validação de CPF
/// </summary>
public class CpfValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return ValidationResult.Success; // Validação de obrigatoriedade é feita por outro atributo

        var cpf = value.ToString()!;
        
        if (!CpfValidator.IsValid(cpf))
        {
            return new ValidationResult(ErrorMessage ?? "CPF inválido");
        }

        return ValidationResult.Success;
    }
}