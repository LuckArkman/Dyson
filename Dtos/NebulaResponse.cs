namespace Dtos;

public class NebulaResponse
{
    public string Message { get; set; }
    public string SessionId { get; set; }
    public Dictionary<string, object> Data { get; set; }
}