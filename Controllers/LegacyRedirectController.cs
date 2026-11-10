using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

/// <summary>
/// The app used to be server-rendered Razor pages with URLs like /Board and /Teams/EditTeam/3.
/// Everything now lives in the Vue app under /app, so these old URLs (bookmarks, emailed links)
/// redirect to the page that replaced them.
/// </summary>
public class LegacyRedirectController : Controller
{
    // Board
    [HttpGet("Board")] public IActionResult Board() => Redirect("/app/dashboard");
    [HttpGet("Allocation")] public IActionResult Allocation() => Redirect("/app/allocation");
    [HttpGet("Unavailability")] public IActionResult Unavailability() => Redirect("/app/unavailability");
    [HttpGet("RouteMap")] public IActionResult RouteMap() => Redirect("/app/route-map");

    [HttpGet("Matches")] public IActionResult Matches() => Redirect("/app/matches");
    [HttpGet("Matches/CreateMatch")] public IActionResult CreateMatch() => Redirect("/app/matches/new");
    [HttpGet("Matches/EditMatch/{id:int}")] public IActionResult EditMatch(int id) => Redirect($"/app/matches/{id}/edit");
    [HttpGet("Matches/Assign/{id:int}")] public IActionResult Assign(int id) => Redirect($"/app/matches/{id}/assign");

    [HttpGet("Teams")] public IActionResult Teams() => Redirect("/app/teams");
    [HttpGet("Teams/CreateTeam")] public IActionResult CreateTeam() => Redirect("/app/teams/new");
    [HttpGet("Teams/EditTeam/{id:int}")] public IActionResult EditTeam(int id) => Redirect($"/app/teams/{id}/edit");

    [HttpGet("Users")] public IActionResult Users() => Redirect("/app/users");
    [HttpGet("Users/RefereeFleet")] public IActionResult RefereeFleet() => Redirect("/app/users/fleet");
    [HttpGet("Users/EditUser/{id}")] public IActionResult EditUser(string id) => Redirect($"/app/users/{Uri.EscapeDataString(id)}/edit");

    // Referee
    [HttpGet("Referee")] public IActionResult Referee() => Redirect("/app/referee");
    [HttpGet("Referee/MyMatches")] public IActionResult MyMatches() => Redirect("/app/referee/matches");
    [HttpGet("Referee/MyAvailability")] public IActionResult MyAvailability() => Redirect("/app/referee/availability");
    [HttpGet("Referee/CreateUnavailability")] public IActionResult CreateUnavailability() => Redirect("/app/referee/availability/new");
    [HttpGet("Referee/EditUnavailability/{id:int}")] public IActionResult EditUnavailability(int id) => Redirect($"/app/referee/availability/{id}/edit");
    [HttpGet("Referee/DeleteUnavailability/{id:int}")] public IActionResult DeleteUnavailability(int id) => Redirect("/app/referee/availability");
}
