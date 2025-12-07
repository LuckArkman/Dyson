using Dtos;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Services;

public class CartService
{
    private readonly IMongoCollection<UserCart> _carts;

    public CartService(IConfiguration configuration)
    {
        // Conexão direta com o Mongo, similar aos seus Repositorios
        var client = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
        var database = client.GetDatabase(configuration["MongoDbSettings:DataBaseName"]);
        _carts = database.GetCollection<UserCart>("Carts");
    }

    // Busca o carrinho do usuário. Se não existir, retorna um vazio (sem salvar ainda)
    public async Task<UserCart> GetCartByUserIdAsync(Guid userId)
    {
        var cart = await _carts.Find(c => c.UserId == userId).FirstOrDefaultAsync();
        return cart ?? new UserCart { UserId = userId, Items = new List<OrderItem>() };
    }

    // Adiciona ou atualiza um item e SALVA no banco
    public async Task AddItemAsync(Guid userId, OrderItem newItem)
    {
        var cart = await GetCartByUserIdAsync(userId);

        var existingItem = cart.Items.FirstOrDefault(x => x.ProductId == newItem.ProductId);

        if (existingItem == null)
        {
            cart.Items.Add(newItem);
        }
        else
        {
            // Atualiza preço e tipo se já existir
            existingItem.Price = newItem.Price;
            existingItem._priceType = newItem._priceType;
            // Opcional: existingItem.Quantity += newItem.Quantity; 
        }

        cart.LastUpdated = DateTime.UtcNow;

        // Upsert: Se existe atualiza, se não existe cria
        await _carts.ReplaceOneAsync(
            c => c.UserId == userId, 
            cart, 
            new ReplaceOptions { IsUpsert = true }
        );
    }

    // Remove item e atualiza o banco
    public async Task RemoveItemAsync(Guid userId, string productId)
    {
        var cart = await GetCartByUserIdAsync(userId);
        
        var itemToRemove = cart.Items.FirstOrDefault(x => x.ProductId == productId);
        if (itemToRemove != null)
        {
            cart.Items.Remove(itemToRemove);
            cart.LastUpdated = DateTime.UtcNow;
            
            await _carts.ReplaceOneAsync(c => c.UserId == userId, cart);
        }
    }

    // Limpa o carrinho (usado após checkout)
    public async Task ClearCartAsync(Guid userId)
    {
        await _carts.DeleteOneAsync(c => c.UserId == userId);
    }
}