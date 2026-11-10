using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

namespace RefApp.Controllers.Api;

[Route("api/board/users")]
public class UsersApiController : BoardApiControllerBase
{
    private static readonly string[] AllowedRoles = { "Referee", "Board" };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GeocodingService _geocoding;

    public UsersApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, GeocodingService geocoding)
    {
        _context = context;
        _userManager = userManager;
        _geocoding = geocoding;
    }

    /// <summary>Every account with its role(s). Also feeds the referee fleet page.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _context.Users
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(ct);

        var roles = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name!).ToList(), ct);

        return Ok(users.Select(u =>
        {
            var userRoles = roles.GetValueOrDefault(u.Id) ?? new List<string>();
            return new
            {
                id = u.Id,
                userName = u.UserName,
                displayName = u.DisplayName,
                role = userRoles.Count == 0 ? "—" : string.Join(", ", userRoles),
                isReferee = userRoles.Contains("Referee"),
                hasCar = u.HasCar,
                rank = u.Rank,
                homeCity = u.HomeCity,
                preferredRole = u.PreferredRole,
            };
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "User not found." });

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            id = user.Id,
            userName = user.UserName,
            displayName = user.DisplayName,
            role = roles.FirstOrDefault() ?? "Referee",
            homeCity = user.HomeCity,
            rank = user.Rank,
            preferredRole = user.PreferredRole,
            hasCar = user.HasCar,
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UserUpdateRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "User not found." });

        if (!AllowedRoles.Contains(request.Role))
            return BadRequest(new { error = "Role must be Referee or Board." });

        var homeCity = request.HomeCity?.Trim();
        if (!string.IsNullOrEmpty(homeCity) && !AradLocalities.All.Contains(homeCity))
        {
            ModelState.AddModelError(nameof(UserUpdateRequest.HomeCity), "Please select a valid city or village from the list.");
            return ValidationProblem(ModelState);
        }

        if (user.UserName != request.UserName)
        {
            var nameResult = await _userManager.SetUserNameAsync(user, request.UserName);
            if (!nameResult.Succeeded) return BadRequest(new { error = Describe(nameResult) });

            // Accounts here do not use real email addresses, so keep the placeholder in step with the username.
            await _userManager.SetEmailAsync(user, $"{request.UserName.Replace(" ", "")}@refapp.local");
        }

        user.DisplayName = request.DisplayName ?? string.Empty;
        user.Rank = request.Rank;
        user.PreferredRole = request.PreferredRole;
        user.HasCar = request.HasCar;

        var newCity = homeCity ?? "";
        if ((user.HomeCity ?? "") != newCity)
        {
            user.HomeCity = newCity;
            if (newCity.Length > 0)
            {
                var coords = await _geocoding.GeocodeAsync(newCity);
                user.Latitude = coords?.Lat;
                user.Longitude = coords?.Lon;
            }
            else
            {
                user.Latitude = null;
                user.Longitude = null;
            }
        }

        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(request.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!passwordResult.Succeeded) return BadRequest(new { error = Describe(passwordResult) });
        }

        return Ok(new { id = user.Id });
    }

    [HttpPost("{id}/toggle-car")]
    public async Task<IActionResult> ToggleCar(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "User not found." });

        user.HasCar = !user.HasCar;
        await _userManager.UpdateAsync(user);
        return Ok(new { hasCar = user.HasCar });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "User not found." });

        var current = await _userManager.GetUserAsync(User);
        if (current?.Id == id)
            return BadRequest(new { error = "You cannot delete your own account." });

        _context.MatchAssignments.RemoveRange(await _context.MatchAssignments.Where(a => a.RefereeId == id).ToListAsync(ct));
        _context.Unavailabilities.RemoveRange(await _context.Unavailabilities.Where(u => u.RefereeId == id).ToListAsync(ct));
        await _context.SaveChangesAsync(ct);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded) return BadRequest(new { error = "There was an error deleting this account." });

        return Ok(new { userName = user.UserName });
    }

    private static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Description));
}

public class UserUpdateRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    public string? NewPassword { get; set; }

    [StringLength(100)]
    public string? HomeCity { get; set; }

    [EnumDataType(typeof(RefereeRank))]
    public RefereeRank Rank { get; set; }

    [EnumDataType(typeof(RefereePreferredRole))]
    public RefereePreferredRole PreferredRole { get; set; }

    public bool HasCar { get; set; }
}
