using System.Text.Json.Serialization;

namespace Dtos;

public class MercadoPagoWebhookRequest
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; } // MP as vezes manda 'topic' em vez de 'type' em versões antigas

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("api_version")]
    public string? ApiVersion { get; set; }

    [JsonPropertyName("data")]
    public WebhookData? Data { get; set; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; set; }
}

public class WebhookData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}