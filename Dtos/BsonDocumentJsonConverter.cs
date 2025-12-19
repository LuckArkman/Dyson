using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Dtos;

public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
{
    public override BsonDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var json = doc.RootElement.GetRawText();
        return BsonDocument.Parse(json);
    }

    public override void Write(Utf8JsonWriter writer, BsonDocument value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value.ToJson());
    }
}