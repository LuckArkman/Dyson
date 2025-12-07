namespace Dtos;

public class OrdersViewModel
{
    // Lista completa de ordens para a tabela
    public List<Order> Orders { get; set; } = new List<Order>();

    // Dados para o Gráfico de Barras (Vendas por Mês)
    public List<MonthlySalesData> MonthlySales { get; set; } = new List<MonthlySalesData>();

    // Dados para o Gráfico de Pizza (Distribuição por Produto)
    public List<ProductSalesData> ProductDistribution { get; set; } = new List<ProductSalesData>();
}