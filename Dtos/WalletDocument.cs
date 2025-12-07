using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class WalletDocument
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string userId { get; set; }

    [BsonElement("address")]
    public string Address { get; set; } 

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}