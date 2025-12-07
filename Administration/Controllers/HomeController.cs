using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Services;
using Dtos;

namespace Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AdminDataService _service;

    public HomeController(AdminDataService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var viewModel = await _service.GetDashboardStatsAsync();
        return View(viewModel);
    }
}