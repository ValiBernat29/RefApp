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

public class UnavailabilityController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;

    public UnavailabilityController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        await UnavailabilityMaintenance.DeleteExpiredAsync(_context, today, cancellationToken);

        var roster = await _context.Unavailabilities
            .Include(u => u.Referee)
            // Prioritize UserName for sorting the roster
            .OrderBy(u => u.Referee!.DisplayName ?? u.Referee.UserName ?? u.Referee.Email)
            .ThenBy(u => u.StartDate)
            .ToListAsync(cancellationToken);
        return View(roster);
    }
}
