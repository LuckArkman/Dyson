using Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly AdminDataService _service;

    public AdminController(AdminDataService service)
    {
        _service = service;
    }

    // Cadastro de Novo Admin
    public IActionResult CreateUser() => View();

    [HttpPost]
    public async Task<IActionResult> CreateUser(string username, string email, string password)
    {
        try
        {
            await _service.CreateAdminAsync(username, email, password);
            TempData["Success"] = "Administrador criado com sucesso!";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View();
        }
    }
    
    public async Task<IActionResult> Orders()
    {
        // 1. Busca a lista completa (como já fazia)
        var ordersList = await _service.GetAllOrdersAsync();

        // 2. Busca dados para o Gráfico de Barras (Últimos 12 meses)
        var barChartData = await _service.GetMonthlySalesVolumeAsync();

        // 3. Busca dados para o Gráfico de Pizza (Mês Atual)
        var pieChartData = await _service.GetProductSalesDistributionAsync();

        // 4. Monta o ViewModel
        var viewModel = new OrdersViewModel
        {
            Orders = ordersList,
            MonthlySales = barChartData,
            ProductDistribution = pieChartData
        };

        return View(viewModel);
    }
}