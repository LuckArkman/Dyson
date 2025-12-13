using MongoDB.Bson;

namespace Dtos;

public class WorkflowNode
{
    public string Id { get; set; }
    public string Name { get; set; } // Nome técnico (webhook, httpRequest)
    public string Type { get; set; } // Classe C# (MyClone.HttpRequest)
    public Position Position { get; set; }
    
    // Usamos object ou JsonNode para ser flexível com o MongoDB
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
}