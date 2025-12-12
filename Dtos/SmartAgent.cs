using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class SmartAgent
{
    [BsonId]
    public string id { get; set; } =  Guid.NewGuid().ToString();

    public string Name { get; set; }
    public string Description { get; set; }
    public string userId { get; set; } // ID do Usuário (Dono)
    
    public bool IsPublic { get; set; } = false;
    public string Category { get; set; }
    public int Downloads { get; set; }

    
    public WorkflowData Workflow { get; set; } = new();

    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public decimal price { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
}