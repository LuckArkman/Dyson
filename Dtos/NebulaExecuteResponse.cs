namespace Dtos;

public class NebulaExecuteResponse
{
    public string Message { get; set; }
    public List<NebulaAction> Actions { get; set; }
    public string SessionId { get; set; }
}