using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.ViewModels;

namespace RefApp.Services;

/// <summary>Scored candidate lists, one per slot (each is sorted for that slot's role).</summary>
public record SlotCandidates(
    List<RefereeOption> Main,
    List<RefereeOption> Assistant1,
    List<RefereeOption> Assistant2);

/// <summary>
/// Single home for the manual-assignment rules: who can be offered for a match,
/// whether a proposed set of referees is allowed, and saving it. Used by the
/// assign page and the route map so both enforce identical rules.
/// </summary>
public class AssignmentService
{
    /// <summary>Another match closer than this blocks a referee (double-booking).</summary>
    private static readonly TimeSpan ConflictWindow = TimeSpan.FromHours(5);

    private readonly ApplicationDbContext _context;
    private readonly RefereeScoringService _scoring;

    public AssignmentService(ApplicationDbContext context, RefereeScoringService scoring)
    {
        _context = context;
        _scoring = scoring;
    }

    public Task<Match?> FindMatchAsync(int id, CancellationToken ct) =>
        _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <summary>
    /// A referee whose home city matches either team's city cannot officiate that match.
    /// Referees based in Arad are exempt (it is the hub city).
    /// </summary>
    public static bool HasCityConflict(ApplicationUser referee, Match match)
    {
        var homeCity = referee.HomeCity?.Trim() ?? "";
        if (homeCity.Length == 0 || homeCity.Equals("Arad", StringComparison.OrdinalIgnoreCase))
            return false;

        return CityMatches(match.HomeTeam?.City, homeCity) || CityMatches(match.AwayTeam?.City, homeCity);
    }

    private static bool CityMatches(string? teamCity, string refereeCity) =>
        !string.IsNullOrEmpty(teamCity) && teamCity.Trim().Equals(refereeCity, StringComparison.OrdinalIgnoreCase);

    private async Task<List<ApplicationUser>> GetRefereesAsync(CancellationToken ct) =>
        await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email ?? "")
            .ToListAsync(ct);

    /// <summary>Builds the flagged and scored candidate list for each of the three slots.</summary>
    public async Task<SlotCandidates> GetCandidatesAsync(Match match, CancellationToken ct)
    {
        var referees = await GetRefereesAsync(ct);

        var matchDate = match.MatchDate.Date;
        var windowStart = match.MatchDate - ConflictWindow;
        var windowEnd = match.MatchDate + ConflictWindow;

        var unavailableIds = await _context.Unavailabilities
            .Where(u => u.StartDate <= matchDate && u.EndDate >= matchDate)
            .Select(u => u.RefereeId)
            .Distinct()
            .ToListAsync(ct);

        // Hard conflict: another match within the window.
        var conflictIds = await _context.MatchAssignments
            .Where(a => a.MatchId != match.Id
                && a.Match!.MatchDate > windowStart
                && a.Match!.MatchDate < windowEnd)
            .Select(a => a.RefereeId)
            .Distinct()
            .ToListAsync(ct);

        // Soft warning: same day, but outside the window.
        var sameDayIds = await _context.MatchAssignments
            .Where(a => a.MatchId != match.Id
                && a.Match!.MatchDate.Date == matchDate
                && (a.Match!.MatchDate <= windowStart || a.Match!.MatchDate >= windowEnd))
            .Select(a => a.RefereeId)
            .Distinct()
            .ToListAsync(ct);

        List<RefereeOption> Build() => referees
            .Select(r => new RefereeOption
            {
                Id = r.Id,
                DisplayName = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Email = r.Email ?? "",
                IsUnavailable = unavailableIds.Contains(r.Id),
                HasConflictingMatch = conflictIds.Contains(r.Id),
                HasOtherMatchThatDay = sameDayIds.Contains(r.Id),
                HasCityConflict = HasCityConflict(r, match)
            })
            .ToList();

        // Each slot gets its own list: scoring is role-aware, and it fills in per-option metadata.
        return new SlotCandidates(
            await _scoring.ScoreAndSortAsync(Build(), match, referees, MatchRoleType.Main),
            await _scoring.ScoreAndSortAsync(Build(), match, referees, MatchRoleType.Assistant1),
            await _scoring.ScoreAndSortAsync(Build(), match, referees, MatchRoleType.Assistant2));
    }

    /// <summary>Returns a user-facing error if the proposed assignment is not allowed, otherwise null.</summary>
    public async Task<string?> ValidateAsync(
        Match match, string? mainId, string? assistant1Id, string? assistant2Id, CancellationToken ct)
    {
        var ids = new[] { mainId, assistant1Id, assistant2Id }
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();
        if (ids.Count == 0) return null;

        if (ids.Distinct().Count() != ids.Count)
            return "Each referee can only be assigned to one role per match.";

        var windowStart = match.MatchDate - ConflictWindow;
        var windowEnd = match.MatchDate + ConflictWindow;

        var doubleBooked = await _context.MatchAssignments
            .AnyAsync(a => a.MatchId != match.Id
                && a.Match!.MatchDate > windowStart
                && a.Match!.MatchDate < windowEnd
                && ids.Contains(a.RefereeId), ct);
        if (doubleBooked)
            return "One or more selected referees are already assigned to another match within 5 hours of this one.";

        var selected = await _context.Users.Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        foreach (var r in selected)
        {
            if (HasCityConflict(r, match))
                return $"{r.DisplayName ?? r.UserName ?? r.Id} cannot officiate a team from their home city ({r.HomeCity?.Trim()}).";
        }

        return null;
    }

    /// <summary>Replaces the match's assignments with the given referees (single SaveChanges, so atomic).</summary>
    public async Task SaveAsync(
        Match match, string? mainId, string? assistant1Id, string? assistant2Id, CancellationToken ct)
    {
        _context.MatchAssignments.RemoveRange(match.Assignments);

        void Add(string? refereeId, MatchRoleType role)
        {
            if (!string.IsNullOrEmpty(refereeId))
                _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = refereeId, RoleType = role });
        }
        Add(mainId, MatchRoleType.Main);
        Add(assistant1Id, MatchRoleType.Assistant1);
        Add(assistant2Id, MatchRoleType.Assistant2);

        await _context.SaveChangesAsync(ct);
    }
}
