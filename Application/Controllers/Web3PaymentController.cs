using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Dtos;
using Services;
using Interfaces;
using Models;

namespace Controllers;

[Authorize]
public class Web3PaymentController : Controller
{
    private readonly ILogger<Web3PaymentController> _logger;
    private readonly IConfiguration _configuration;
    private readonly CartService _cartService;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly IPaymentGateway _paymentGateway;

    public Web3PaymentController(
        ILogger<Web3PaymentController> logger,
        IConfiguration configuration,
        CartService cartService,
        IRepositorio<Order> orderRepo,
        IPaymentGateway paymentGateway)
    {
        _logger = logger;
        _configuration = configuration;
        _cartService = cartService;
        _orderRepo = orderRepo;
        _paymentGateway = paymentGateway;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    /// <summary>
    /// GET: /Web3Payment/Checkout
    /// Página de checkout Arc
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            // Obter carrinho
            Guid.TryParse(userId, out var userGuid);
            var cart = await _cartService.GetCartByUserIdAsync(userGuid);

            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Seu carrinho está vazio.";
                return RedirectToAction("Cart", "Shop");
            }

            // Calcular total
            var total = cart.Items.Sum(i => i.Price * i.Quantity);

            // Criar ViewModel
            var viewModel = new Web3CheckoutViewModel
            {
                CartItems = cart.Items,
                TotalAmount = total,
                RecipientAddress = _configuration["Arc:RecipientAddress"] ?? "",
                ChainId = _configuration["Arc:ChainId"] ?? "5042002",
                Network = "Arc Testnet",
                IsTestnet = bool.Parse(_configuration["Arc:IsTestnet"] ?? "true"),
                ExplorerUrl = "https://testnet.arcscan.app",
                RpcUrl = _configuration["Arc:RpcUrl"] ?? "https://arc-testnet.drpc.org"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar checkout");
            TempData["Error"] = "Erro ao carregar checkout. Tente novamente.";
            return RedirectToAction("Cart", "Shop");
        }
    }

    /// <summary>
    /// POST: /Web3Payment/InitiateTransaction
    /// Inicia uma transação Web3
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> InitiateTransaction([FromBody] InitiateTransactionRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Usuário não autenticado" });

            // Obter carrinho
            Guid.TryParse(userId, out var userGuid);
            var cart = await _cartService.GetCartByUserIdAsync(userGuid);

            if (cart == null || !cart.Items.Any())
                return Json(new { success = false, message = "Carrinho vazio" });

            // Calcular total
            var total = cart.Items.Sum(i => i.Price * i.Quantity);

            // Criar pedido pendente
            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Items = cart.Items,
                TotalAmount = total,
                PaymentMethod = "USDC",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                WalletAddress = request.WalletAddress,
                BlockchainNetwork = "Arc Testnet"
            };

            await _orderRepo.InsertOneAsync(order);

            _logger.LogInformation($"Transação iniciada: Order {order.Id}, Wallet {request.WalletAddress}");

            return Json(new
            {
                success = true,
                orderId = order.Id,
                amount = total,
                recipientAddress = _configuration["Arc:RecipientAddress"],
                message = "Transação iniciada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar transação");
            return Json(new { success = false, message = "Erro ao iniciar transação" });
        }
    }

    /// <summary>
    /// POST: /Web3Payment/ConfirmTransaction
    /// Confirma uma transação após ser enviada na blockchain
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmTransaction([FromBody] ConfirmTransactionRequest request)
    {
        try
        {
            _logger.LogInformation($"Confirmando transação: Order {request.OrderId}, TxHash {request.TransactionHash}");

            var order = await _orderRepo.GetByIdAsync(request.OrderId);
            
            if (order == null)
                return Json(new { success = false, message = "Pedido não encontrado" });

            // Verificar e confirmar pagamento usando o serviço
            bool confirmed = false;
            
            if (_paymentGateway is ArcPaymentService arcService)
            {
                confirmed = await arcService.ConfirmPaymentAsync(request.OrderId, request.TransactionHash);
            }
            else
            {
                // Fallback: atualizar manualmente
                order.Status = "Paid";
                order.BlockchainTxHash = request.TransactionHash;
                order.BlockchainNetwork = request.Network;
                order.BlockchainConfirmedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                
                await _orderRepo.UpdateAsync(order.Id, order);
                confirmed = true;
            }

            if (!confirmed)
                return Json(new { success = false, message = "Falha ao verificar transação na blockchain" });

            // Limpar carrinho
            Guid.TryParse(order.UserId, out var userGuid);
            await _cartService.ClearCartAsync(userGuid);

            _logger.LogInformation($"Pagamento confirmado: Order {order.Id}");

            return Json(new
            {
                success = true,
                message = "Pagamento confirmado!",
                redirectUrl = Url.Action("Success", new { orderId = order.Id })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar transação");
            return Json(new { success = false, message = $"Erro: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET: /Web3Payment/Success/{orderId}
    /// Página de sucesso após pagamento
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Success(string orderId)
    {
        try
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            
            if (order == null)
            {
                TempData["Error"] = "Pedido não encontrado.";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new Web3SuccessViewModel
            {
                OrderId = order.Id,
                TransactionHash = order.BlockchainTxHash ?? "",
                Network = order.BlockchainNetwork ?? "Arc Testnet",
                ExplorerUrl = "https://testnet.arcscan.app",
                BlockNumber = order.BlockNumber,
                TotalAmount = order.TotalAmount,
                GasFee = order.GasFee,
                ConfirmedAt = order.BlockchainConfirmedAt,
                Items = order.Items
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar página de sucesso");
            TempData["Error"] = "Erro ao carregar detalhes do pedido.";
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// GET: /Web3Payment/CheckTransactionStatus
    /// Verifica o status de uma transação
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckTransactionStatus(string orderId)
    {
        try
        {
            var order = await _orderRepo.GetByIdAsync(orderId);
            
            if (order == null)
                return Json(new { success = false, message = "Pedido não encontrado" });

            return Json(new
            {
                success = true,
                status = order.Status,
                transactionHash = order.BlockchainTxHash,
                blockNumber = order.BlockNumber,
                confirmedAt = order.BlockchainConfirmedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status");
            return Json(new { success = false, message = "Erro ao verificar status" });
        }
    }
}