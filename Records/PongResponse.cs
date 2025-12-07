using System.Text.Json.Serialization;

namespace Records;

public record PongResponse : _Message
{
    public string content { get; set; } 
    
    [JsonConstructor] 
    public PongResponse(Guid correlationId, string content) : base(correlationId) 
    {
        this.content = content;
    }
}