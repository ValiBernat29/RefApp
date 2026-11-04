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

public class TeamsController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly GeocodingService _geocoding;

    public TeamsController(
        ApplicationDbContext context,
        GeocodingService geocoding)
    {
        _context = context;
        _geocoding = geocoding;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var teams = await _context.Teams
            .OrderBy(t => t.League)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
        return View(teams);
    }

    [HttpGet]
    public IActionResult CreateTeam()
    {
        return View(new Team { PreferredMatchDay = DayOfWeek.Sunday });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam([FromForm] Team model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _context.Teams.Add(model);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Team created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditTeam(int id, CancellationToken cancellationToken)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, cancellationToken);
        if (team == null) return NotFound();

        return View(team);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTeam(int id, [FromForm] Team model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        // Validate city is a real Arad County locality
        if (!string.IsNullOrEmpty(model.City)
            && !AradLocalities.All.Contains(model.City.Trim()))
        {
            ModelState.AddModelError(nameof(model.City), "Please select a valid city or village from the list.");
            return View(model);
        }

        var team = await _context.Teams.FindAsync(new object[] { id }, cancellationToken);
        if (team == null) return NotFound();

        // Actualizăm proprietățile
        team.Name = model.Name ?? "";
        team.League = model.League;
        team.PreferredMatchDay = model.PreferredMatchDay;

        // Geocode city if it changed
        var newCity = (model.City ?? "").Trim();
        if (team.City != newCity)
        {
            team.City = newCity;
            if (!string.IsNullOrEmpty(newCity))
            {
                var coords = await _geocoding.GeocodeAsync(newCity);
                team.Latitude = coords?.Lat;
                team.Longitude = coords?.Lon;
            }
            else
            {
                team.Latitude = null;
                team.Longitude = null;
            }

            // Sync location to all matches where this team is the home team
            var homeMatches = await _context.Matches
                .Where(m => m.HomeTeamId == team.Id)
                .ToListAsync(cancellationToken);
            foreach (var match in homeMatches)
            {
                match.Location = newCity;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Team updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTeam(int id, CancellationToken cancellationToken)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, cancellationToken);
        if (team == null) return NotFound();


        var teamMatches = await _context.Matches
            .Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id)
            .ToListAsync(cancellationToken);

        var matchIds = teamMatches.Select(m => m.Id).ToList();

        if (matchIds.Any())
        {
            var relatedAssignments = await _context.MatchAssignments
                .Where(a => matchIds.Contains(a.MatchId))
                .ToListAsync(cancellationToken);

            _context.MatchAssignments.RemoveRange(relatedAssignments);
        }

        _context.Matches.RemoveRange(teamMatches);

        _context.Teams.Remove(team);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"Team {team.Name} and all associated matches were deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
