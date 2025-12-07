using Dtos;
using MongoDB.Driver;

namespace Interfaces;

public interface IRepositorio<T> 
{
    Task<T> InsertOneAsync(T document);
    Task<T> GetByIdAsync(string id, CancellationToken none);
    void InitializeCollection(string connectionString, string databaseName, string collectionName);
    Task<T> GetByMailAsync(string email, CancellationToken none);
    Task<List<T>?> GetAllProductsAsync();
    Task<List<T>?> GetAllOrdersByUserAsync(string userId);
    Task<Product?> GetProductByIdAsync(Guid productId, CancellationToken none);
    Task UpdateOneAsync(string orderId, Order order);
    Task<T?> GetUserByIdAsync(string id, CancellationToken none);
    Task<User?> GetUserByMailAsync(string email, CancellationToken none);
    Task<WalletDocument?> GetUserWalletAsync(string id, CancellationToken none);
}