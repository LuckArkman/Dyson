using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class MercadoPagoService : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MercadoPagoService> _logger;
    private readonly string _accessToken;

    public MercadoPagoService(HttpClient httpClient, IConfiguration configuration, ILogger<MercadoPagoService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _accessToken = _configuration["MercadoPago:AccessToken"];
    }

    public async Task<PaymentResponse> CreatePaymentAsync(Order order, string cpf, string paymentMethod)
    {
        try
        {
            _logger.LogInformation("🚀 === INICIANDO PAGAMENTO (MODO USUÁRIO VINCULADO) ===");
            var cleanCpf = cpf.Replace(".", "").Replace("-", "").Trim();
            var testUser = await CreateTestUserWithCpfAsync(cleanCpf);
            
            if (testUser == null)
            {
                _logger.LogWarning("⚠️ Falha na criação vinculada. Usando estratégia Guest pura.");
                return await CreatePaymentAsGuestAsync(order, cleanCpf, paymentMethod);
            }

            _logger.LogInformation("👤 User Criado: {Email} | ID: {Id}", testUser.Email, testUser.Id);

            var requestUrl = "https://api.mercadopago.com/v1/payments";

            string mpPaymentMethodId = paymentMethod.ToLower() switch
            {
                "pix" => "pix",
                "boleto" => "bolbradesco",
                _ => "credit_card"
            };

            // 2. MONTA PAYLOAD USANDO O USUÁRIO VINCULADO
            var payload = new
            {
                transaction_amount = (double)order.TotalAmount,
                description = $"Pedido Dyson AI #{order.id.Substring(0, 8)}",
                payment_method_id = mpPaymentMethodId,
                payer = new
                {
                    // Enviamos TUDO para garantir o match
                    id = testUser.Id,
                    email = testUser.Email,
                    first_name = "Comprador",
                    last_name = "Teste",
                    identification = new
                    {
                        type = "CPF",
                        number = cleanCpf
                    }
                },
                external_reference = order.id,
                binary_mode = true // Ajuda a evitar estados intermediários
            };

            return await SendPaymentRequestAsync(requestUrl, payload, paymentMethod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔥 Exception no MercadoPagoService");
            return new PaymentResponse { Success = false, Message = "Erro interno: " + ex.Message };
        }
    }

    public async Task<PaymentResponse> GetPaymentAsync(string transactionId)
    {
        try
        {
            var requestUrl = $"https://api.mercadopago.com/v1/payments/{transactionId}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new PaymentResponse { Success = false, Message = "Pagamento não encontrado." };

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            var result = new PaymentResponse
            {
                Success = true,
                TransactionId = transactionId,
                Message = root.GetProperty("status").GetString() ?? "pending",
                Details = new PaymentDetails()
            };

            // Tenta pegar o valor com segurança
            if(root.TryGetProperty("transaction_amount", out var amountProp))
                result.Amount = amountProp.GetDecimal();

            string type = root.GetProperty("payment_method_id").GetString()!;
            result.Details.PaymentMethod = type;

            if (type == "pix")
            {
                if (root.TryGetProperty("point_of_interaction", out var poi) &&
                    poi.TryGetProperty("transaction_data", out var tData))
                {
                    result.Details.PixQrCode = tData.GetProperty("qr_code").GetString();
                    result.Details.PixQrCodeImage = tData.GetProperty("qr_code_base64").GetString();
                    result.Details.PixExpirationDate = DateTime.Now.AddMinutes(30); 
                }
            }
            else if (type == "bolbradesco" || type == "boleto")
            {
                if (root.TryGetProperty("transaction_details", out var tDetails) &&
                    tDetails.TryGetProperty("external_resource_url", out var pdfUrl))
                {
                    result.Details.BoletoPdfUrl = pdfUrl.GetString();
                }
                if (root.TryGetProperty("barcode", out var barcode) && 
                    barcode.TryGetProperty("content", out var barContent))
                {
                    result.Details.BoletoBarCode = barContent.GetString();
                }
                if (root.TryGetProperty("date_of_expiration", out var expiration))
                {
                    // Tratamento de data seguro
                    if (DateTime.TryParse(expiration.GetString(), out var date))
                        result.Details.BoletoDueDate = date;
                    else
                        result.Details.BoletoDueDate = DateTime.Now.AddDays(3);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar pagamento");
            return new PaymentResponse { Success = false, Message = "Erro ao recuperar dados." };
        }
    }
    
    private object GetDefaultAddress()
    {
        return new 
        {
            zip_code = "01310-100",
            street_name = "Av. Paulista",
            street_number = "1000",
            neighborhood = "Bela Vista",
            city = "São Paulo",
            federal_unit = "SP"
        };
    }

    // Estratégia de Fallback (Guest puro com domínio .com genérico)
    private async Task<PaymentResponse> CreatePaymentAsGuestAsync(Order order, string cleanCpf, string paymentMethod)
    {
        var requestUrl = "https://api.mercadopago.com/v1/payments";
        string mpPaymentMethodId = paymentMethod.ToLower() == "pix" ? "pix" : "bolbradesco";
        
        string guestEmail = $"guest_{Guid.NewGuid().ToString().Substring(0,6)}@sandbox-pagamento.net";

        var payload = new
        {
            transaction_amount = (double)order.TotalAmount,
            description = $"Pedido Dyson AI #{order.id.Substring(0, 8)}",
            payment_method_id = mpPaymentMethodId,
            payer = new
            {
                email = guestEmail,
                first_name = "Comprador",
                last_name = "Guest",
                identification = new
                {
                    type = "CPF",
                    number = cleanCpf
                },
                // AQUI ESTÁ A CORREÇÃO: Endereço obrigatório para Boleto
                address = GetDefaultAddress() 
            },
            external_reference = order.id,
            binary_mode = true
        };

        return await SendPaymentRequestAsync(requestUrl, payload, paymentMethod);
    }

    private async Task<PaymentResponse> SendPaymentRequestAsync(string url, object payload, string paymentMethod)
    {
        var jsonPayload = JsonSerializer.Serialize(payload);
        _logger.LogDebug("📦 Payload: {Json}", jsonPayload);

        var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = requestContent;

        var response = await _httpClient.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("❌ Erro MP [Status {Status}]: {Response}", response.StatusCode, responseString);
            string errorMsg = "Erro no gateway.";
            try {
                using var errDoc = JsonDocument.Parse(responseString);
                if (errDoc.RootElement.TryGetProperty("message", out var msg)) errorMsg = msg.GetString();
                if (errDoc.RootElement.TryGetProperty("cause", out var causes) && causes.GetArrayLength() > 0)
                     errorMsg += $" ({causes[0].GetProperty("description").GetString()})";
            } catch { }
            return new PaymentResponse { Success = false, Message = errorMsg };
        }

        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        
        var result = new PaymentResponse
        {
            Success = true,
            TransactionId = root.GetProperty("id").ToString(),
            Message = "Pagamento gerado com sucesso",
            Details = new PaymentDetails { PaymentMethod = paymentMethod }
        };

        if (paymentMethod.ToLower() == "pix")
        {
            if (root.TryGetProperty("point_of_interaction", out var poi) &&
                poi.TryGetProperty("transaction_data", out var tData))
            {
                result.Details.PixQrCode = tData.GetProperty("qr_code").GetString();
                result.Details.PixQrCodeImage = tData.GetProperty("qr_code_base64").GetString();
                result.Details.PixExpirationDate = DateTime.Now.AddMinutes(30);
            }
        }
        else if (paymentMethod.ToLower() == "boleto")
        {
            if (root.TryGetProperty("transaction_details", out var tDetails) &&
                tDetails.TryGetProperty("external_resource_url", out var pdfUrl))
                {
                    result.Details.BoletoPdfUrl = pdfUrl.GetString();
                }
            if (root.TryGetProperty("barcode", out var barcode) && 
                barcode.TryGetProperty("content", out var barContent))
            {
                result.Details.BoletoBarCode = barContent.GetString();
            }
            // Tentativa segura de data
            if (root.TryGetProperty("date_of_expiration", out var expiration))
            {
                 if (DateTime.TryParse(expiration.GetString(), out var date))
                        result.Details.BoletoDueDate = date;
            }
            if (!result.Details.BoletoDueDate.HasValue) 
                result.Details.BoletoDueDate = DateTime.Now.AddDays(3);
        }

        return result;
    }

    private class TestUserDto { public string Id { get; set; } = ""; public string Email { get; set; } = ""; }

    private async Task<TestUserDto?> CreateTestUserWithCpfAsync(string cpf)
    {
        try
        {
            var url = "https://api.mercadopago.com/users/test_user";
            // Tentamos injetar o CPF e nome na criação
            var payload = new 
            { 
                site_id = "MLB",
                description = "Comprador de Testes"
            };
            
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return new TestUserDto 
                { 
                    Id = doc.RootElement.GetProperty("id").ToString(), 
                    Email = doc.RootElement.GetProperty("email").GetString()!
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
        }
        return null;
    }
}