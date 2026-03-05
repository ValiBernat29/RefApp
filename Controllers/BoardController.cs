using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.ViewModels;

namespace RefApp.Controllers;

[Authorize(Roles = "Board")]
    public class BoardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BoardController(ApplicationDbContext context)
        {
            _context = context;
        }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var endOfWindow = today.AddDays(7);
        var upcoming = await _context.Matches
            .Where(m => m.MatchDate >= today && m.MatchDate < endOfWindow)
            .OrderBy(m => m.MatchDate)
            .Take(5)
            .ToListAsync(cancellationToken);
        ViewBag.PendingMatches = await _context.Matches
            .CountAsync(m => m.MatchDate >= today && m.MatchDate < endOfWindow, cancellationToken);
        ViewBag.UnavailableRefsCount = await _context.Unavailabilities
            .Where(u => u.EndDate >= today)
            .Select(u => u.RefereeId)
            .Distinct()
            .CountAsync(cancellationToken);
        ViewBag.UpcomingMatches = upcoming;
        ViewBag.RecentUnavailabilities = await _context.Unavailabilities
            .Include(u => u.Referee)
            .Where(u => u.EndDate >= today)
            .OrderBy(u => u.StartDate)
            .Take(5)
            .ToListAsync(cancellationToken);
        return View();
    }

    public async Task<IActionResult> Teams(CancellationToken cancellationToken)
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
        return RedirectToAction(nameof(Teams));
    }

    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .OrderBy(u => u.DisplayName ?? u.Email ?? u.UserName ?? "")
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

    public async Task<IActionResult> UpcomingMatches(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var matches = await _context.Matches
            .Include(m => m.Assignments)
            .ThenInclude(a => a.Referee)
            .Where(m => m.MatchDate >= today)
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);
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
            MatchDate = DateTime.UtcNow,
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

        var match = new Match
        {
            MatchDate = model.MatchDate,
            Location = model.Location,
            HomeTeam = home.Name,
            AwayTeam = away.Name
        };

        _context.Matches.Add(match);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Match created successfully.";
        return RedirectToAction(nameof(UpcomingMatches));
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
            HomeTeamId = teams.FirstOrDefault(t => t.Name == match.HomeTeam)?.Id ?? 0,
            AwayTeamId = teams.FirstOrDefault(t => t.Name == match.AwayTeam)?.Id ?? 0,
            League = teams.FirstOrDefault(t => t.Name == match.HomeTeam)?.League,
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

        match.MatchDate = model.MatchDate;
        match.Location = model.Location;
        match.HomeTeam = home.Name;
        match.AwayTeam = away.Name;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Match updated successfully.";
        return RedirectToAction(nameof(UpcomingMatches));
    }

    [HttpGet]
    public async Task<IActionResult> Assign(int id, CancellationToken cancellationToken)
    {
        var match = await _context.Matches
            .Include(m => m.Assignments)
            .ThenInclude(a => a.Referee)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (match == null)
            return NotFound();

        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .OrderBy(u => u.DisplayName ?? u.Email ?? u.UserName ?? "")
            .ToListAsync(cancellationToken);

        var matchDate = match.MatchDate.Date;
        var unavailableRefereeIds = await _context.Unavailabilities
            .Where(u => u.StartDate <= matchDate && u.EndDate >= matchDate)
            .Select(u => u.RefereeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var eligible = referees
            .Where(r => !unavailableRefereeIds.Contains(r.Id))
            .Select(r => new RefereeOption
            {
                Id = r.Id,
                DisplayName = r.DisplayName ?? r.Email ?? r.UserName ?? r.Id,
                Email = r.Email ?? ""
            })
            .ToList();

        var vm = new AssignRefereesViewModel
        {
            MatchId = match.Id,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            MatchDate = match.MatchDate,
            Location = match.Location,
            EligibleReferees = eligible,
            MainRefereeId = match.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Main)?.RefereeId,
            Assistant1Id = match.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Assistant1)?.RefereeId,
            Assistant2Id = match.Assignments.FirstOrDefault(a => a.RoleType == MatchRoleType.Assistant2)?.RefereeId
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, [FromForm] AssignRefereesViewModel model, CancellationToken cancellationToken)
    {
        var match = await _context.Matches
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (match == null)
            return NotFound();

        var mainId = model.MainRefereeId;
        var asst1Id = model.Assistant1Id;
        var asst2Id = model.Assistant2Id;

        var ids = new[] { mainId, asst1Id, asst2Id }.Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (ids.Distinct().Count() != ids.Count)
        {
            ModelState.AddModelError("", "Each referee can only be assigned to one role per match.");
            return await ReloadAssignView(match, model, cancellationToken);
        }

        _context.MatchAssignments.RemoveRange(match.Assignments);

        if (!string.IsNullOrEmpty(mainId))
            _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = mainId, RoleType = MatchRoleType.Main });
        if (!string.IsNullOrEmpty(asst1Id))
            _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst1Id, RoleType = MatchRoleType.Assistant1 });
        if (!string.IsNullOrEmpty(asst2Id))
            _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst2Id, RoleType = MatchRoleType.Assistant2 });

        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Referees assigned successfully.";
        return RedirectToAction(nameof(UpcomingMatches));
    }

    private async Task<IActionResult> ReloadAssignView(Match match, AssignRefereesViewModel model, CancellationToken cancellationToken)
    {
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .OrderBy(u => u.DisplayName ?? u.Email ?? u.UserName ?? "")
            .ToListAsync(cancellationToken);

        var matchDate = match.MatchDate.Date;
        var unavailableRefereeIds = await _context.Unavailabilities
            .Where(u => u.StartDate <= matchDate && u.EndDate >= matchDate)
            .Select(u => u.RefereeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        model.EligibleReferees = referees
            .Where(r => !unavailableRefereeIds.Contains(r.Id))
            .Select(r => new RefereeOption
            {
                Id = r.Id,
                DisplayName = r.DisplayName ?? r.Email ?? r.UserName ?? r.Id,
                Email = r.Email ?? ""
            })
            .ToList();
        model.MatchId = match.Id;
        model.HomeTeam = match.HomeTeam;
        model.AwayTeam = match.AwayTeam;
        model.MatchDate = match.MatchDate;
        model.Location = match.Location;
        return View(model);
    }

    public async Task<IActionResult> UnavailabilityRoster(CancellationToken cancellationToken)
    {
        var roster = await _context.Unavailabilities
            .Include(u => u.Referee)
            .OrderBy(u => u.Referee!.DisplayName ?? u.Referee.Email ?? u.Referee.UserName)
            .ThenBy(u => u.StartDate)
            .ToListAsync(cancellationToken);
        return View(roster);
    }
}
