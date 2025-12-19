using Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Prices
{
    [BsonId]
    [BsonElement("_id")]
    public Guid id { get; set; } = Guid.NewGuid();
    [BsonElement("PriceType")]
    [BsonRepresentation(BsonType.Int32)]  // ← This maps: 1 = Monthly, 2 = Quarterly, etc.
    public PriceType PriceType { get; set; } = PriceType.None;
    [BsonElement("Price")]
    public decimal Price { get; set; }
}