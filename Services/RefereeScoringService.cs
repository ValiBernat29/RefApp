using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.ViewModels;

namespace RefApp.Services;

/// <summary>
/// Computes a suitability score for each referee candidate for a given match.
/// Higher score = better candidate. Board always makes the final call.
/// </summary>
public class RefereeScoringService
{
    private readonly ApplicationDbContext _context;

    // ── Scoring weights ───────────────────────────────────────────────────
    // Distance: lose 1 point per 5 km (configurable)
    private const double KmPenaltyPerKm = 0.2;

    // Frequency: how many of last N matches for either team this ref did
    private const int RecentMatchWindow = 5;
    private static readonly double[] FrequencyPenalty = { 0, 15, 35, 60, 1000, 1000 }; // index = count

    // Refusal: effectively removes the ref from consideration
    private const double RefusalPenalty = 1000;

    public RefereeScoringService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns <paramref name="candidates"/> sorted by suitability (best first),
    /// with all scoring metadata populated on each <see cref="RefereeOption"/>.
    /// </summary>
    public async Task<List<RefereeOption>> ScoreAndSortAsync(
        List<RefereeOption> candidates,
        Match match,
        List<ApplicationUser> refereeUsers)
    {
        if (match.HomeTeam == null || match.AwayTeam == null)
            return candidates; // can't score without navigation props loaded

        var homeTeamId = match.HomeTeamId;
        var awayTeamId = match.AwayTeamId;

        // ── Pre-fetch data in bulk ─────────────────────────────────────────

        // Refusals for both teams in this match
        var refusalsByTeam = await _context.TeamRefereeRefusals
            .Where(r => r.TeamId == homeTeamId || r.TeamId == awayTeamId)
            .ToListAsync();

        // Last N completed match assignments for each team
        var recentHomeAssignments = await _context.MatchAssignments
            .Include(a => a.Match)
            .Where(a => (a.Match.HomeTeamId == homeTeamId || a.Match.AwayTeamId == homeTeamId)
                     && a.Match.MatchDate < match.MatchDate)
            .OrderByDescending(a => a.Match.MatchDate)
            .Take(RecentMatchWindow * 3)  // fetch a bit extra to cover all 3 roles
            .Select(a => a.RefereeId)
            .ToListAsync();

        var recentAwayAssignments = await _context.MatchAssignments
            .Include(a => a.Match)
            .Where(a => (a.Match.HomeTeamId == awayTeamId || a.Match.AwayTeamId == awayTeamId)
                     && a.Match.MatchDate < match.MatchDate)
            .OrderByDescending(a => a.Match.MatchDate)
            .Take(RecentMatchWindow * 3)
            .Select(a => a.RefereeId)
            .ToListAsync();

        var refereeLookup = refereeUsers.ToDictionary(u => u.Id);

        // Match location coords (from HomeTeam)
        double? matchLat = match.HomeTeam.Latitude;
        double? matchLon = match.HomeTeam.Longitude;

        // ── Score each candidate ──────────────────────────────────────────
        foreach (var option in candidates)
        {
            double score = 100.0;

            // --- Rule 1: Distance ---
            if (refereeLookup.TryGetValue(option.Id, out var refUser)
                && refUser.Latitude.HasValue && refUser.Longitude.HasValue
                && matchLat.HasValue && matchLon.HasValue)
            {
                var km = GeocodingService.HaversineKm(
                    refUser.Latitude.Value, refUser.Longitude.Value,
                    matchLat.Value, matchLon.Value);
                option.DistanceKm = Math.Round(km, 1);
                score -= km * KmPenaltyPerKm;
            }

            // --- Rule 2: Frequency (Home + Away combined) ---
            var recentCount = recentHomeAssignments.Count(id => id == option.Id)
                            + recentAwayAssignments.Count(id => id == option.Id);
            recentCount = Math.Min(recentCount, FrequencyPenalty.Length - 1);
            option.RecentMatchCount = recentCount;
            score -= FrequencyPenalty[recentCount];

            // --- Rule 3: Refusals ---
            option.IsRefusedByHomeTeam = refusalsByTeam
                .Any(r => r.TeamId == homeTeamId && r.RefereeId == option.Id);
            option.IsRefusedByAwayTeam = refusalsByTeam
                .Any(r => r.TeamId == awayTeamId && r.RefereeId == option.Id);

            if (option.IsRefusedByHomeTeam || option.IsRefusedByAwayTeam)
                score -= RefusalPenalty;

            option.SuitabilityScore = Math.Round(score, 2);
        }

        // Sort: refused refs last, then by score descending
        return candidates
            .OrderBy(o => o.IsRefusedByHomeTeam || o.IsRefusedByAwayTeam ? 1 : 0)
            .ThenByDescending(o => o.SuitabilityScore)
            .ToList();
    }
}
