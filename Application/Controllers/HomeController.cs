using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Application.Models;
using Microsoft.AspNetCore.Authorization;

namespace Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        
        public IActionResult Index()
        {
            return View();
        }
        
        public IActionResult SmartContracts()
        {
            return View();
        }
        
        public IActionResult SmartWallets()
        {
            return View();
        }
        
        public IActionResult SmartNodes()
        {
            return View();
        }
        
        [Authorize]
        public IActionResult Profile()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}