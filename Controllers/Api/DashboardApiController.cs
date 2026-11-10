using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

namespace RefApp.Controllers.Api;

[Route("api/board/dashboard")]
public class DashboardApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        await UnavailabilityMaintenance.DeleteExpiredAsync(_context, today, ct);

        var endOfWindow = today.AddDays(7);
        var thisWeek = _context.Matches.Where(m => m.MatchDate >= today && m.MatchDate < endOfWindow);

        // A match still needs referees when any of its three slots is empty.
        var needsReferees = thisWeek.Where(m =>
            !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Main) ||
            !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant1) ||
            !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant2));

        var pending = await needsReferees
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.MatchDate)
            .Take(5)
            .ToListAsync(ct);

        var unavailabilities = await _context.Unavailabilities
            .Include(u => u.Referee)
            .Where(u => u.EndDate >= today)
            .OrderBy(u => u.StartDate)
            .Take(5)
            .ToListAsync(ct);

        return Ok(new
        {
            upcomingMatchesCount = await thisWeek.CountAsync(ct),
            matchesNeedingReferees = await needsReferees.CountAsync(ct),
            pendingMatches = pending.Select(m => new
            {
                id = m.Id,
                homeTeam = m.HomeTeam?.Name ?? "?",
                awayTeam = m.AwayTeam?.Name ?? "?",
                matchDate = m.MatchDate,
                location = m.Location,
            }),
            recentUnavailabilities = unavailabilities.Select(u => new
            {
                id = u.Id,
                refereeName = u.Referee?.DisplayName ?? u.Referee?.Email ?? u.Referee?.UserName ?? "—",
                startDate = u.StartDate,
                endDate = u.EndDate,
            }),
        });
    }
}
