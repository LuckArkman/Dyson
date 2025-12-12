using System.Linq.Expressions;
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
    
    Task<ContractDocument?> SaveContractAsync(ContractDocument contract);
    Task<List<ContractDocument>> GetUserContractsAsync(string userId);
    Task<ContractDocument?> GetContractByAddressAsync(string contractAddress);
    Task<bool> UpdateContractStatusAsync(string contractAddress, string status);
    Task<ContractDocument?> GetContractByIdAsync(string contractId);
    Task UpdateAsync(string contractId, ContractDocument contract);
    Task<List<User>> GetAllAsync(CancellationToken none);
    Task UpdateUserAsync(User existingUser);
    
    Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> predicate);
    Task<T> GetByIdAsync(string id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(string id);
}