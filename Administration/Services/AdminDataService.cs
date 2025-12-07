using Dtos;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Data;
using MongoDB.Driver.Linq;
using DashboardViewModel = Models.DashboardViewModel;

namespace Services;

public class AdminDataService
{
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<AdminUser> _admins;
    private readonly IMongoCollection<Order> _orders;
    private readonly IConfiguration _configuration;
    

    public AdminDataService(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        var database = client.GetDatabase(settings.Value.DatabaseName);

        _products = database.GetCollection<Product>("Products");
        _admins = database.GetCollection<AdminUser>("Administrators");
        _orders = database.GetCollection<Order>("Orders");
    }

    // --- PRODUTOS ---
    public async Task<List<Product>> GetAllProductsAsync() => 
        await _products.Find(_ => true).ToListAsync();
    
    public async Task<List<MonthlySalesData>> GetMonthlySalesVolumeAsync()
    {
        var oneYearAgo = DateTime.UtcNow.AddMonths(-11).Date; // Pega os últimos 12 meses
        oneYearAgo = new DateTime(oneYearAgo.Year, oneYearAgo.Month, 1); // Começo do mês

        // Agregação via LINQ do Mongo Driver
        var query = _orders.AsQueryable()
            .Where(o => o.CreatedAt >= oneYearAgo)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new MonthlySalesData
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
                // Nota: A formatação da string será feita na memória depois, 
                // pois o LINQ to Mongo tem limitações com ToString() de data
                MonthLabel = "" 
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month);

        var result = await query.ToListAsync();

        // Formata os labels (Ex: "11/2024") no lado do C#
        foreach (var item in result)
        {
            item.MonthLabel = $"{item.Month:D2}/{item.Year}";
        }

        return result;
    }
    
    public async Task<List<ProductSalesData>> GetProductSalesDistributionAsync()
    {
        // Filtra para pegar apenas vendas do Mês Atual (conforme solicitado na lógica de "fatias")
        // Se quiser de todo o período, basta remover o filtro Where.
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        
        var query = _orders.AsQueryable()
            .Where(o => o.CreatedAt >= startOfMonth)
            .GroupBy(o => o.Items.First().ProductName)
            .Select(g => new ProductSalesData
            {
                ProductName = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count); // Mais vendidos primeiro

        return await query.ToListAsync();
    }

    public async Task<Product> GetProductByIdAsync(Guid id) => 
        await _products.Find(p => p.id == id).FirstOrDefaultAsync();
    
    public async Task<DashboardViewModel> GetDashboardStatsAsync()
    {
        // Executa as contagens em paralelo para performance
        var productsTask = _products.CountDocumentsAsync(_ => true);
        var ordersTask = _orders.CountDocumentsAsync(_ => true);
        var adminsTask = _admins.CountDocumentsAsync(_ => true);
    
        // Busca as 5 últimas ordens
        var recentOrdersTask = _orders.Find(_ => true)
            .SortByDescending(o => o.CreatedAt)
            .Limit(5)
            .ToListAsync();

        await Task.WhenAll(productsTask, ordersTask, adminsTask, recentOrdersTask);

        return new DashboardViewModel
        {
            TotalProducts = productsTask.Result,
            TotalOrders = ordersTask.Result,
            TotalAdmins = adminsTask.Result,
            RecentOrders = recentOrdersTask.Result
        };
    }

    public async Task CreateProductAsync(Product product)
    {
        if (product.id == null) product.id = Guid.NewGuid();
        // Garante inicialização das listas se nulas
        product.resourcesCollection ??= new List<Resources>();
        product.PricesCollection ??= new List<Prices>();
        await _products.InsertOneAsync(product);
    }

    public async Task UpdateProductAsync(Product product) =>
        await _products.ReplaceOneAsync(p => p.id == product.id, product);

    public async Task DeleteProductAsync(Guid id) =>
        await _products.DeleteOneAsync(p => p.id == id);

    // --- ORDENS ---
    public async Task<List<Order>> GetAllOrdersAsync() =>
        await _orders.Find(_ => true).SortByDescending(o => o.CreatedAt).ToListAsync();

    // --- ADMINISTRAÇÃO ---
    public async Task<AdminUser?> AuthenticateAsync(string mail, string password)
    {
        var admin = await _admins.Find(a => a.Email == mail).FirstOrDefaultAsync();
        if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
            return null;
        
        return admin;
    }

    public async Task CreateAdminAsync(string username, string email, string password)
    {
        var exists = await _admins.Find(a => a.Username == username).AnyAsync();
        if (exists) throw new Exception("Usuário já existe.");

        var admin = new AdminUser
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        await _admins.InsertOneAsync(admin);
    }
}