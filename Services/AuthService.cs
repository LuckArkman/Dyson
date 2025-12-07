using System.Security.Cryptography;
using System.Text;
using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Services;

public class AuthService
{
    private readonly IRepositorio<User> _repositorio;
    private readonly TokenService _tokenService;
    private readonly SessionService _sessionService; 
    private readonly IConfiguration _cfg;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IRepositorio<User> repositorio,
        TokenService tokenService,
        SessionService sessionService,
        IConfiguration cfg,
        IPasswordHasher<User> passwordHasher)
    {
        _repositorio = repositorio;
        _tokenService = tokenService;
        _sessionService = sessionService;
        _cfg = cfg;
        _passwordHasher = passwordHasher;

        _repositorio.InitializeCollection(_cfg["MongoDbSettings:ConnectionString"],
            _cfg["MongoDbSettings:DataBaseName"],
            "Users");
    }

    // ======================================================
    // LOGIN
    // ======================================================
    public async Task<TokenRequest?> AuthenticateAsync(string email, string password)
    {
        var user = await _repositorio.GetUserByMailAsync(
            email: email,
            none: CancellationToken.None);
        if (user == null) return null;
        Console.WriteLine($"{nameof(AuthenticateAsync)} >> {user == null}");
        var result = VerifyPassword(password, user.PasswordHash);

        if (result)
        {
            var token = _tokenService.GenerateToken(user);
            await _sessionService.SetAsync(token, user);
            return new TokenRequest
            {
                token = token
            };
        }

        return null;
    }
    
    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    private bool VerifyPassword(string enteredPassword, string storedHash)
    {
        return HashPassword(enteredPassword).Equals(storedHash);
    }

    // ======================================================
    // GET ACCOUNT
    // ======================================================
    public async Task<object> GetAccount(Guid userId)
    {
        var user = await _repositorio.GetUserByIdAsync(
            id: userId.ToString(),
            none: CancellationToken.None);
        return user ?? null;
    }
}