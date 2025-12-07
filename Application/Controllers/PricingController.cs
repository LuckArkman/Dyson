using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Controllers;

public class PricingController: Controller
{
    private readonly IRepositorio<Product> _service;
    private readonly IConfiguration _configuration;
    public PricingController(IRepositorio<Product> service,
        IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
        _service.InitializeCollection(_configuration["MongoDbSettings:ConnectionString"],
            _configuration["MongoDbSettings:DataBaseName"],
            "Products");
    }
    [HttpGet]
    public async Task<IActionResult> Pricing()
    {
        var products = await _service.GetAllProductsAsync();
        return View(products);
    }
}