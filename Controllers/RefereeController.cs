using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;

namespace RefApp.Controllers;

[Authorize(Roles = "Referee")]
public class RefereeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public RefereeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task DeleteExpiredUnavailabilitiesAsync(DateTime todayUtcDate, CancellationToken cancellationToken)
    {
        await _context.Unavailabilities
            .Where(u => u.EndDate < todayUtcDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<string?> GetCurrentRefereeIdAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> MyMatches(CancellationToken cancellationToken)
    {
        // Preluăm ID-ul arbitrului logat
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Challenge();

        // Găsim meciurile lui, dar aducem și TOATE delegările pentru acele meciuri
        var myMatches = await _context.Matches
            .Include(m => m.Assignments)
                .ThenInclude(a => a.Referee) // Tragem și datele colegilor din baza de date
            .Where(m => m.Assignments.Any(a => a.RefereeId == userId))
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);

        return View(myMatches);
    }

    public async Task<IActionResult> MyAvailability(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        await DeleteExpiredUnavailabilitiesAsync(today, cancellationToken);

        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        var list = await _context.Unavailabilities
            .Where(u => u.RefereeId == refereeId)
            .OrderBy(u => u.StartDate)
            .ToListAsync(cancellationToken);
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateUnavailability()
    {
        var today = DateTime.Today;
        return View(new Unavailability
        {
            StartDate = today,
            EndDate = today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUnavailability([FromForm] Unavailability model, CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError("EndDate", "End date must be on or after start date.");
        }

        var hasOverlap = await _context.Unavailabilities
            .AnyAsync(u =>
                u.RefereeId == refereeId &&
                u.StartDate <= model.EndDate &&
                u.EndDate >= model.StartDate,
                cancellationToken);

        if (hasOverlap)
        {
            ModelState.AddModelError(string.Empty, "This period overlaps an existing unavailability.");
        }

        if (ModelState.IsValid)
        {
            model.RefereeId = refereeId;
            model.Id = 0;
            _context.Unavailabilities.Add(model);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Unavailability added.";
            return RedirectToAction(nameof(MyAvailability));
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditUnavailability(int id, CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        var item = await _context.Unavailabilities.FirstOrDefaultAsync(u => u.Id == id && u.RefereeId == refereeId, cancellationToken);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUnavailability(int id, [FromForm] Unavailability model, CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        if (model.EndDate < model.StartDate)
            ModelState.AddModelError("EndDate", "End date must be on or after start date.");

        var hasOverlap = await _context.Unavailabilities
            .AnyAsync(u =>
                u.RefereeId == refereeId &&
                u.Id != id &&
                u.StartDate <= model.EndDate &&
                u.EndDate >= model.StartDate,
                cancellationToken);

        if (hasOverlap)
        {
            ModelState.AddModelError(string.Empty, "This period overlaps an existing unavailability.");
        }

        var item = await _context.Unavailabilities.FirstOrDefaultAsync(u => u.Id == id && u.RefereeId == refereeId, cancellationToken);
        if (item == null) return NotFound();

        if (ModelState.IsValid)
        {
            item.StartDate = model.StartDate;
            item.EndDate = model.EndDate;
            item.Reason = model.Reason;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Unavailability updated.";
            return RedirectToAction(nameof(MyAvailability));
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DeleteUnavailability(int id, CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        var item = await _context.Unavailabilities
            .FirstOrDefaultAsync(u => u.Id == id && u.RefereeId == refereeId, cancellationToken);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("DeleteUnavailability")]
    public async Task<IActionResult> DeleteUnavailabilityConfirmed(int id, CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        var item = await _context.Unavailabilities.FirstOrDefaultAsync(u => u.Id == id && u.RefereeId == refereeId, cancellationToken);
        if (item == null) return NotFound();
        _context.Unavailabilities.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Unavailability removed.";
        return RedirectToAction(nameof(MyAvailability));
    }
}
