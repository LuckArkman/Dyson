namespace Dtos;

// ==================== PAYMENT DTOs ====================

/// <summary>
/// Resultado de uma operação de pagamento
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string Message { get; set; } = "";
    public PaymentDetails? Details { get; set; }
}

/// <summary>
/// Informações de pagamento
/// </summary>
public class PaymentInfo
{
    public bool Success { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string? TransactionId { get; set; }
    public string Message { get; set; } = "";
    public PaymentDetails? Details { get; set; }
}

/// <summary>
/// Metadados de pagamento
/// </summary>
public class PaymentMetadata
{
    public string? PixQrCode { get; set; }
    public string? PixQrCodeBase64 { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? BoletoBarcode { get; set; }
    public string? BoletoPdfUrl { get; set; }
    public DateTime? BoletoDueDate { get; set; }
}

// ==================== REQUEST DTOs ====================

/// <summary>
/// Request de criação de pagamento
/// </summary>
public class CreatePaymentRequest
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string? PayerDocument { get; set; }
    public string? WalletAddress { get; set; }
}

/// <summary>
/// Request de confirmação de transação Web3
/// </summary>
public class ConfirmTransactionRequest
{
    public string OrderId { get; set; } = "";
    public string TransactionHash { get; set; } = "";
    public string Network { get; set; } = "";
}

/// <summary>
/// Request para iniciar transação Web3
/// </summary>
public class InitiateTransactionRequest
{
    public string WalletAddress { get; set; } = "";
}

// ==================== RESPONSE DTOs ====================

/// <summary>
/// Response de status de transação
/// </summary>
public class TransactionStatusResponse
{
    public bool Success { get; set; }
    public string Status { get; set; } = "";
    public string? TransactionHash { get; set; }
    public long? BlockNumber { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Response genérico de API
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}

/// <summary>
/// Response de iniciação de transação
/// </summary>
public class InitiateTransactionResponse
{
    public bool Success { get; set; }
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public string RecipientAddress { get; set; } = "";
    public string? UsdcContractAddress { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Resposta de erro padronizada
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    public Dictionary<string, string>? Errors { get; set; }
}

/// <summary>
/// Detalhes de erro de validação
/// </summary>
public class ValidationError
{
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
}