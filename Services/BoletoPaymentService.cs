using Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Services;

/// <summary>
/// Serviço para gerar boletos bancários
/// </summary>
public class BoletoPaymentService
{
    private readonly ILogger<BoletoPaymentService> _logger;
    private readonly IConfiguration _configuration;

    public BoletoPaymentService(
        ILogger<BoletoPaymentService> logger, 
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Gera um boleto bancário completo
    /// </summary>
    public async Task<PaymentResponse> GenerateBoletoAsync(
        decimal amount, 
        string cpf, 
        string orderId,
        DateTime? dueDate = null)
    {
        try
        {
            _logger.LogInformation("Gerando Boleto para CPF: {Cpf}, Valor: {Amount}, Pedido: {OrderId}", 
                CpfValidator.Format(cpf), amount, orderId);

            // Remove formatação do CPF
            cpf = CpfValidator.RemoveFormatting(cpf);

            // Valida CPF
            if (!CpfValidator.IsValid(cpf))
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = "CPF inválido"
                };
            }

            // Data de vencimento padrão: 3 dias úteis
            var boletoDueDate = dueDate ?? CalculateDueDate(3);

            // Gera identificadores
            var transactionId = GenerateTransactionId();
            var boletoNumber = GenerateBoletoNumber();
            var barCode = GenerateBarCode(amount, boletoDueDate, boletoNumber);

            // Gera URL para PDF (será criada posteriormente)
            var pdfUrl = $"/Payment/DownloadBoleto?transactionId={transactionId}";

            var response = new PaymentResponse
            {
                Success = true,
                Message = "Boleto gerado com sucesso",
                TransactionId = transactionId,
                Details = new PaymentDetails
                {
                    PaymentMethod = "boleto",
                    BoletoBarCode = barCode,
                    BoletoPdfUrl = pdfUrl,
                    BoletoDueDate = boletoDueDate,
                    BoletoAmount = amount
                }
            };

            _logger.LogInformation("Boleto gerado com sucesso. TransactionId: {TransactionId}", transactionId);

            // Simula chamada assíncrona para gateway externo
            await Task.Delay(500);

            // Aqui você salvaria o boleto no banco de dados para posterior consulta
            await SaveBoletoToDatabase(transactionId, cpf, orderId, amount, barCode, boletoDueDate);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar boleto");
            return new PaymentResponse
            {
                Success = false,
                Message = $"Erro ao gerar boleto: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gera código de barras do boleto (padrão FEBRABAN)
    /// </summary>
    private string GenerateBarCode(decimal amount, DateTime dueDate, string boletoNumber)
    {
        // Código do banco (exemplo: 237 = Bradesco, 341 = Itaú, 001 = Banco do Brasil)
        var bankCode = _configuration["Payment:BankCode"] ?? "001";
        
        // Moeda (9 = Real)
        var currency = "9";
        
        // Fator de vencimento (dias desde 07/10/1997)
        var baseDateFactorDate = new DateTime(1997, 10, 7);
        var dueDateFactor = (dueDate - baseDateFactorDate).Days.ToString("D4");
        
        // Valor do documento (10 dígitos com centavos)
        var amountString = ((int)(amount * 100)).ToString("D10");
        
        // Campo livre (25 dígitos - específico de cada banco)
        var freeField = GenerateFreeField(boletoNumber);
        
        // Monta código de barras sem o dígito verificador
        var barCodeWithoutDigit = $"{bankCode}{currency}XXXXX{dueDateFactor}{amountString}{freeField}";
        
        // Calcula dígito verificador
        var verificationDigit = CalculateBarCodeVerificationDigit(barCodeWithoutDigit);
        
        // Insere dígito verificador na posição 4
        var finalBarCode = $"{bankCode}{currency}{verificationDigit}{dueDateFactor}{amountString}{freeField}";
        
        // Formata para linha digitável (com espaços e pontos)
        return FormatToDigitableLine(finalBarCode);
    }

    /// <summary>
    /// Gera campo livre do boleto (25 dígitos)
    /// </summary>
    private string GenerateFreeField(string boletoNumber)
    {
        // Agência (4 dígitos)
        var agency = _configuration["Payment:AgencyNumber"] ?? "0001";
        
        // Conta (formatada para 10 dígitos)
        var account = _configuration["Payment:AccountNumber"] ?? "0000000001";
        
        // Nosso número (11 dígitos - identificador único do boleto)
        var ourNumber = boletoNumber.PadLeft(11, '0');
        
        return $"{agency}{account}{ourNumber}";
    }

    /// <summary>
    /// Calcula dígito verificador do código de barras (módulo 11)
    /// </summary>
    private string CalculateBarCodeVerificationDigit(string barCode)
    {
        // Remove o marcador de posição "XXXXX"
        barCode = barCode.Replace("XXXXX", "");
        
        int sum = 0;
        int multiplier = 2;

        // Percorre da direita para esquerda
        for (int i = barCode.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(barCode[i])) continue;
            
            sum += int.Parse(barCode[i].ToString()) * multiplier;
            multiplier++;
            if (multiplier > 9) multiplier = 2;
        }

        int remainder = sum % 11;
        int digit = 11 - remainder;

        // Se resultado for 0, 10 ou 11, o dígito é 1
        if (digit == 0 || digit == 10 || digit == 11)
            return "1";

        return digit.ToString();
    }

    /// <summary>
    /// Formata código de barras para linha digitável
    /// </summary>
    private string FormatToDigitableLine(string barCode)
    {
        if (barCode.Length != 44)
            return barCode;

        // Divide o código de barras em campos
        var field1 = barCode.Substring(0, 4) + barCode.Substring(19, 5);
        var field2 = barCode.Substring(24, 10);
        var field3 = barCode.Substring(34, 10);
        var field4 = barCode.Substring(4, 1);
        var field5 = barCode.Substring(5, 14);

        // Formata: XXXXX.XXXXX XXXXX.XXXXXX XXXXX.XXXXXX X XXXXXXXXXXXXXX
        return $"{field1.Substring(0, 5)}.{field1.Substring(5)} " +
               $"{field2.Substring(0, 5)}.{field2.Substring(5)} " +
               $"{field3.Substring(0, 5)}.{field3.Substring(5)} " +
               $"{field4} {field5}";
    }

    /// <summary>
    /// Calcula data de vencimento (dias úteis)
    /// </summary>
    private DateTime CalculateDueDate(int businessDays)
    {
        var date = DateTime.Now.Date;
        int addedDays = 0;

        while (addedDays < businessDays)
        {
            date = date.AddDays(1);
            
            // Pula fins de semana
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                addedDays++;
            }
        }

        return date;
    }

    private string GenerateTransactionId()
    {
        return $"BOL{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid():N}".Substring(0, 32);
    }

    private string GenerateBoletoNumber()
    {
        // Gera número único do boleto (nosso número)
        return DateTime.Now.ToString("yyyyMMddHHmmss");
    }

    /// <summary>
    /// Salva boleto no banco de dados (implementar com seu repositório)
    /// </summary>
    private async Task SaveBoletoToDatabase(
        string transactionId, 
        string cpf, 
        string orderId, 
        decimal amount, 
        string barCode, 
        DateTime dueDate)
    {
        // TODO: Implementar salvamento no MongoDB
        _logger.LogInformation("Salvando boleto no banco: TransactionId={TransactionId}", transactionId);
        await Task.CompletedTask;
    }
}