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

public class RouteMapController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AssignmentService _assignments;

    public RouteMapController(
        ApplicationDbContext context,
        AssignmentService assignments)
    {
        _context = context;
        _assignments = assignments;
    }

    // ── Referee Route Map ────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>
    /// Returns referees + upcoming matches as JSON for the interactive map.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .ToListAsync(cancellationToken);

        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .Where(m => m.MatchDate >= DateTime.Today)
            .OrderBy(m => m.MatchDate)
            .Take(60)
            .ToListAsync(cancellationToken);

        var result = new
        {
            referees = referees.Select(r => new
            {
                id = r.Id,
                name = r.DisplayName ?? r.UserName ?? r.Id,
                city = r.HomeCity ?? "—",
                lat = r.Latitude,
                lon = r.Longitude,
                hasCar = r.HasCar,
                preferredRole = r.PreferredRole.ToString(),
                rank = r.Rank.ToString()
            }),
            matches = matches.Select(m => new
            {
                id = m.Id,
                homeTeam = m.HomeTeam?.Name ?? "?",
                awayTeam = m.AwayTeam?.Name ?? "?",
                date = m.MatchDate.ToString("ddd dd MMM yyyy HH:mm"),
                lat = m.HomeTeam?.Latitude,
                lon = m.HomeTeam?.Longitude,
                city = m.HomeTeam?.City ?? "",
                assignedIds = m.Assignments.Select(a => a.RefereeId).ToList(),
                mainRefereeId = m.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Main)?.RefereeId,
                assistant1Id = m.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Assistant1)?.RefereeId,
                assistant2Id = m.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Assistant2)?.RefereeId
            })
        };

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] MapAssignmentDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { error = "Invalid payload." });
        var match = await _assignments.FindMatchAsync(dto.MatchId, cancellationToken);
        if (match == null) return NotFound(new { error = "Match not found." });

        var error = await _assignments.ValidateAsync(match, dto.MainRefereeId, dto.Assistant1Id, dto.Assistant2Id, cancellationToken);
        if (error != null) return BadRequest(new { error });

        await _assignments.SaveAsync(match, dto.MainRefereeId, dto.Assistant1Id, dto.Assistant2Id, cancellationToken);
        return Ok(new { success = true });
    }
}

public class MapAssignmentDto
{
    public int MatchId { get; set; }
    public string? MainRefereeId { get; set; }
    public string? Assistant1Id { get; set; }
    public string? Assistant2Id { get; set; }
}
