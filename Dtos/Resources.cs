using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Resources
{
    [BsonId] 
    public Guid id { get; set; } = Guid.NewGuid();
    public string description { get; set; }
    public bool active { get; set; }
    
}