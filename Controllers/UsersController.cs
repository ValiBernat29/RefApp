using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using RefApp.Services;

namespace RefApp.Controllers;

public class UsersController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GeocodingService _geocoding;

    public UsersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        GeocodingService geocoding)
    {
        _context = context;
        _userManager = userManager;
        _geocoding = geocoding;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Prioritize UserName over Email for sorting
        var users = await _context.Users
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(cancellationToken);

        var userRolesLookup = await _context.UserRoles
            .Join(_context.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                g => g.Key,
                g => string.Join(", ", g.Select(x => x.Name)),
                cancellationToken);

        ViewBag.UserRoles = userRolesLookup;

        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var userRole = roles.FirstOrDefault() ?? "Referee";

        var vm = new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = user.DisplayName,
            Role = userRole,
            HomeCity = user.HomeCity,
            Rank = user.Rank,
            PreferredRole = user.PreferredRole,
            HasCar = user.HasCar
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, EditUserViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        // Validate city is a real Arad County locality
        if (!string.IsNullOrEmpty(model.HomeCity)
            && !AradLocalities.All.Contains(model.HomeCity.Trim()))
        {
            ModelState.AddModelError(nameof(model.HomeCity), "Please select a valid city or village from the list.");
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (user.UserName != model.UserName)
        {
            var nameResult = await _userManager.SetUserNameAsync(user, model.UserName);
            if (!nameResult.Succeeded)
            {
                foreach (var error in nameResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            var safeName = model.UserName ?? "user";
            var dummyEmail = $"{safeName.Replace(" ", "")}@refapp.local";
            await _userManager.SetEmailAsync(user, dummyEmail);
        }

        user.DisplayName = model.DisplayName ?? string.Empty;
        user.Rank = model.Rank;
        user.PreferredRole = model.PreferredRole;
        user.HasCar = model.HasCar;

        // Geocode HomeCity if it changed
        var newCity = (model.HomeCity ?? "").Trim();
        if (user.HomeCity != newCity)
        {
            user.HomeCity = newCity;
            if (!string.IsNullOrEmpty(newCity))
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
        var newRole = model.Role ?? "Referee";

        if (!currentRoles.Contains(newRole))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);
        }

        if (!string.IsNullOrEmpty(model.NewPassword))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            var passResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!passResult.Succeeded)
            {
                foreach (var error in passResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }
        }

        TempData["Success"] = "Account updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Referee Fleet Management ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> RefereeFleet(CancellationToken cancellationToken)
    {
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(cancellationToken);
        return View(referees);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCar(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.HasCar = !user.HasCar;
        await _userManager.UpdateAsync(user);

        return Json(new { hasCar = user.HasCar });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Safety check: Don't let the admin delete themselves!
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }


        var userAssignments = await _context.MatchAssignments
            .Where(a => a.RefereeId == id)
            .ToListAsync();
        _context.MatchAssignments.RemoveRange(userAssignments);

        var userUnavailabilities = await _context.Unavailabilities
            .Where(u => u.RefereeId == id)
            .ToListAsync();
        _context.Unavailabilities.RemoveRange(userUnavailabilities);

        await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Account and all associated records deleted successfully.";
        }
        else
        {
            TempData["Error"] = "There was an error deleting this account.";
        }

        return RedirectToAction(nameof(Index));
    }
}
