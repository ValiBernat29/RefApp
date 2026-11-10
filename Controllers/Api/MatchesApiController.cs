using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;

namespace RefApp.Controllers.Api;

[Route("api/board/matches")]
public class MatchesApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public MatchesApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Matches in a date range (default: the next 7 days), optionally limited to one league.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? league, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var from = (startDate ?? today).Date;
        var to = (endDate ?? today.AddDays(7)).Date;
        var endOfDay = to.AddDays(1).AddTicks(-1);

        var query = _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments).ThenInclude(a => a.Referee)
            .Where(m => m.MatchDate >= from && m.MatchDate <= endOfDay);

        if (!string.IsNullOrEmpty(league) && Enum.TryParse<League>(league, out var leagueValue))
        {
            // A match belongs to the league of its home team.
            var teamIds = await _context.Teams.Where(t => t.League == leagueValue).Select(t => t.Id).ToListAsync(ct);
            query = query.Where(m => teamIds.Contains(m.HomeTeamId));
        }

        var matches = await query.OrderBy(m => m.MatchDate).ToListAsync(ct);

        return Ok(new
        {
            filter = new { league = league ?? "", startDate = from.ToString("yyyy-MM-dd"), endDate = to.ToString("yyyy-MM-dd") },
            matches = matches.Select(m => new
            {
                id = m.Id,
                matchDate = m.MatchDate,
                homeTeam = m.HomeTeam?.Name ?? "?",
                awayTeam = m.AwayTeam?.Name ?? "?",
                location = m.Location,
                assignedCount = m.Assignments.Count,
                main = RefereeName(m, MatchRoleType.Main),
                assistant1 = RefereeName(m, MatchRoleType.Assistant1),
                assistant2 = RefereeName(m, MatchRoleType.Assistant2),
            }),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var match = await _context.Matches.Include(m => m.HomeTeam).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match == null) return NotFound(new { error = "Match not found." });

        return Ok(new
        {
            id = match.Id,
            matchDate = match.MatchDate,
            homeTeamId = match.HomeTeamId,
            awayTeamId = match.AwayTeamId,
            league = match.HomeTeam?.League,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MatchRequest request, CancellationToken ct)
    {
        var (home, away, error) = await CheckAsync(request, null, ct);
        if (error != null) return BadRequest(new { error });

        var match = new Match
        {
            MatchDate = request.MatchDate,
            Location = home!.City ?? home.Name,
            HomeTeamId = home.Id,
            AwayTeamId = away!.Id,
        };
        _context.Matches.Add(match);
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = match.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MatchRequest request, CancellationToken ct)
    {
        var match = await _context.Matches.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match == null) return NotFound(new { error = "Match not found." });

        var (home, away, error) = await CheckAsync(request, id, ct);
        if (error != null) return BadRequest(new { error });

        match.MatchDate = request.MatchDate;
        match.Location = home!.City ?? home.Name;
        match.HomeTeamId = home.Id;
        match.AwayTeamId = away!.Id;
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = match.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var match = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match == null) return NotFound(new { error = "Match not found." });

        _context.MatchAssignments.RemoveRange(match.Assignments);
        _context.Matches.Remove(match);
        await _context.SaveChangesAsync(ct);
        return Ok(new { name = $"{match.HomeTeam?.Name} vs {match.AwayTeam?.Name}" });
    }

    private static string? RefereeName(Match match, MatchRoleType role)
    {
        var referee = match.Assignments.FirstOrDefault(a => a.RoleType == role)?.Referee;
        return referee == null ? null : referee.DisplayName ?? referee.UserName ?? referee.Email ?? "?";
    }

    /// <summary>The scheduling rules shared by create and edit. Returns an error message, or the two teams.</summary>
    private async Task<(Team? Home, Team? Away, string? Error)> CheckAsync(MatchRequest request, int? excludeMatchId, CancellationToken ct)
    {
        if (request.HomeTeamId == request.AwayTeamId)
            return (null, null, "Home and away team must be different.");

        var home = await _context.Teams.FindAsync(new object[] { request.HomeTeamId }, ct);
        var away = await _context.Teams.FindAsync(new object[] { request.AwayTeamId }, ct);
        if (home == null || away == null)
            return (null, null, "Selected teams could not be found.");

        if (home.League != away.League)
            return (null, null, "Home and away team must be from the same league.");

        var day = request.MatchDate.Date;
        var clash = await _context.Matches
            .Where(m => excludeMatchId == null || m.Id != excludeMatchId)
            .AnyAsync(m => m.MatchDate.Date == day &&
                (m.HomeTeamId == home.Id || m.AwayTeamId == home.Id || m.HomeTeamId == away.Id || m.AwayTeamId == away.Id), ct);
        if (clash)
            return (null, null, "One of the selected teams already has a match on this date.");

        return (home, away, null);
    }
}

public class MatchRequest
{
    [Required]
    public DateTime MatchDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select the home team.")]
    public int HomeTeamId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select the away team.")]
    public int AwayTeamId { get; set; }
}
