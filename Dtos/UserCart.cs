using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class UserCart
{
    [BsonId] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}