namespace Dtos;

public class AIMessage
{
    public string Role { get; set; } // "user", "assistant", "system"
    public string Content { get; set; }
}