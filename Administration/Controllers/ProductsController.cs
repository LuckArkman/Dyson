using Dtos;
using Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Services;

namespace Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly AdminDataService _service;

    public ProductsController(AdminDataService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _service.GetAllProductsAsync();
        return View(products);
    }

    // GET: Products/Create
    public IActionResult Create()
    {
        var product = new Product
        {
            // Inicializa as listas para evitar NullReferenceException na View
            PricesCollection = new List<Prices>(),
            resourcesCollection = new List<Resources>()
        };

        // Pré-popula a lista de preços com base no Enum PriceType
        // Isso cria os inputs com valor 0.00 para cada tipo de plano automaticamente
        foreach (PriceType type in Enum.GetValues(typeof(PriceType)))
        {
            // Ignoramos 'None' e 'Custom' se não forem usados como planos padrão
            if (type != PriceType.None && type != PriceType.Custom)
            {
                product.PricesCollection.Add(new Prices
                {
                    id = Guid.NewGuid(),
                    PriceType = type,
                    Price = 0 // Valor inicial
                });
            }
        }

        return View(product);
    }

    // POST: Products/Create
    [HttpPost]
    public async Task<IActionResult> Create(Product product)
    {
        // 1. Garante que o ID do produto exista
        if (product.id == null) product.id = Guid.NewGuid();

        // 2. Lógica para limpar e validar Recursos Dinâmicos
        if (product.resourcesCollection != null)
        {
            // Remove itens que vieram com descrição vazia (ex: usuário clicou em adicionar mas não preencheu)
            product.resourcesCollection = product.resourcesCollection
                .Where(r => !string.IsNullOrWhiteSpace(r.description))
                .ToList();

            // Garante IDs para os recursos válidos
            foreach (var resource in product.resourcesCollection)
            {
                if (resource.id == null) resource.id = Guid.NewGuid();
            }
        }
        else
        {
            product.resourcesCollection = new List<Resources>();
        }

        // 3. Salva no MongoDB
        await _service.CreateProductAsync(product);
        return RedirectToAction(nameof(Index));
    }

    // GET: Products/Edit/{id}
    public async Task<IActionResult> Edit(Guid id)
    {
        var product = await _service.GetProductByIdAsync(id);
        if (product == null) return NotFound();

        // Proteção contra dados legados nulos
        if (product.PricesCollection == null) product.PricesCollection = new List<Prices>();
        if (product.resourcesCollection == null) product.resourcesCollection = new List<Resources>();

        // Verifica se faltam tipos de preço (caso novos tipos tenham sido adicionados ao Enum depois)
        // e adiciona campos zerados para eles, permitindo edição.
        foreach (PriceType type in Enum.GetValues(typeof(PriceType)))
        {
            if (type != PriceType.None && type != PriceType.Custom)
            {
                if (!product.PricesCollection.Any(p => p.PriceType == type))
                {
                    product.PricesCollection.Add(new Prices
                    {
                        id = Guid.NewGuid(),
                        PriceType = type,
                        Price = 0
                    });
                }
            }
        }

        // Ordena para manter consistência visual (Ex: Mensal, Trimestral, Anual...)
        product.PricesCollection = product.PricesCollection.OrderBy(p => p.PriceType).ToList();

        return View(product);
    }

    // POST: Products/Edit
    [HttpPost]
    public async Task<IActionResult> Edit(Product product)
    {
        // Mesma lógica de limpeza de recursos do Create
        if (product.resourcesCollection != null)
        {
            product.resourcesCollection = product.resourcesCollection
                .Where(r => !string.IsNullOrWhiteSpace(r.description))
                .ToList();

            foreach (var resource in product.resourcesCollection)
            {
                if (resource.id == null) resource.id = Guid.NewGuid();
            }
        }

        await _service.UpdateProductAsync(product);
        return RedirectToAction(nameof(Index));
    }
}