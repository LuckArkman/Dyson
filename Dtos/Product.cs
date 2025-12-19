using Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Product
{
    [BsonId]
    public Guid id { get; set; } = Guid.NewGuid();
    
    [BsonElement("name")]
    public string name { get; set; }
    
    [BsonElement("description")]
    public string description { get; set; }
    
    [BsonElement("category")]
    public ProductCategory category { get; set; } = ProductCategory.none;
    
    [BsonElement("resourcesCollection")]
    public ICollection<Resources> resourcesCollection { get; set; }
    
    [BsonElement("PricesCollection")]
    public ICollection<Prices> PricesCollection { get; set; }
}