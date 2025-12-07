using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Controllers;

public class DashBoardController: Controller
{
    [Authorize]
    public IActionResult Index()
    {
        ViewData["Title"] = "DashBoard";
        return View();
    }
}