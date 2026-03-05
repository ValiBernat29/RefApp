using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Board")) return RedirectToAction("Index", "Board");
            if (User.IsInRole("Referee")) return RedirectToAction("Index", "Referee");
        }
        return View();
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}

