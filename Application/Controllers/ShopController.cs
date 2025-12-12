using System.Security.Claims;
using Akka.Util.Internal;
using Dtos;
using Enums;
using Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers; // Namespace ajustado

[Authorize]
public class ShopController : Controller
{
    private readonly IRepositorio<Product> _productRepo;
    private readonly IRepositorio<Order> _orderRepo;
    private readonly IRepositorio<OrderItem> _cartRepo;
    private readonly IConfiguration _configuration;
    private readonly CartService _cartService;
    
    // Carrinho em Memória (Static para persistir durante a execução do app)
    private static Dictionary<string, List<OrderItem>> _carts = new();

    public ShopController(
        IConfiguration configuration,
        IRepositorio<Product> productRepo,
        IRepositorio<Order> orderRepo,
        IRepositorio<OrderItem> cartRepo,
        CartService cartService)
    {
        _configuration = configuration;
        _productRepo = productRepo;
        _orderRepo = orderRepo;
        _cartRepo = cartRepo;
        _cartService = cartService;
        
        // Inicializações
        _productRepo.InitializeCollection(
            _configuration["MongoDbSettings:ConnectionString"], 
            _configuration["MongoDbSettings:DataBaseName"], "Products");
        _orderRepo.InitializeCollection(_configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "Orders");
        _cartRepo.InitializeCollection(_configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "Carts");
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    // --- CORREÇÃO DO ERRO 404 ---
    // Se o usuário acessar /Shop/AddToCart pela URL, redireciona para a vitrine
    [HttpGet]
    public IActionResult AddToCart()
    {
        if (_carts != null)
        {
            
        }
        return RedirectToAction("Pricing", "Pricing");
        
        
    }
    
    // Ação chamada pelo FORMULÁRIO (Botão)
    [HttpPost]
    public async Task<IActionResult> AddToCart(string productId, decimal price)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        Guid.TryParse(productId, out Guid id);
        var product = await _productRepo.GetProductByIdAsync(id, CancellationToken.None);

        if (product == null) return NotFound();

        // Lógica de Preço (Mantendo a correção que fizemos anteriormente)
        var selectedPriceObj = product.PricesCollection?.FirstOrDefault(p => p.Price == price);
        if (selectedPriceObj == null) selectedPriceObj = product.PricesCollection?.FirstOrDefault();
        if (selectedPriceObj == null) return BadRequest("Preço inválido.");

        // Cria o item
        var newItem = new OrderItem
        {
            ProductId = productId,
            ProductName = product.name,
            _priceType = selectedPriceObj.PriceType,
            Price = selectedPriceObj.Price,
            Quantity = 1
        };

        Guid.TryParse(userId, out var _id);
        await _cartService.AddItemAsync(_id, newItem);
        
        return RedirectToAction("Cart", "Shop");
    }

    [HttpPost]
    public IActionResult RemoveItem(string productId)
    {
        var userId = GetUserId();
        // --- MUDANÇA AQUI: Remove do CartStorage ---
        if (CartStorage.Carts.ContainsKey(userId))
        {
            CartStorage.Carts[userId].RemoveAll(x => x.ProductId == productId);
        }
        return RedirectToAction("Cart");
    }

    [HttpGet]
    public async Task<IActionResult> Cart()
    {
        var userId = GetUserId();
        Guid.TryParse(userId, out var _id);
        var cart = await _cartService.GetCartByUserIdAsync(_id);
        
        ViewBag.Qtd = cart.Items.Count;
        return View(cart.Items);
    }


    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    {
        var userId = GetUserId();
        if (!_carts.ContainsKey(userId) || !_carts[userId].Any())
            return BadRequest(new { success = false, message = "Carrinho vazio." });

        var cartItems = _carts[userId];
        var total = cartItems.Sum(x => x.Price * x.Quantity);

        // 1. Criar Pedido
        var order = new Order
        {
            id = Guid.NewGuid().ToString(),
            UserId = userId,
            Items = new List<OrderItem>(cartItems),
            TotalAmount = total,
            PaymentMethod = request.PaymentMethod,
            Status = "Pending", // "Pending", "Paid", "Canceled"
            CreatedAt = DateTime.UtcNow
        };

        await _orderRepo.InsertOneAsync(order);

        // 2. Limpar Carrinho
        _carts[userId].Clear();

        return Ok(new { success = true, message = "Compra realizada com sucesso!" });
    }
}

public class CheckoutRequest
{
    public string PaymentMethod { get; set; }
    public string CardName { get; set; }
    public string CardNumber { get; set; }
    public string CardExpiry { get; set; }
    public string CardCvv { get; set; }
}