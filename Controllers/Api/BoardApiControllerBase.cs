using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers.Api;

/// <summary>
/// Base for the Board-only JSON API used by the Vue app.
/// Requires the Board role and validates the antiforgery header on every unsafe request (POST/PUT/DELETE).
/// Unauthenticated callers get 401 and wrong-role callers get 403 (never a login redirect), see Program.cs.
/// </summary>
[Authorize(Roles = "Board")]
[ApiController]
[AutoValidateAntiforgeryToken]
public abstract class BoardApiControllerBase : ControllerBase
{
}
