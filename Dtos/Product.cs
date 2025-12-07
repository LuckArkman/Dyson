using Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Product
{
    [BsonId]
    public Guid id { get; set; } = Guid.NewGuid();
    public string name { get; set; }
    public string description { get; set; }
    public ProductCategory category { get; set; } = ProductCategory.none;
    public ICollection<Resources> resourcesCollection { get; set; }
    public ICollection<Prices> PricesCollection { get; set; }
}