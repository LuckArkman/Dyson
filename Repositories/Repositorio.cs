using System.Linq.Expressions;
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
    
    /// <summary>
    /// Salva um novo contrato no MongoDB
    /// </summary>
    public async Task<ContractDocument> SaveContractAsync(ContractDocument contract)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        
        // Garantir que tem ID e timestamps
        if (string.IsNullOrEmpty(contract.Id))
            contract.Id = Guid.NewGuid().ToString();
        
        contract.CreatedAt = DateTime.UtcNow;
        contract.UpdatedAt = DateTime.UtcNow;

        await collection.InsertOneAsync(contract);
        return contract;
    }

    /// <summary>
    /// Retorna todos os contratos de um usuário
    /// </summary>
    public async Task<List<ContractDocument>> GetUserContractsAsync(string userId)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        var filter = Builders<ContractDocument>.Filter.Eq(c => c.WalletAddress, userId);
        
        return await collection
            .Find(filter)
            .SortByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Busca um contrato pelo endereço
    /// </summary>
    public async Task<ContractDocument> GetContractByAddressAsync(string contractAddress)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        var filter = Builders<ContractDocument>.Filter.Eq(c => c.ContractAddress, contractAddress);
        
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Atualiza o status de um contrato
    /// </summary>
    public async Task<bool> UpdateContractStatusAsync(string contractAddress, string status)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        
        var filter = Builders<ContractDocument>.Filter.Eq(c => c.ContractAddress, contractAddress);
        var update = Builders<ContractDocument>.Update
            .Set(c => c.Status, status)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<ContractDocument?> GetContractByIdAsync(string contractId)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        var filter = Builders<ContractDocument>.Filter.Eq(c => c.Id, contractId);
        
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(string contractId, ContractDocument contract)
    {
        var collection = _db.GetDatabase().GetCollection<ContractDocument>("Contracts");
        contract = await GetContractByIdAsync(contractId);
        
        var filter = Builders<ContractDocument>.Filter.Eq(c => c.Id, contractId);
        var update = Builders<ContractDocument>.Update
            .Set(c => c.Status, contract.Status)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        var result = await collection.UpdateOneAsync(filter, update);
        await Task.CompletedTask;
    }

    public Task<List<User>> GetAllAsync(CancellationToken none)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateUserAsync(User _User)
    {
        var collection = _db.GetDatabase().GetCollection<User>("Users");
         var user = await GetContractByIdAsync(_User.Id);
        
        var filter = Builders<User>.Filter.Eq(c => c.Id, _User.Id);
        var update = Builders<User>.Update
            .Set(c => c.UserName, _User.UserName)
            .Set(c => c.Email, _User.Email)
            .Set(c => c.PhoneNumber, _User.PhoneNumber)
            .Set(c => c.PasswordHash, _User.PasswordHash)
            .Set(c => c.PersonalName, _User.PersonalName)
            .Set(c => c.BackupDate, _User.BackupDate)
            .Set(c => c.LastWalletAuth, DateTime.UtcNow);
        var result = await collection.UpdateOneAsync(filter, update);
        await Task.CompletedTask;
    }
    
    public async Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> predicate)
    {
        return await _collection.Find(predicate).ToListAsync();
    }

    public async Task<T> GetByIdAsync(string id)
    {
        // Assume que a entidade tem um campo Id. 
        // Como T é genérico, filtramos usando Builders genéricos ou Reflection se necessário.
        // A maneira mais limpa com Driver Mongo moderno é Filter.Eq("Id", id)
        var filter = Builders<T>.Filter.Eq("Id", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _collection.InsertOneAsync(entity);
    }

    public async Task UpdateAsync(T entity)
    {
        // Precisamos pegar o ID da entidade via Reflection ou assumir uma interface
        var idProperty = typeof(T).GetProperty("Id");
        var idValue = idProperty?.GetValue(entity)?.ToString();

        if (idValue != null)
        {
            var filter = Builders<T>.Filter.Eq("Id", idValue);
            await _collection.ReplaceOneAsync(filter, entity);
        }
    }

    public async Task DeleteAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq("Id", id);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task<T?> GetAgentByIdAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq("_id", id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }
}