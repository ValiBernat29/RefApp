using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefApp.Models;

namespace RefApp.Controllers.Api;

/// <summary>Static option lists the forms need (enum values, valid cities).</summary>
[Authorize]
[ApiController]
[Route("api/lookups")]
public class LookupsApiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        leagues = Enum.GetNames<League>(),
        matchDays = Enum.GetNames<DayOfWeek>(),
        cities = AradLocalities.All,
    });
}
