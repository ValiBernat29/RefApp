using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

public class HomeController : Controller
{
    /// <summary>Signed-in users go to the Vue app (which picks their dashboard); everyone else to the Identity login page.</summary>
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/app");

        return LocalRedirect("/Identity/Account/Login");
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
