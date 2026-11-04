using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RefApp.Controllers;

/// <summary>Base for every Board-only MVC controller, so the role requirement lives in one place.</summary>
[Authorize(Roles = "Board")]
public abstract class BoardControllerBase : Controller
{
}
