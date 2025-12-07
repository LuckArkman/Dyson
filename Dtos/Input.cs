using System.Text.Json.Serialization;

namespace Dtos;

public class Input
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }
}