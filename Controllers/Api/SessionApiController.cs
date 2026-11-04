using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RefApp.Models;

namespace RefApp.Controllers.Api;

/// <summary>Who is signed in. The Vue app calls this once at startup to build its navigation and guards.</summary>
[Authorize]
[ApiController]
[Route("api/me")]
public class SessionApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SessionApiController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        return Ok(new
        {
            id = user.Id,
            userName = user.UserName,
            displayName = user.DisplayName ?? user.UserName,
            roles = await _userManager.GetRolesAsync(user),
        });
    }
}
