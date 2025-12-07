using Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Prices
{
    [BsonId]
    public Guid id { get; set; } = Guid.NewGuid();
    public PriceType PriceType { get; set; } = PriceType.None;
    public decimal Price { get; set; }
}