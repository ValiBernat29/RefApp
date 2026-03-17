using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace RefApp.Controllers;

[Authorize(Roles = "Board")]
public class BoardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public BoardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        var matchesNeedingAssignments = await _context.Matches
            .Where(m => m.MatchDate >= today)
            .Where(m =>
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Main) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant1) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant2))
            .CountAsync(cancellationToken);
        ViewBag.MatchesNeedingAssignments = matchesNeedingAssignments;

        ViewBag.UpcomingMatches = upcoming
            .Where(m =>
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Main) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant1) ||
                !_context.MatchAssignments.Any(a => a.MatchId == m.Id && a.RoleType == MatchRoleType.Assistant2))
            .ToList();
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
    public async Task<IActionResult> UpcomingMatches(string? league, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Dacă nu s-au selectat date manual, punem default pe următoarele 7 zile (săptămâna curentă)
        var filterStartDate = startDate ?? today;
        var filterEndDate = endDate ?? today.AddDays(7);

        var query = _context.Matches
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
                    .Select(t => t.Name)
                    .ToListAsync(cancellationToken);

                // Afișăm doar meciurile unde echipa gazdă face parte din acea ligă
                query = query.Where(m => teamsInLeague.Contains(m.HomeTeam));
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

        var matchDate = match.MatchDate.Date;
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(cancellationToken);

        var unavailableRefereeIds = await _context.Unavailabilities
            .Where(u => u.StartDate <= matchDate && u.EndDate >= matchDate)
            .Select(u => u.RefereeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var otherMatchRefIds = await _context.MatchAssignments
            .Include(a => a.Match)
            .Where(a => a.MatchId != match.Id && a.Match!.MatchDate.Date == matchDate)
            .Select(a => a.RefereeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // We removed the filter here so EVERYONE shows up!
        var eligible = referees
            .Select(r => new RefereeOption
            {
                Id = r.Id,
                DisplayName = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Email = r.Email ?? "",
                IsUnavailable = unavailableRefereeIds.Contains(r.Id), // This flags them for the JS script
                HasOtherMatchThatDay = otherMatchRefIds.Contains(r.Id)
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
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(cancellationToken);

        var matchDate = match.MatchDate.Date;
        var unavailableRefereeIds = await _context.Unavailabilities
            .Where(u => u.StartDate <= matchDate && u.EndDate >= matchDate)
            .Select(u => u.RefereeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        model.EligibleReferees = referees
            .Select(r => new RefereeOption
            {
                Id = r.Id,
                DisplayName = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Email = r.Email ?? "",
                IsUnavailable = unavailableRefereeIds.Contains(r.Id) // Flags them for the JS script
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
            // Prioritize UserName for sorting the roster
            .OrderBy(u => u.Referee!.DisplayName ?? u.Referee.UserName ?? u.Referee.Email)
            .ThenBy(u => u.StartDate)
            .ToListAsync(cancellationToken);
        return View(roster);
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
            Role = userRole
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, EditUserViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

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
        return RedirectToAction(nameof(Users));
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
            return RedirectToAction(nameof(Users));
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

        return RedirectToAction(nameof(Users));
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
        {
            return View(model);
        }

        var team = await _context.Teams.FindAsync(new object[] { id }, cancellationToken);
        if (team == null) return NotFound();

        // Actualizăm proprietățile
        team.Name = model.Name ?? "";
        team.League = model.League;
        team.PreferredMatchDay = model.PreferredMatchDay;

        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Team updated successfully.";
        return RedirectToAction(nameof(Teams));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTeam(int id, CancellationToken cancellationToken)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, cancellationToken);
        if (team == null) return NotFound();


        var teamMatches = await _context.Matches
            .Where(m => m.HomeTeam == team.Name || m.AwayTeam == team.Name)
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
        return RedirectToAction(nameof(Teams));
    }

}