using Dtos;

namespace Interfaces;

/// <summary>
/// Métodos de extensão para IRepositorio com Order
/// Adicione estes métodos à sua implementação de IRepositorio
/// </summary>
public static class OrderRepositoryExtensions
{
    /// <summary>
    /// Busca ordem por ID - método helper
    /// </summary>
    public static async Task<Order?> GetByIdAsync(this IRepositorio<Order> repository, string orderId)
    {
        // Este método deve ser implementado na sua classe de repositório
        // Esta é apenas uma interface de exemplo
        throw new NotImplementedException("Implemente GetByIdAsync no seu repositório");
    }

    /// <summary>
    /// Atualiza uma ordem - método helper
    /// </summary>
    public static async Task UpdateAsync(this IRepositorio<Order> repository, string orderId, Order order)
    {
        // Este método deve ser implementado na sua classe de repositório
        throw new NotImplementedException("Implemente UpdateAsync no seu repositório");
    }

    /// <summary>
    /// Insere uma nova ordem - método helper
    /// </summary>
    public static async Task InsertOneAsync(this IRepositorio<Order> repository, Order order)
    {
        // Este método deve ser implementado na sua classe de repositório
        throw new NotImplementedException("Implemente InsertOneAsync no seu repositório");
    }
}