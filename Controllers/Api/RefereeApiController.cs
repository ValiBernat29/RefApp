using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

namespace RefApp.Controllers.Api;

[Route("api/referee")]
public class RefereeApiController : RefereeApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public RefereeApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>The matches this referee is assigned to, with the rest of the referee team for each.</summary>
    [HttpGet("matches")]
    public async Task<IActionResult> MyMatches(CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments).ThenInclude(a => a.Referee)
            .Where(m => m.Assignments.Any(a => a.RefereeId == userId))
            .OrderBy(m => m.MatchDate)
            .ToListAsync(ct);

        return Ok(matches.Select(m => new
        {
            id = m.Id,
            matchDate = m.MatchDate,
            homeTeam = m.HomeTeam?.Name ?? "?",
            awayTeam = m.AwayTeam?.Name ?? "?",
            location = m.Location,
            team = m.Assignments.OrderBy(a => a.RoleType).Select(a => new
            {
                role = a.RoleType,
                name = a.Referee?.DisplayName ?? a.Referee?.UserName ?? "Unknown",
            }),
        }));
    }

    [HttpGet("unavailability")]
    public async Task<IActionResult> ListUnavailability(CancellationToken ct)
    {
        await UnavailabilityMaintenance.DeleteExpiredAsync(_context, DateTime.UtcNow.Date, ct);

        var userId = _userManager.GetUserId(User);
        var items = await _context.Unavailabilities
            .Where(u => u.RefereeId == userId)
            .OrderBy(u => u.StartDate)
            .ToListAsync(ct);
        return Ok(items.Select(UnavailabilityDto.From));
    }

    [HttpGet("unavailability/{id:int}")]
    public async Task<IActionResult> GetUnavailability(int id, CancellationToken ct)
    {
        var item = await FindMineAsync(id, ct);
        return item == null ? NotFound(new { error = "That period no longer exists." }) : Ok(UnavailabilityDto.From(item));
    }

    [HttpPost("unavailability")]
    public async Task<IActionResult> CreateUnavailability([FromBody] UnavailabilityRequest request, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User)!;
        var problem = await CheckAsync(request, userId, null, ct);
        if (problem != null) return problem;

        var item = new Unavailability
        {
            RefereeId = userId,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Reason = request.Reason,
        };
        _context.Unavailabilities.Add(item);
        await _context.SaveChangesAsync(ct);
        return Ok(UnavailabilityDto.From(item));
    }

    [HttpPut("unavailability/{id:int}")]
    public async Task<IActionResult> UpdateUnavailability(int id, [FromBody] UnavailabilityRequest request, CancellationToken ct)
    {
        var item = await FindMineAsync(id, ct);
        if (item == null) return NotFound(new { error = "That period no longer exists." });

        var problem = await CheckAsync(request, item.RefereeId, id, ct);
        if (problem != null) return problem;

        item.StartDate = request.StartDate.Date;
        item.EndDate = request.EndDate.Date;
        item.Reason = request.Reason;
        await _context.SaveChangesAsync(ct);
        return Ok(UnavailabilityDto.From(item));
    }

    [HttpDelete("unavailability/{id:int}")]
    public async Task<IActionResult> DeleteUnavailability(int id, CancellationToken ct)
    {
        var item = await FindMineAsync(id, ct);
        if (item == null) return NotFound(new { error = "That period no longer exists." });

        _context.Unavailabilities.Remove(item);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<Unavailability?> FindMineAsync(int id, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User);
        return _context.Unavailabilities.FirstOrDefaultAsync(u => u.Id == id && u.RefereeId == userId, ct);
    }

    /// <summary>Date order and overlap rules shared by create and edit. Returns a 400 response, or null when fine.</summary>
    private async Task<IActionResult?> CheckAsync(UnavailabilityRequest request, string refereeId, int? excludeId, CancellationToken ct)
    {
        if (request.EndDate.Date < request.StartDate.Date)
        {
            ModelState.AddModelError(nameof(UnavailabilityRequest.EndDate), "End date must be on or after start date.");
            return ValidationProblem(ModelState);
        }

        var overlaps = await _context.Unavailabilities.AnyAsync(u =>
            u.RefereeId == refereeId &&
            (excludeId == null || u.Id != excludeId) &&
            u.StartDate <= request.EndDate.Date &&
            u.EndDate >= request.StartDate.Date, ct);
        return overlaps ? BadRequest(new { error = "This period overlaps an existing unavailability." }) : null;
    }
}

public class UnavailabilityRequest
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public record UnavailabilityDto(int Id, DateTime StartDate, DateTime EndDate, string? Reason)
{
    public static UnavailabilityDto From(Unavailability u) => new(u.Id, u.StartDate, u.EndDate, u.Reason);
}
