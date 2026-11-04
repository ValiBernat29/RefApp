using Microsoft.AspNetCore.Mvc;
using RefApp.Services;

namespace RefApp.Controllers.Api;

/// <summary>JSON endpoints behind the Vue assign page.</summary>
[Route("api/board/matches/{id:int}/assignment")]
public class MatchAssignmentApiController : BoardApiControllerBase
{
    private readonly AssignmentService _assignments;

    public MatchAssignmentApiController(AssignmentService assignments)
    {
        _assignments = assignments;
    }

    /// <summary>Match summary, current assignment, and scored candidates for each slot.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var match = await _assignments.FindMatchAsync(id, ct);
        if (match == null) return NotFound(new { error = "Match not found." });

        var candidates = await _assignments.GetCandidatesAsync(match, ct);

        object Project(IEnumerable<ViewModels.RefereeOption> options) => options.Select(o => new
        {
            id = o.Id,
            name = o.DisplayName,
            isUnavailable = o.IsUnavailable,
            hasConflictingMatch = o.HasConflictingMatch,
            hasOtherMatchThatDay = o.HasOtherMatchThatDay,
            hasCityConflict = o.HasCityConflict,
            isRefused = o.IsRefusedByHomeTeam || o.IsRefusedByAwayTeam,
            distanceKm = o.DistanceKm,
            hasCar = o.HasCar,
            roleMatch = o.RoleMatchBoostApplied,
            recentMatchCount = o.RecentMatchCount,
        });

        string? Current(Models.MatchRoleType role) =>
            match.Assignments.FirstOrDefault(a => a.RoleType == role)?.RefereeId;

        return Ok(new
        {
            match = new
            {
                id = match.Id,
                homeTeam = match.HomeTeam?.Name ?? "",
                awayTeam = match.AwayTeam?.Name ?? "",
                date = match.MatchDate.ToString("dddd, dd MMM yyyy HH:mm"),
                location = match.Location,
            },
            current = new
            {
                mainRefereeId = Current(Models.MatchRoleType.Main),
                assistant1Id = Current(Models.MatchRoleType.Assistant1),
                assistant2Id = Current(Models.MatchRoleType.Assistant2),
            },
            candidates = new
            {
                main = Project(candidates.Main),
                assistant1 = Project(candidates.Assistant1),
                assistant2 = Project(candidates.Assistant2),
            },
        });
    }

    /// <summary>Validates and saves the three slots for the match.</summary>
    [HttpPut]
    public async Task<IActionResult> Put(int id, [FromBody] SaveAssignmentRequest request, CancellationToken ct)
    {
        var match = await _assignments.FindMatchAsync(id, ct);
        if (match == null) return NotFound(new { error = "Match not found." });

        var error = await _assignments.ValidateAsync(
            match, request.MainRefereeId, request.Assistant1Id, request.Assistant2Id, ct);
        if (error != null) return BadRequest(new { error });

        await _assignments.SaveAsync(match, request.MainRefereeId, request.Assistant1Id, request.Assistant2Id, ct);
        return Ok(new { success = true });
    }
}

public record SaveAssignmentRequest(string? MainRefereeId, string? Assistant1Id, string? Assistant2Id);
