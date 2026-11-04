using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

/// <summary>Keeps old Razor URLs working (bookmarks, emailed links) by sending them to the Vue app.</summary>
public class LegacyRedirectController : Controller
{
    [HttpGet("RouteMap")]
    public IActionResult RouteMap() => Redirect("/app/route-map");

    [HttpGet("Teams")]
    public IActionResult Teams() => Redirect("/app/teams");

    [HttpGet("Teams/CreateTeam")]
    public IActionResult CreateTeam() => Redirect("/app/teams/new");

    [HttpGet("Teams/EditTeam/{id:int}")]
    public IActionResult EditTeam(int id) => Redirect($"/app/teams/{id}/edit");
}
