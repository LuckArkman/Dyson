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

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: Exibe a página de Registro
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                //return RedirectToAction("Index", "Home");
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
                    return RedirectToAction("Login", "Account");
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception)
                {
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
                //return RedirectToAction("Index", "DashBoard");
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
                    return RedirectToAction("Index", "Profile");
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

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) // Cookie expira em 24h
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }