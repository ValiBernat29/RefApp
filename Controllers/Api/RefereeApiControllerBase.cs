using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers.Api;

/// <summary>
/// Base for the referee-facing JSON API: requires the Referee role and validates the antiforgery header
/// on every unsafe request. Every query must be scoped to the signed-in referee.
/// </summary>
[Authorize(Roles = "Referee")]
[ApiController]
[AutoValidateAntiforgeryToken]
public abstract class RefereeApiControllerBase : ControllerBase
{
}
