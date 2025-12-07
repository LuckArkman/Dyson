using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Services;

namespace Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly AuthService _authService;

    public SessionController(
        AuthService authService)
    {
        _authService = authService;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var token = await _authService.AuthenticateAsync(req.Email, req.Password);
        if (token == null)
        {
            Console.WriteLine($"{nameof(Login)} >> Credenciais inválidas. ! {req.Email}");
            return Ok(null);
        }

        Console.WriteLine($"{nameof(Login)} >> Usuario Logado com Sucesso ! {req.Email}");
        return Ok(new { token });
    }

}