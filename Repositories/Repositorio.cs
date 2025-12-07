using Data;
using Dtos;
using Interfaces;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Repositories;

public class Repositorio<T> : IRepositorio<T>
{
    private readonly IConfiguration _configuration;
    protected IMongoCollection<T> _collection;
    public string _collectionName { get; set; }
    private MongoDataController _db { get; set; }
    private IMongoDatabase _mongoDatabase { get; set; }

    public Repositorio(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    public void InitializeCollection(string connectionString,
        string databaseName,
        string collectionName)
    {
        _collectionName = collectionName;
        // Verifica se a conexão já foi estabelecida
        if (_collection != null) return;
        
        _db = new MongoDataController(connectionString, databaseName, _collectionName);
        _mongoDatabase = _db.GetDatabase();

        // O compilador usa o T da CLASSE Repositorio<T>, resolvendo o erro de conversão.
        _collection = _mongoDatabase.GetCollection<T>(_collectionName);
    }

    // --- Implementação dos Métodos da Interface (CRUD) ---

    public async Task<T> InsertOneAsync(T document)
    {
        _collection.InsertOne(document);
        return document;
    }
    
    public async Task<T> GetByIdAsync(string id, CancellationToken none)
    {
        if (_collection == null) throw new InvalidOperationException("A coleção não foi inicializada.");
        
        // Assume que o ID é mapeado para a propriedade padrão '_id'
        var filter = Builders<T>.Filter.Eq("_id", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T> GetByMailAsync(string email, CancellationToken none)
    {
        var filter = Builders<T>.Filter.Eq("Email", email);
        return await _collection.Find(filter).FirstOrDefaultAsync(none);
    }
    
    public async Task<List<T>> GetAllProductsAsync() => 
        await _collection.Find(_ => true).ToListAsync();

    public async Task<List<T>?> GetAllOrdersByUserAsync(string userId)
    {
        var filter = Builders<T>.Filter.Eq("userId", userId);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(Guid productId, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<Product>("Products");
        var filter = Builders<Product>.Filter.Eq(p => p.id, productId);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public Task UpdateOneAsync(string orderId, Order order)
    {
        throw new NotImplementedException();
    }

    public async Task<T?> GetUserByIdAsync(string id, CancellationToken cancellationToken)
    {

        var filter = Builders<T>.Filter.Eq("_id", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByMailAsync(string email, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<User>("Users");
        var filter = Builders<User>.Filter.Eq(u => u.Email, email);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<WalletDocument?> GetUserWalletAsync(string id, CancellationToken none)
    {
        var collection = _db.GetDatabase().GetCollection<WalletDocument>("Wallets");
        var filter = Builders<WalletDocument>.Filter.Eq(u => u.userId, id);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }
}