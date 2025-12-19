using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class ContractEvent
{
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("eventType")]
    public string EventType { get; set; }

    [BsonElement("description")]
    public string Description { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}