using System.Security.Claims;
using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly ILogger<PaymentController> _logger;
    private readonly CartService _cartService;
    private static Dictionary<string, List<OrderItem>> _carts = new(); 

    public PaymentController(IPaymentGateway paymentGateway,
        IRepositorio<Order> orderRepo,
        ILogger<PaymentController> logger,
        CartService cartService)
    {
        _paymentGateway = paymentGateway;
        _orderRepo = orderRepo;
        _logger = logger;
        _cartService = cartService;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost]
    [Route("Payment/ProcessCheckout")]
    public async Task<IActionResult> ProcessCheckout([FromBody] CheckoutWithCpfRequest request)
    {
        try
        {
            var userId = GetUserId();
        
            // 1. RECUPERAR O CARRINHO DO BANCO DE DADOS
            Guid.TryParse(userId, out var id);
            var userCart = await _cartService.GetCartByUserIdAsync(id);
        
            if (userCart == null || !userCart.Items.Any())
            {
                return BadRequest(new { success = false, message = "Seu carrinho está vazio." });
            }

            var cartItems = userCart.Items;
            var total = cartItems.Sum(x => x.Price * x.Quantity);

            // Validação extra de segurança
            if (total <= 0) 
                return BadRequest(new { success = false, message = "Valor inválido para pagamento." });

            var orderId = Guid.NewGuid().ToString();
            var order = new Order
            {
                Id = orderId,
                UserId = userId,
                Items = new List<OrderItem>(cartItems), // Copia os itens
                TotalAmount = total, // Agora o total será R$ 25,00
                PaymentMethod = request.PaymentMethod,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            // Chama o Gateway com o valor correto
            var paymentResult = await _paymentGateway.CreatePaymentAsync(order, request.Cpf ?? "00000000000", request.PaymentMethod);

            if (!paymentResult.Success)
            {
                return BadRequest(new { success = false, message = paymentResult.Message });
            }
            order.TransactionId = paymentResult.TransactionId;
            
            // Aqui salvamos os dados críticos para o usuário pagar depois
            if (paymentResult.Details != null)
            {
                order.PaymentData = new PaymentMetadata
                {
                    // Dados do PIX
                    PixQrCode = paymentResult.Details.PixQrCode,
                    PixQrCodeBase64 = paymentResult.Details.PixQrCodeImage,
                    ExpirationDate = paymentResult.Details.PixExpirationDate,

                    // Dados do Boleto
                    BoletoBarcode = paymentResult.Details.BoletoBarCode,
                    BoletoPdfUrl = paymentResult.Details.BoletoPdfUrl,
                    BoletoDueDate = paymentResult.Details.BoletoDueDate
                };
            }
            
            await _orderRepo.InsertOneAsync(order);
            
            await _cartService.ClearCartAsync(id);

            string redirectUrl;
            if (request.PaymentMethod.ToLower() == "pix")
                redirectUrl = $"/Payment/Pix/{paymentResult.TransactionId}";
            else if (request.PaymentMethod.ToLower() == "boleto")
                redirectUrl = $"/Payment/Boleto/{paymentResult.TransactionId}";
            else
                redirectUrl = "/Profile/Orders";

            return Ok(new { success = true, redirectUrl = redirectUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no checkout");
            return StatusCode(500, new { success = false, message = "Erro interno: " + ex.Message });
        }
    }
    
    [HttpGet]
    [Route("Payment/Pix/{transactionId}")]
    public async Task<IActionResult> Pix(string transactionId)
    {
        var paymentInfo = await _paymentGateway.GetPaymentAsync(transactionId);
        
        if (!paymentInfo.Success || paymentInfo.Details == null)
            return RedirectToAction("Error", "Home");

        var model = new PixPaymentViewModel
        {
            TransactionId = transactionId,
            PixCode = paymentInfo.Details.PixQrCode ?? "N/A",
            QrCodeBase64 = paymentInfo.Details.PixQrCodeImage ?? "",
            ExpirationDate = paymentInfo.Details.PixExpirationDate ?? DateTime.Now.AddMinutes(30),
            Amount = paymentInfo.Amount,
            OrderId = transactionId
        };

        return View(model);
    }

    // --- ROTA BOLETO ---
    [HttpGet]
    [Route("Payment/Boleto/{transactionId}")]
    public async Task<IActionResult> Boleto(string transactionId)
    {
        var paymentInfo = await _paymentGateway.GetPaymentAsync(transactionId);

        if (!paymentInfo.Success || paymentInfo.Details == null)
            return RedirectToAction("Error", "Home");

        var model = new BoletoPaymentViewModel
        {
            TransactionId = transactionId,
            BarCode = paymentInfo.Details.BoletoBarCode ?? "N/A",
            BoletoNumber = transactionId,
            DueDate = paymentInfo.Details.BoletoDueDate ?? DateTime.Now.AddDays(3),
            Amount = paymentInfo.Amount,
            OrderId = transactionId,
            PdfDownloadUrl = paymentInfo.Details.BoletoPdfUrl ?? "#"
        };

        return View(model);
    }
}