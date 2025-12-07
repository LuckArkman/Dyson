using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver; // Necessário para a lógica de update direto se não usar classe separada
using Services;

namespace Controllers;

[Route("api/mercadopago")]
public class WebhookController : Controller
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<WebhookController> _logger;
    
    // Injetamos a coleção diretamente ou o serviço que encapsula o update
    private readonly IMongoCollection<Order> _orderCollection; 

    public WebhookController(
        IPaymentGateway paymentGateway,
        ILogger<WebhookController> logger,
        IConfiguration configuration)
    {
        _paymentGateway = paymentGateway;
        _logger = logger;

        // Inicializa conexão direta para fazer o Update otimizado
        var client = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
        var database = client.GetDatabase(configuration["MongoDbSettings:DataBaseName"]);
        _orderCollection = database.GetCollection<Order>("Orders");
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveNotification([FromBody] MercadoPagoWebhookRequest notification)
    {
        try
        {
            // O Mercado Pago espera um 200 OK rápido. Se demorar, eles reenviam.
            // Logamos a entrada
            _logger.LogInformation("🔔 Webhook Recebido: {Type} - ID: {Id}", 
                notification.Type ?? notification.Topic, notification.Data?.Id);

            // 1. Validar se é uma notificação de pagamento
            if (notification.Type == "payment" || notification.Topic == "payment")
            {
                var paymentId = notification.Data?.Id;
                if (string.IsNullOrEmpty(paymentId)) return Ok(); // Ignora se não tiver ID

                // 2. Consultar o Mercado Pago para garantir o status real (Segurança)
                // Nunca confie apenas no payload do webhook, sempre consulte a fonte.
                var paymentInfo = await _paymentGateway.GetPaymentAsync(paymentId);

                if (!paymentInfo.Success)
                {
                    _logger.LogWarning("⚠️ Pagamento {Id} não encontrado no Gateway.", paymentId);
                    return Ok();
                }

                // Precisamos extrair o ExternalReference (ID do Pedido) da resposta do Gateway
                // Supondo que o GetPaymentAsync retorna os detalhes preenchidos.
                // Como GetPaymentAsync atual retorna um objeto genérico, vamos assumir
                // que precisamos buscar o OrderId que salvamos como ExternalReference.
                
                // NOTA: Para isso funcionar perfeitamente, o GetPaymentAsync no Service 
                // precisa retornar o 'external_reference'. Vou simular a obtenção aqui
                // ou você deve garantir que o Service preencha isso no DTO de retorno.
                
                // Aqui faremos uma busca no banco pelo TransactionId se o Gateway não retornar o OrderId direto,
                // OU confiamos que o Gateway retornou o OrderId (ideal).
                
                // Vamos buscar no banco qual pedido tem essa TransactionId
                var filter = Builders<Order>.Filter.Eq(x => x.TransactionId, paymentId);
                var order = await _orderCollection.Find(filter).FirstOrDefaultAsync();

                if (order == null)
                {
                    _logger.LogWarning("⚠️ Pedido não encontrado para a transação {Id}", paymentId);
                    return Ok();
                }

                // 3. Mapear Status do Mercado Pago para Status do Sistema
                string newStatus = paymentInfo.Message.ToLower() switch
                {
                    "approved" => "Paid",
                    "authorized" => "Pending",
                    "in_process" => "Pending",
                    "pending" => "Pending",
                    "rejected" => "Failed",
                    "cancelled" => "Canceled",
                    "refunded" => "Refunded",
                    "charged_back" => "ChargedBack",
                    _ => order.Status // Mantém o atual se desconhecido
                };

                // 4. Atualizar no Banco de Dados se o status mudou
                if (order.Status != newStatus)
                {
                    var update = Builders<Order>.Update
                        .Set(o => o.Status, newStatus)
                        .Set(o => o.PaymentMethod, paymentInfo.Details?.PaymentMethod ?? order.PaymentMethod); // Atualiza método se disponível

                    await _orderCollection.UpdateOneAsync(filter, update);

                    _logger.LogInformation("✅ Pedido {OrderId} atualizado para: {Status}", order.Id, newStatus);
                }
            }

            return Ok(); // Retorna 200 para o Mercado Pago parar de enviar
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar Webhook");
            // Retornar 500 faz o Mercado Pago tentar de novo mais tarde
            return StatusCode(500); 
        }
    }
}