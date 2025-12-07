using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Records;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatController;
    public ChatController(IChatService chatController)
    {
        _chatController = chatController;
    }
    
    [HttpPost("Respond")] 
    public async Task<IActionResult> OnResponse([FromBody] Input input)
    {
        Console.WriteLine($"{nameof(OnResponse)} >> {input.Text}");
        if (string.IsNullOrWhiteSpace(input.Text)) return BadRequest("O texto de entrada não pode estar vazio.");

        var response = await _chatController.GenerateMessage(new HelloRequest(input.Text));
        // return Ok(input.Content);
        return Ok(response);
    }
    [HttpPost("Input")] 
    public async Task<IActionResult> OnInputResponse([FromBody] PongResponse input)
    {
        Console.WriteLine($"{nameof(OnResponse)} >> {input.content}");
        if (string.IsNullOrWhiteSpace(input.content)) return BadRequest("O texto de entrada não pode estar vazio.");
        return Ok(input.content);
    }
}