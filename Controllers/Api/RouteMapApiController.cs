using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;

namespace RefApp.Controllers.Api;

/// <summary>Referees and upcoming matches for the interactive route map. Saving uses the assignment API.</summary>
[Route("api/board/route-map")]
public class RouteMapApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public RouteMapApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .ToListAsync(ct);

        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .Where(m => m.MatchDate >= DateTime.Today)
            .OrderBy(m => m.MatchDate)
            .Take(60)
            .ToListAsync(ct);

        return Ok(new
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
        });
    }
}
