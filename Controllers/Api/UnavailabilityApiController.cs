using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Services;

namespace RefApp.Controllers.Api;

/// <summary>Every referee's blocked dates, for the Board's roster page.</summary>
[Route("api/board/unavailability")]
public class UnavailabilityApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public UnavailabilityApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await UnavailabilityMaintenance.DeleteExpiredAsync(_context, DateTime.UtcNow.Date, ct);

        var roster = await _context.Unavailabilities
            .Include(u => u.Referee)
            .OrderBy(u => u.Referee!.DisplayName ?? u.Referee.UserName ?? u.Referee.Email)
            .ThenBy(u => u.StartDate)
            .ToListAsync(ct);

        return Ok(roster.Select(u => new
        {
            id = u.Id,
            refereeName = u.Referee?.DisplayName ?? u.Referee?.UserName ?? u.Referee?.Email ?? u.RefereeId,
            startDate = u.StartDate,
            endDate = u.EndDate,
            reason = u.Reason,
        }));
    }
}
