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

public class AllocationController : BoardControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISmartAllocationEngine _allocationEngine;

    public AllocationController(
        ApplicationDbContext context,
        ISmartAllocationEngine allocationEngine)
    {
        _context = context;
        _allocationEngine = allocationEngine;
    }

    // ── Smart Auto-Allocation ────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Index()
    {
        var today = DateTime.UtcNow.Date;
        ViewBag.DefaultStart = today.ToString("yyyy-MM-dd");
        ViewBag.DefaultEnd = today.AddDays(7).ToString("yyyy-MM-dd");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (endDate < startDate)
        {
            TempData["AllocError"] = "End date must be on or after the start date.";
            ViewBag.DefaultStart = startDate.ToString("yyyy-MM-dd");
            ViewBag.DefaultEnd = endDate.ToString("yyyy-MM-dd");
            return View();
        }

        var result = await _allocationEngine.AllocateRefereesAsync(
            startDate.Date,
            endDate.Date.AddDays(1).AddTicks(-1),   // include the full end day
            cancellationToken);

        // Reload the assigned matches so we can display them in the results view
        var assignedMatches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .ThenInclude(a => a.Referee)
            .Where(m => m.MatchDate >= startDate.Date &&
                        m.MatchDate <= endDate.Date.AddDays(1).AddTicks(-1))
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);

        ViewBag.AllocationResult = result;
        ViewBag.AssignedMatches = assignedMatches;
        ViewBag.StartDate = startDate.ToString("dd MMM yyyy");
        ViewBag.EndDate = endDate.ToString("dd MMM yyyy");
        return View("AutoAllocateResult");
    }
}
