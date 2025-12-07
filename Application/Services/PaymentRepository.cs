using Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Services;

/// <summary>
/// Repositório para gerenciar pagamentos no MongoDB
/// </summary>
public class PaymentRepository
{
    private readonly IMongoCollection<PaymentDocument> _payments;
    private readonly ILogger<PaymentRepository> _logger;

    public PaymentRepository(
        IConfiguration configuration,
        ILogger<PaymentRepository> logger)
    {
        _logger = logger;

        var connectionString = configuration["MongoDbSettings:ConnectionString"];
        var databaseName = configuration["MongoDbSettings:DatabaseName"];

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _payments = database.GetCollection<PaymentDocument>("Payments");

        // Cria índices
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            // Índice no mercadoPagoPaymentId
            _payments.Indexes.CreateOne(
                new CreateIndexModel<PaymentDocument>(
                    Builders<PaymentDocument>.IndexKeys.Ascending(p => p.MercadoPagoPaymentId),
                    new CreateIndexOptions { Unique = true, Sparse = true }
                )
            );

            // Índice no orderId
            _payments.Indexes.CreateOne(
                new CreateIndexModel<PaymentDocument>(
                    Builders<PaymentDocument>.IndexKeys.Ascending(p => p.OrderId)
                )
            );

            // Índice no userId
            _payments.Indexes.CreateOne(
                new CreateIndexModel<PaymentDocument>(
                    Builders<PaymentDocument>.IndexKeys.Ascending(p => p.UserId)
                )
            );

            // Índice no status
            _payments.Indexes.CreateOne(
                new CreateIndexModel<PaymentDocument>(
                    Builders<PaymentDocument>.IndexKeys.Ascending(p => p.Status)
                )
            );

            // Índice composto: userId + createdAt (para histórico)
            _payments.Indexes.CreateOne(
                new CreateIndexModel<PaymentDocument>(
                    Builders<PaymentDocument>.IndexKeys
                        .Ascending(p => p.UserId)
                        .Descending(p => p.CreatedAt)
                )
            );

            _logger.LogInformation("Índices da coleção Payments criados com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar índices da coleção Payments");
        }
    }

    public async Task<PaymentDocument> InsertAsync(PaymentDocument payment)
    {
        await _payments.InsertOneAsync(payment);
        return payment;
    }

    public async Task<PaymentDocument?> GetByIdAsync(string id)
    {
        return await _payments.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<PaymentDocument?> GetByMercadoPagoIdAsync(long mercadoPagoId)
    {
        return await _payments.Find(p => p.MercadoPagoPaymentId == mercadoPagoId).FirstOrDefaultAsync();
    }

    public async Task<PaymentDocument?> GetByOrderIdAsync(string orderId)
    {
        return await _payments.Find(p => p.OrderId == orderId).FirstOrDefaultAsync();
    }

    public async Task<List<PaymentDocument>> GetByUserIdAsync(string userId, int limit = 50)
    {
        return await _payments.Find(p => p.UserId == userId)
            .SortByDescending(p => p.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<bool> UpdateStatusAsync(string id, string status, string? statusDetail = null)
    {
        var update = Builders<PaymentDocument>.Update
            .Set(p => p.Status, status)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        if (statusDetail != null)
        {
            update = update.Set(p => p.StatusDetail, statusDetail);
        }

        if (status == "approved")
        {
            update = update.Set(p => p.ApprovedAt, DateTime.UtcNow);
        }

        var result = await _payments.UpdateOneAsync(p => p.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddWebhookNotificationAsync(string id, WebhookNotification notification)
    {
        var update = Builders<PaymentDocument>.Update
            .Push(p => p.WebhookNotifications, notification)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var result = await _payments.UpdateOneAsync(p => p.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<List<PaymentDocument>> GetPendingPaymentsAsync()
    {
        return await _payments.Find(p => p.Status == "pending")
            .SortBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PaymentDocument>> GetExpiredPixPaymentsAsync()
    {
        var now = DateTime.UtcNow;
        return await _payments.Find(p => 
            p.PaymentMethod == "pix" && 
            p.Status == "pending" && 
            p.PixExpirationDate != null && 
            p.PixExpirationDate < now)
            .ToListAsync();
    }
}