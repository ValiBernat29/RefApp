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

    private async Task<string?> GetCurrentRefereeIdAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> MyMatches(CancellationToken cancellationToken)
    {
        var refereeId = await GetCurrentRefereeIdAsync(cancellationToken);
        if (refereeId == null) return Challenge();

        var now = DateTime.UtcNow;
        var assignments = await _context.MatchAssignments
            .Include(a => a.Match)
            .Where(a => a.RefereeId == refereeId)
            .OrderByDescending(a => a.Match!.MatchDate)
            .ToListAsync(cancellationToken);

        var upcoming = assignments.Where(a => a.Match!.MatchDate >= now).OrderBy(a => a.Match!.MatchDate).ToList();
        var past = assignments.Where(a => a.Match!.MatchDate < now).ToList();

        ViewBag.Upcoming = upcoming;
        ViewBag.Past = past;
        return View();
    }

    public async Task<IActionResult> MyAvailability(CancellationToken cancellationToken)
    {
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
        return View(new Unavailability());
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
