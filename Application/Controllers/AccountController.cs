using System.Security.Claims;
using Application.Models;
using Dtos;
using Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Models;

namespace Controllers;

public class AccountController : Controller
{
    private readonly IUserService _userService;
    private readonly IWeb3AuthService _web3AuthService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IUserService userService, 
        IWeb3AuthService web3AuthService,
        ILogger<AccountController> logger)
    {
        _userService = userService;
        _web3AuthService = web3AuthService;
        _logger = logger;
    }

    #region Traditional Authentication

    // GET: Exibe a página de Registro
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("profile", "Profile");
        }
        return View();
    }

    // POST: Processa o Registro
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var user = await _userService.Register(model);
                await Authenticate(user);
                return RedirectToAction("profile", "Profile");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro inesperado durante o registro.");
            }
        }

        return View(model);
    }

    // GET: Exibe a página de Login
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("profile", "Profile");
        }
        return View();
    }

    // POST: Processa o Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userService.Login(model);

            if (user != null)
            {
                await Authenticate(user);
                return RedirectToAction("profile", "Profile");
            }
            
            ModelState.AddModelError(string.Empty, "Credenciais inválidas. Verifique seu e-mail e senha.");
        }

        return View(model);
    }

    // POST: Faz o Logout
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    #endregion

    #region Web3 Authentication (MetaMask)

    /// <summary>
    /// Gera mensagem para o usuário assinar com MetaMask
    /// POST: /Account/GetWeb3SignatureMessage
    /// </summary>
    [HttpPost]
    public IActionResult GetWeb3SignatureMessage([FromBody] Web3SignatureRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WalletAddress))
            {
                return BadRequest(new { message = "Endereço da carteira é obrigatório" });
            }

            if (!_web3AuthService.IsValidEthereumAddress(request.WalletAddress))
            {
                return BadRequest(new { message = "Endereço da carteira inválido" });
            }

            var message = _web3AuthService.GenerateSignatureMessage(
                request.WalletAddress, 
                request.Action ?? "login");

            var response = new Web3SignatureResponse
            {
                Message = message,
                WalletAddress = request.WalletAddress,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating signature message");
            return StatusCode(500, new { message = "Erro ao gerar mensagem de assinatura" });
        }
    }

        /// <summary>
    /// NOVO: Autentica usuário via MetaMask/Web3
    /// POST: /Account/Web3Login
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Web3Login([FromBody] Web3AuthRequest request)
    {
        try
        {
            _logger.LogInformation("Web3Login: Starting authentication for wallet {Wallet}", 
                request?.WalletAddress);

            // Validação de dados
            if (string.IsNullOrWhiteSpace(request?.WalletAddress) ||
                string.IsNullOrWhiteSpace(request?.Signature) ||
                string.IsNullOrWhiteSpace(request?.Message))
            {
                _logger.LogWarning("Web3Login: Incomplete data received");
                return BadRequest(new { 
                    success = false, 
                    message = "Dados incompletos" 
                });
            }

            // Autentica ou cria usuário
            var user = await _web3AuthService.AuthenticateWithWallet(
                request.WalletAddress, 
                request.Signature, 
                request.Message);

            if (user == null)
            {
                _logger.LogWarning("Web3Login: Authentication failed for wallet {Wallet}", 
                    request.WalletAddress);
                return Unauthorized(new { 
                    success = false, 
                    message = "Falha na autenticação" 
                });
            }

            // Define se é novo usuário (criado recentemente)
            var isNewUser = user.CreatedAt.HasValue && 
                            user.CreatedAt.Value > DateTime.UtcNow.AddMinutes(-1);

            // CRÍTICO: Autentica no sistema (cria cookie)
            await Authenticate(user);

            // Adiciona claim da carteira
            if (!string.IsNullOrEmpty(user.WalletAddress))
            {
                await AddWalletClaim(user.WalletAddress);
            }

            // Prepara resposta JSON
            var response = new Web3AuthResponse
            {
                Success = true,
                Message = isNewUser ? "Conta criada com sucesso!" : "Login realizado com sucesso!",
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                WalletAddress = user.WalletAddress,
                IsNewUser = isNewUser,
                RedirectUrl = Url.Action("profile", "Profile") // URL para redirect
            };

            _logger.LogInformation("Web3Login: Success for wallet {Wallet}. IsNew: {IsNew}. RedirectUrl: {Url}", 
                request.WalletAddress, isNewUser, response.RedirectUrl);

            // CRÍTICO: Retorna JSON (não RedirectToAction!)
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Web3Login: Unauthorized attempt - {Message}", ex.Message);
            return Unauthorized(new { 
                success = false, 
                message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web3Login: Error during authentication");
            return StatusCode(500, new { 
                success = false, 
                message = "Erro ao autenticar com MetaMask" 
            });
        }
    }

    /// <summary>
    /// Adiciona claim da carteira a uma sessão já autenticada
    /// </summary>
    private async Task AddWalletClaim(string walletAddress)
    {
        var identity = User.Identity as ClaimsIdentity;
        if (identity != null)
        {
            // Remove claim antiga se existir
            var existingClaim = identity.FindFirst("WalletAddress");
            if (existingClaim != null)
            {
                identity.RemoveClaim(existingClaim);
            }

            // Adiciona nova claim
            identity.AddClaim(new Claim("WalletAddress", walletAddress));

            // Atualiza o cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }
    }

    /// <summary>
    /// Vincula carteira MetaMask a conta existente
    /// POST: /Account/LinkWallet
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> LinkWallet([FromBody] LinkWalletRequest request)
    {
        try
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "Usuário não autenticado" });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { message = "ID do usuário não encontrado" });
            }

            var success = await _web3AuthService.LinkWalletToUser(
                userId,
                request.WalletAddress,
                request.Signature,
                request.Message);

            if (!success)
            {
                return BadRequest(new { message = "Falha ao vincular carteira" });
            }

            var response = new LinkWalletResponse
            {
                Success = true,
                Message = "Carteira vinculada com sucesso!",
                WalletAddress = request.WalletAddress
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking wallet");
            return StatusCode(500, new { message = "Erro ao vincular carteira" });
        }
    }

    /// <summary>
    /// Verifica se uma carteira já está registrada
    /// GET: /Account/CheckWallet/{address}
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckWallet(string address)
    {
        try
        {
            if (!_web3AuthService.IsValidEthereumAddress(address))
            {
                return BadRequest(new { message = "Endereço inválido" });
            }

            var user = await _web3AuthService.GetUserByWalletAddress(address);
            
            return Ok(new 
            { 
                exists = user != null,
                isLinked = user != null && !string.IsNullOrEmpty(user.WalletAddress)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking wallet");
            return StatusCode(500, new { message = "Erro ao verificar carteira" });
        }
    }

    #endregion

    #region Helpers

    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Acesso Negado";
        return View();
    }
    
    private async Task Authenticate(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
        };

        // Adiciona claim da carteira se existir
        if (!string.IsNullOrEmpty(user.WalletAddress))
        {
            claims.Add(new Claim("WalletAddress", user.WalletAddress));
        }

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    #endregion
}