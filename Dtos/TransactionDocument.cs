using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class TransactionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("fromAddress")]
    public string fromAddress { get; set; } = null!;

    [BsonElement("toAddress")]
    public string toAddress { get; set; } = null!;

    [BsonElement("amount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal amount { get; set; }

    [BsonElement("timestamp")]
    public DateTime timestamp { get; set; }

    [BsonElement("notes")]
    public string? notes { get; set; } = null!;

    [BsonElement("hash")]
    public string hash { get; set; } = null!;

    public string type { get; set; }
    public string status { get; set; }
    public string blockchainNetwork { get; set; }
    public string contractAddress { get; set; }
}