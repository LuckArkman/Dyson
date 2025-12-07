using Dtos;

namespace Services;

public static class CartStorage
{
    // Armazena os carrinhos de todos os usuários em memória
    // Chave: UserId, Valor: Lista de Itens
    public static Dictionary<string, List<OrderItem>> Carts { get; } = new();
}