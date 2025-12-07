using MongoDB.Bson.Serialization.Attributes;

namespace Dtos;

public class Message
{
    [BsonElement("sender")]
    public string Sender { get; set; } = string.Empty; // "user" ou "ai"

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}