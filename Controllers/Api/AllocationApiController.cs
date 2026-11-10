using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

namespace RefApp.Controllers.Api;

[Route("api/board/allocation")]
public class AllocationApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISmartAllocationEngine _engine;

    public AllocationApiController(ApplicationDbContext context, ISmartAllocationEngine engine)
    {
        _context = context;
        _engine = engine;
    }

    /// <summary>Runs the allocation solver for a date range, saves the result, and returns it with the resulting schedule.</summary>
    [HttpPost]
    public async Task<IActionResult> Run([FromBody] AllocationRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
            return BadRequest(new { error = "End date must be on or after the start date." });

        var from = request.StartDate.Date;
        var to = request.EndDate.Date.AddDays(1).AddTicks(-1); // include the whole end day

        var result = await _engine.AllocateRefereesAsync(from, to, ct);

        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments).ThenInclude(a => a.Referee)
            .Where(m => m.MatchDate >= from && m.MatchDate <= to)
            .OrderBy(m => m.MatchDate)
            .ToListAsync(ct);

        return Ok(new
        {
            result,
            startDate = from.ToString("yyyy-MM-dd"),
            endDate = request.EndDate.Date.ToString("yyyy-MM-dd"),
            matches = matches.Select(m => new
            {
                id = m.Id,
                matchDate = m.MatchDate,
                homeTeam = m.HomeTeam?.Name ?? "?",
                awayTeam = m.AwayTeam?.Name ?? "?",
                league = m.HomeTeam?.League,
                assignedCount = m.Assignments.Count,
                main = Slot(m, MatchRoleType.Main),
                assistant1 = Slot(m, MatchRoleType.Assistant1),
                assistant2 = Slot(m, MatchRoleType.Assistant2),
            }),
        });
    }

    private static object? Slot(Match match, MatchRoleType role)
    {
        var referee = match.Assignments.FirstOrDefault(a => a.RoleType == role)?.Referee;
        return referee == null
            ? null
            : new { name = referee.DisplayName ?? referee.UserName ?? referee.Email ?? "—", hasCar = referee.HasCar };
    }
}

public class AllocationRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
