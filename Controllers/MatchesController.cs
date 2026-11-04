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

public class MatchesController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;

    public MatchesController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? league, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Dacă nu s-au selectat date manual, punem default pe următoarele 7 zile (săptămâna curentă)
        var filterStartDate = startDate ?? today;
        var filterEndDate = endDate ?? today.AddDays(7);

        var query = _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .ThenInclude(a => a.Referee)
            .AsQueryable();

        // 1. Filtrarea după dată
        // Adăugăm o zi la EndDate pentru a include meciurile jucate până la ora 23:59 în acea zi
        var endOfDay = filterEndDate.Date.AddDays(1).AddTicks(-1);
        query = query.Where(m => m.MatchDate >= filterStartDate.Date && m.MatchDate <= endOfDay);

        // 2. Filtrarea după Ligă
        if (!string.IsNullOrEmpty(league))
        {
            if (Enum.TryParse<RefApp.Models.League>(league, out var leagueEnum))
            {
                // Căutăm toate echipele din liga selectată
                var teamsInLeague = await _context.Teams
                    .Where(t => t.League == leagueEnum)
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);

                // Afișăm doar meciurile unde echipa gazdă face parte din acea ligă
                query = query.Where(m => teamsInLeague.Contains(m.HomeTeamId));
            }
        }

        var matches = await query.OrderBy(m => m.MatchDate).ToListAsync(cancellationToken);

        // Salvăm filtrele curente în ViewBag ca să le putem afișa înapoi în interfață (să nu se reseteze vizual)
        ViewBag.StartDate = filterStartDate.ToString("yyyy-MM-dd");
        ViewBag.EndDate = filterEndDate.ToString("yyyy-MM-dd");
        ViewBag.SelectedLeague = league;

        return View(matches);
    }

    [HttpGet]
    public async Task<IActionResult> CreateMatch(CancellationToken cancellationToken)
    {
        var teams = await _context.Teams
            .OrderBy(t => t.League)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var vm = new CreateMatchViewModel
        {
            MatchDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0),
            League = null,
            Teams = teams
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMatch([FromForm] CreateMatchViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        if (model.HomeTeamId == model.AwayTeamId)
        {
            ModelState.AddModelError(string.Empty, "Home and away team must be different.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        var home = await _context.Teams.FindAsync(new object[] { model.HomeTeamId }, cancellationToken);
        var away = await _context.Teams.FindAsync(new object[] { model.AwayTeamId }, cancellationToken);

        if (home == null || away == null)
        {
            ModelState.AddModelError(string.Empty, "Selected teams could not be found.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        if (home.League != away.League)
        {
            ModelState.AddModelError(string.Empty, "Home and away team must be from the same league.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        var matchDate = model.MatchDate.Date;
        var hasTeamConflict = await _context.Matches
            .AnyAsync(m =>
                m.MatchDate.Date == matchDate &&
                (m.HomeTeamId == home.Id || m.AwayTeamId == home.Id || m.HomeTeamId == away.Id || m.AwayTeamId == away.Id),
                cancellationToken);

        if (hasTeamConflict)
        {
            ModelState.AddModelError(string.Empty, "One of the selected teams already has a match on this date.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        var match = new Match
        {
            MatchDate = model.MatchDate,
            Location = home.City ?? home.Name,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id
        };

        _context.Matches.Add(match);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Match created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditMatch(int id, CancellationToken cancellationToken)
    {
        var match = await _context.Matches.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (match == null)
            return NotFound();

        var teams = await _context.Teams
            .OrderBy(t => t.League)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var vm = new CreateMatchViewModel
        {
            Id = match.Id,
            MatchDate = match.MatchDate,
            Location = match.Location,
            HomeTeamId = match.HomeTeamId,
            AwayTeamId = match.AwayTeamId,
            League = teams.FirstOrDefault(t => t.Id == match.HomeTeamId)?.League,
            Teams = teams
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMatch(int id, [FromForm] CreateMatchViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        var match = await _context.Matches.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (match == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        if (model.HomeTeamId == model.AwayTeamId)
        {
            ModelState.AddModelError(string.Empty, "Home and away team must be different.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        var home = await _context.Teams.FindAsync(new object[] { model.HomeTeamId }, cancellationToken);
        var away = await _context.Teams.FindAsync(new object[] { model.AwayTeamId }, cancellationToken);

        if (home == null || away == null)
        {
            ModelState.AddModelError(string.Empty, "Selected teams could not be found.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        if (home.League != away.League)
        {
            ModelState.AddModelError(string.Empty, "Home and away team must be from the same league.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        var matchDate = model.MatchDate.Date;
        var hasTeamConflict = await _context.Matches
            .Where(m => m.Id != id)
            .AnyAsync(m =>
                m.MatchDate.Date == matchDate &&
                (m.HomeTeamId == home.Id || m.AwayTeamId == home.Id || m.HomeTeamId == away.Id || m.AwayTeamId == away.Id),
                cancellationToken);

        if (hasTeamConflict)
        {
            ModelState.AddModelError(string.Empty, "One of the selected teams already has a match on this date.");
            model.Teams = await _context.Teams
                .OrderBy(t => t.League)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return View(model);
        }

        match.MatchDate = model.MatchDate;
        match.Location = home.City ?? home.Name;
        match.HomeTeamId = home.Id;
        match.AwayTeamId = away.Id;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Match updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMatch(int id, CancellationToken cancellationToken)
    {
        var match = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (match == null)
            return NotFound();

        _context.MatchAssignments.RemoveRange(match.Assignments);
        _context.Matches.Remove(match);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Hosts the Vue assign page. Data comes from <see cref="MatchAssignmentApiController"/>.</summary>
    [HttpGet]
    public async Task<IActionResult> Assign(int id, CancellationToken cancellationToken)
    {
        if (!await _context.Matches.AnyAsync(m => m.Id == id, cancellationToken))
            return NotFound();

        ViewBag.MatchId = id;
        return View();
    }
}
