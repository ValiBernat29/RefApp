using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

/// <summary>
/// Serves the Vue single-page app. Every URL under /app returns the same HTML shell and the
/// client-side router decides what to show. Authentication is required (anonymous visitors are sent
/// to the Identity login page); role checks happen on the API and in the router.
/// </summary>
[Authorize]
public class SpaController : Controller
{
    [HttpGet("app")]
    [HttpGet("app/{**path}")]
    public IActionResult Index() => View();
}
