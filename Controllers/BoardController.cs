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

public class BoardController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;

    public BoardController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        await UnavailabilityMaintenance.DeleteExpiredAsync(_context, today, cancellationToken);

        var endOfWindow = today.AddDays(7);
        var upcoming = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.MatchDate >= today && m.MatchDate < endOfWindow)
            .OrderBy(m => m.MatchDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        ViewBag.UpcomingMatchesCount = await _context.Matches
            .CountAsync(m => m.MatchDate >= today && m.MatchDate < endOfWindow, cancellationToken);

        var matchesNeedingAssignments = await _context.Matches
            .Where(m => m.MatchDate >= today && m.MatchDate < endOfWindow)
            .Where(m =>
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Main) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant1) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant2))
            .CountAsync(cancellationToken);
        ViewBag.MatchesNeedingAssignments = matchesNeedingAssignments;

        ViewBag.UpcomingMatches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.MatchDate >= today && m.MatchDate < endOfWindow)
            .Where(m =>
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Main) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant1) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant2))
            .OrderBy(m => m.MatchDate)
            .Take(5)
            .ToListAsync(cancellationToken);
        ViewBag.RecentUnavailabilities = await _context.Unavailabilities
            .Include(u => u.Referee)
            .Where(u => u.EndDate >= today)
            .OrderBy(u => u.StartDate)
            .Take(5)
            .ToListAsync(cancellationToken);
        return View();
    }
}
