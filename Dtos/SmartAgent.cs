using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class SmartAgent
{
    [BsonId]
    public string id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public string hashContract { get; set; } = "0x0000000000000000000000000000000000000000000000";
    public decimal price { get; set; } = 0.0m;
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
    public DateTime updatedAt { get; set; }
    
}