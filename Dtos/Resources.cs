using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Resources
{
    [BsonId]
    [BsonElement("_id")]
    public Guid id { get; set; } = Guid.NewGuid();
    
    [BsonElement("description")]
    public string description { get; set; }
    
    [BsonElement("active")]
    public bool active { get; set; }
}