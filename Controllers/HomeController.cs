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

        // Send them straight to the official Identity Login page we just fixed!
        return LocalRedirect("/Identity/Account/Login");
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}

