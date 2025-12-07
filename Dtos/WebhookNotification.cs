namespace Dtos;

public class WebhookNotification
{
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}