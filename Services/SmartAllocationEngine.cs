using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;

namespace RefApp.Services;

public class SmartAllocationEngine : ISmartAllocationEngine
{
    private readonly ApplicationDbContext _context;

    // ── Soft constraint penalty weights ───────────────────────────────────────
    private const double Over40KmPenalty          = 500.0;  // per-assignment if >40 km
    private const double KmPenaltyMultiplier       = 1.0;   // 1 pt per km
    private const double WorkloadPenaltyMultiplier = 30.0;  // 30 pts per monthly assignment
    private const double RoleMatchBonus            = 15.0;  // −15 pts when preferred role matches
    private const double WrongRolePenalty          = 300.0; // +300 pts when wrong role assigned (avoid unless no choice)
    private const double SameTeamMonthlyPenalty    = 300.0; // +300 pts if ref officiated same team >2× in past 30 days
    private const double MissingCoordsDefaultPenalty = 20.0;
    private const double DummyRefereePenalty       = 10000.0;
    private const double NoCarPenalty              = 800.0; // per match with no car-equipped referee

    // Frequency penalty table: index = number of recent matches for either team
    private static readonly double[] FrequencyPenalty = { 0, 15, 35, 60, 1000, 1000 };

    // ── Scheduling gap ────────────────────────────────────────────────────────
    /// <summary>Hard gap in hours: a referee cannot be assigned two matches within this window.</summary>
    private const double MinHoursBetweenMatches = 5.0;

    private const int MaxSearchStates = 20000;

    public SmartAllocationEngine(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AllocationResult> AllocateRefereesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // ── Step 1: Bulk Fetch ────────────────────────────────────────────────

        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .Where(m => m.MatchDate >= startDate && m.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        if (!matches.Any())
            return new AllocationResult { Success = true, Message = "No matches found in the specified date range.", TotalMatchesToAssign = 0 };

        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .ToListAsync(cancellationToken);

        var unavailabilities = await _context.Unavailabilities
            .Where(u => u.StartDate <= endDate && u.EndDate >= startDate)
            .ToListAsync(cancellationToken);

        var refusals = await _context.TeamRefereeRefusals.ToListAsync(cancellationToken);

        // Fetch history for: 21-day check, workload, frequency scoring (60 days), monthly team count (30 days)
        var historyStart   = startDate.AddDays(-60); // widest window needed
        var workloadStart  = new DateTime(startDate.Year, startDate.Month, 1);
        var fetchStart     = historyStart < workloadStart ? historyStart : workloadStart;

        var pastAssignments = await _context.MatchAssignments
            .Include(a => a.Match).ThenInclude(m => m.HomeTeam)
            .Include(a => a.Match).ThenInclude(m => m.AwayTeam)
            .Where(a => a.Match.MatchDate >= fetchStart && a.Match.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        // ── Step 2: Build Solver State ─────────────────────────────────────────

        var thirtyDaysAgo  = startDate.AddDays(-30);
        var twentyOneDaysAgo = startDate.AddDays(-21);
        var sixtyDaysAgo   = startDate.AddDays(-60);

        var solverRefs = new List<SolverReferee>();
        foreach (var r in referees)
        {
            var solverRef = new SolverReferee
            {
                Id           = r.Id,
                Name         = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Rank         = r.Rank,
                PreferredRole = r.PreferredRole,
                Latitude     = r.Latitude,
                Longitude    = r.Longitude,
                HasCar       = r.HasCar
            };

            // Unavailabilities
            foreach (var u in unavailabilities.Where(u => u.RefereeId == r.Id))
                solverRef.Unavailabilities.Add((u.StartDate.Date, u.EndDate.Date));

            // Blocked teams (refusals)
            foreach (var teamId in refusals.Where(rf => rf.RefereeId == r.Id).Select(rf => rf.TeamId))
                solverRef.BlockedTeamIds.Add(teamId);

            // Past officiated history (for 21-day hard check)
            var refPast = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate < startDate)
                .ToList();
            foreach (var a in refPast)
            {
                var d = a.Match.MatchDate;
                if (!solverRef.PastOfficiatedTeams.ContainsKey(a.Match.HomeTeamId))
                    solverRef.PastOfficiatedTeams[a.Match.HomeTeamId] = new List<DateTime>();
                solverRef.PastOfficiatedTeams[a.Match.HomeTeamId].Add(d);

                if (!solverRef.PastOfficiatedTeams.ContainsKey(a.Match.AwayTeamId))
                    solverRef.PastOfficiatedTeams[a.Match.AwayTeamId] = new List<DateTime>();
                solverRef.PastOfficiatedTeams[a.Match.AwayTeamId].Add(d);
            }

            // Monthly workload per calendar month
            var refWorkloads = pastAssignments
                .Where(a => a.RefereeId == r.Id)
                .GroupBy(a => a.Match.MatchDate.ToString("yyyy_MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in refWorkloads)
                solverRef.MonthWorkload[kvp.Key] = kvp.Value;

            // Frequency count per team over last 60 days (for FrequencyPenalty)
            var recentByTeam = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= sixtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { a.Match.HomeTeamId, a.Match.AwayTeamId })
                .GroupBy(tid => tid)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in recentByTeam)
                solverRef.RecentTeamMatchCount[kvp.Key] = kvp.Value;

            // Same-team count over last 30 days (for SameTeamMonthlyPenalty — >2 times)
            var monthlyByTeam = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= thirtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { a.Match.HomeTeamId, a.Match.AwayTeamId })
                .GroupBy(tid => tid)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in monthlyByTeam)
                solverRef.SameTeamMonthlyCount[kvp.Key] = kvp.Value;

            solverRefs.Add(solverRef);
        }

        // Sort matches: most constrained (higher leagues) first, then chronological
        var sortedMatches = matches
            .OrderBy(m => m.HomeTeam?.League == League.L4 ? 0 :
                          (m.HomeTeam?.League == League.L5A || m.HomeTeam?.League == League.L5B || m.HomeTeam?.League == League.L5C ? 1 : 2))
            .ThenBy(m => m.MatchDate)
            .ToList();

        var variables = new List<SolverVariable>();
        int varIndex = 0;
        foreach (var m in sortedMatches)
        {
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Main });
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Assistant1 });
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Assistant2 });
        }

        // ── Step 3: Branch & Bound Search ─────────────────────────────────────

        var currentAssignment = new string?[variables.Count];
        var bestAssignment    = new string?[variables.Count];
        double bestPenalty    = double.MaxValue;
        int stateCount        = 0;

        void Search(int vIdx, double currentPenalty)
        {
            stateCount++;
            if (stateCount > MaxSearchStates) return;

            if (vIdx == variables.Count)
            {
                // Base case: apply per-match car penalty
                double finalPenalty = currentPenalty;
                for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
                {
                    bool hasCar = false;
                    foreach (var id in new[] { currentAssignment[mIdx * 3], currentAssignment[mIdx * 3 + 1], currentAssignment[mIdx * 3 + 2] })
                    {
                        if (id != null && solverRefs.FirstOrDefault(r => r.Id == id)?.HasCar == true)
                        { hasCar = true; break; }
                    }
                    if (!hasCar) finalPenalty += NoCarPenalty;
                }

                if (finalPenalty < bestPenalty)
                {
                    bestPenalty = finalPenalty;
                    Array.Copy(currentAssignment, bestAssignment, variables.Count);
                }
                return;
            }

            if (currentPenalty >= bestPenalty) return;

            var v     = variables[vIdx];
            var match = v.Match;
            var role  = v.Role;

            // Refs already assigned to this match in current branch
            var assignedRefsForThisMatch = new List<string>();
            for (int i = 0; i < vIdx; i++)
                if (variables[i].Match.Id == match.Id && currentAssignment[i] != null)
                    assignedRefsForThisMatch.Add(currentAssignment[i]!);

            var candidates = new List<(SolverReferee? Ref, double LocalPenalty)>();

            foreach (var r in solverRefs)
            {
                // ── Hard Constraints ──────────────────────────────────────────

                if (assignedRefsForThisMatch.Contains(r.Id)) continue;
                if (r.IsUnavailable(match.MatchDate)) continue;

                // 5-hour scheduling gap — hard block
                if (r.AssignedTimes.Any(t => Math.Abs((match.MatchDate - t).TotalHours) < MinHoursBetweenMatches))
                    continue;

                if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId))
                    continue;

                if (!MeetsRankRequirement(r.Rank, match.HomeTeam.League)) continue;

                if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                    HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate))
                    continue;

                // ── Soft Penalties ────────────────────────────────────────────

                double penalty = 0.0;

                // 1. Travel distance
                if (r.Latitude.HasValue && r.Longitude.HasValue &&
                    match.HomeTeam.Latitude.HasValue && match.HomeTeam.Longitude.HasValue)
                {
                    var dist = HaversineKm(r.Latitude.Value, r.Longitude.Value,
                                           match.HomeTeam.Latitude.Value, match.HomeTeam.Longitude.Value);
                    penalty += dist * KmPenaltyMultiplier;
                    if (dist > 40.0) penalty += Over40KmPenalty;
                }
                else penalty += MissingCoordsDefaultPenalty;

                // 2. Workload balance
                var monthKey = match.MatchDate.ToString("yyyy_MM");
                r.MonthWorkload.TryGetValue(monthKey, out var w);
                penalty += (w + 1) * WorkloadPenaltyMultiplier;

                // 3. Role preference — strong penalty for wrong role, bonus for correct
                if (r.PreferredRole != RefereePreferredRole.None)
                {
                    bool wrongRole =
                        (role == MatchRoleType.Main && r.PreferredRole == RefereePreferredRole.AssistantReferee) ||
                        ((role == MatchRoleType.Assistant1 || role == MatchRoleType.Assistant2) && r.PreferredRole == RefereePreferredRole.MainReferee);

                    if (wrongRole)
                        penalty += WrongRolePenalty;  // only assigned when no other choice
                    else
                        penalty -= RoleMatchBonus;
                }

                // 4. Frequency (last 60 days, either team)
                var recentTotal = Math.Min(
                    (r.RecentTeamMatchCount.TryGetValue(match.HomeTeamId, out var rh) ? rh : 0) +
                    (r.RecentTeamMatchCount.TryGetValue(match.AwayTeamId, out var ra) ? ra : 0),
                    FrequencyPenalty.Length - 1);
                penalty += FrequencyPenalty[recentTotal];

                // 5. Same-team monthly overexposure (>2 times in last 30 days per team)
                var monthlyHome = r.SameTeamMonthlyCount.TryGetValue(match.HomeTeamId, out var mh) ? mh : 0;
                var monthlyAway = r.SameTeamMonthlyCount.TryGetValue(match.AwayTeamId, out var ma) ? ma : 0;
                if (monthlyHome > 2 || monthlyAway > 2)
                    penalty += SameTeamMonthlyPenalty;

                candidates.Add((r, penalty));
            }

            candidates.Add((null, DummyRefereePenalty)); // unassigned fallback

            foreach (var c in candidates.OrderBy(c => c.LocalPenalty))
            {
                if (c.Ref != null)
                {
                    c.Ref.AssignedTimes.Add(match.MatchDate);

                    if (!c.Ref.PastOfficiatedTeams.ContainsKey(match.HomeTeamId))
                        c.Ref.PastOfficiatedTeams[match.HomeTeamId] = new List<DateTime>();
                    c.Ref.PastOfficiatedTeams[match.HomeTeamId].Add(match.MatchDate);

                    if (!c.Ref.PastOfficiatedTeams.ContainsKey(match.AwayTeamId))
                        c.Ref.PastOfficiatedTeams[match.AwayTeamId] = new List<DateTime>();
                    c.Ref.PastOfficiatedTeams[match.AwayTeamId].Add(match.MatchDate);

                    var mk = match.MatchDate.ToString("yyyy_MM");
                    c.Ref.MonthWorkload.TryGetValue(mk, out var mw);
                    c.Ref.MonthWorkload[mk] = mw + 1;

                    c.Ref.RecentTeamMatchCount.TryGetValue(match.HomeTeamId, out var fh);
                    c.Ref.RecentTeamMatchCount[match.HomeTeamId] = fh + 1;
                    c.Ref.RecentTeamMatchCount.TryGetValue(match.AwayTeamId, out var fa);
                    c.Ref.RecentTeamMatchCount[match.AwayTeamId] = fa + 1;

                    c.Ref.SameTeamMonthlyCount.TryGetValue(match.HomeTeamId, out var smh);
                    c.Ref.SameTeamMonthlyCount[match.HomeTeamId] = smh + 1;
                    c.Ref.SameTeamMonthlyCount.TryGetValue(match.AwayTeamId, out var sma);
                    c.Ref.SameTeamMonthlyCount[match.AwayTeamId] = sma + 1;

                    currentAssignment[vIdx] = c.Ref.Id;
                    Search(vIdx + 1, currentPenalty + c.LocalPenalty);

                    // Revert
                    c.Ref.AssignedTimes.Remove(match.MatchDate);
                    c.Ref.PastOfficiatedTeams[match.HomeTeamId].Remove(match.MatchDate);
                    c.Ref.PastOfficiatedTeams[match.AwayTeamId].Remove(match.MatchDate);
                    c.Ref.MonthWorkload[mk] = mw;
                    c.Ref.RecentTeamMatchCount[match.HomeTeamId] = fh;
                    c.Ref.RecentTeamMatchCount[match.AwayTeamId] = fa;
                    c.Ref.SameTeamMonthlyCount[match.HomeTeamId] = smh;
                    c.Ref.SameTeamMonthlyCount[match.AwayTeamId] = sma;
                }
                else
                {
                    currentAssignment[vIdx] = null;
                    Search(vIdx + 1, currentPenalty + c.LocalPenalty);
                }

                currentAssignment[vIdx] = null;
            }
        }

        Search(0, 0.0);

        // ── Step 4: Persist and Build Result ──────────────────────────────────

        var warnings        = new List<string>();
        int fullyAssigned   = 0;
        int partiallyAssigned = 0;
        double totalTravelDist = 0.0;
        int assignedRolesCount = 0;
        int noCarWarnings   = 0;

        for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
        {
            var match      = sortedMatches[mIdx];
            var mainRefId  = bestAssignment[mIdx * 3];
            var asst1RefId = bestAssignment[mIdx * 3 + 1];
            var asst2RefId = bestAssignment[mIdx * 3 + 2];

            int cnt = (mainRefId != null ? 1 : 0) + (asst1RefId != null ? 1 : 0) + (asst2RefId != null ? 1 : 0);
            if (cnt == 3) fullyAssigned++;
            else if (cnt > 0) partiallyAssigned++;

            if (mainRefId  == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Main Referee assigned.");
            if (asst1RefId == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Assistant 1 assigned.");
            if (asst2RefId == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Assistant 2 assigned.");

            bool matchHasCar = false;
            foreach (var refId in new[] { mainRefId, asst1RefId, asst2RefId })
            {
                if (refId == null) continue;
                var r = referees.FirstOrDefault(ref_ => ref_.Id == refId);
                if (r?.HasCar == true) { matchHasCar = true; break; }
            }
            if (!matchHasCar && cnt > 0)
            {
                noCarWarnings++;
                warnings.Add($"⚠ Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: no referee with a car assigned.");
            }

            foreach (var refId in new[] { mainRefId, asst1RefId, asst2RefId })
            {
                if (refId == null) continue;
                var r = referees.FirstOrDefault(ref_ => ref_.Id == refId);
                if (r?.Latitude.HasValue == true && r.Longitude.HasValue &&
                    match.HomeTeam?.Latitude.HasValue == true && match.HomeTeam.Longitude.HasValue)
                {
                    totalTravelDist += HaversineKm(r.Latitude.Value, r.Longitude.Value,
                                                    match.HomeTeam.Latitude.Value, match.HomeTeam.Longitude.Value);
                    assignedRolesCount++;
                }
            }

            _context.MatchAssignments.RemoveRange(match.Assignments);
            if (mainRefId  != null) _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = mainRefId,  RoleType = MatchRoleType.Main });
            if (asst1RefId != null) _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst1RefId, RoleType = MatchRoleType.Assistant1 });
            if (asst2RefId != null) _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst2RefId, RoleType = MatchRoleType.Assistant2 });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AllocationResult
        {
            Success = true,
            Message = $"Auto-allocation complete. Processed {sortedMatches.Count} matches.",
            TotalMatchesToAssign    = sortedMatches.Count,
            FullyAssignedMatchesCount     = fullyAssigned,
            PartiallyAssignedMatchesCount = partiallyAssigned,
            TotalTravelDistanceKm   = Math.Round(totalTravelDist, 1),
            AvgTravelDistanceKm     = assignedRolesCount > 0 ? Math.Round(totalTravelDist / assignedRolesCount, 1) : 0,
            NoCarWarningsCount      = noCarWarnings,
            Warnings                = warnings
        };
    }

    // ── Helper methods ─────────────────────────────────────────────────────────

    private static bool MeetsRankRequirement(RefereeRank rank, League league)
    {
        if (rank == RefereeRank.None)  return false;
        if (rank == RefereeRank.Above) return true;
        return league == League.L4
            ? rank == RefereeRank.L4
            : rank == RefereeRank.L4 || rank == RefereeRank.L6_5;
    }

    private static bool HasRecentHistoryConflict(SolverReferee r, int teamId, DateTime matchDate)
    {
        if (r.PastOfficiatedTeams.TryGetValue(teamId, out var dates))
            foreach (var d in dates)
                if (Math.Abs((matchDate.Date - d.Date).Days) <= 21) return true;
        return false;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    // ── Solver internal classes ────────────────────────────────────────────────

    private class SolverReferee
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RefereeRank Rank { get; set; }
        public RefereePreferredRole PreferredRole { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool HasCar { get; set; }

        public HashSet<int> BlockedTeamIds { get; set; } = new();
        /// <summary>Actual DateTimes of matches already assigned in the current search branch.</summary>
        public List<DateTime> AssignedTimes { get; set; } = new();
        public List<(DateTime Start, DateTime End)> Unavailabilities { get; set; } = new();
        public Dictionary<int, List<DateTime>> PastOfficiatedTeams { get; set; } = new();
        public Dictionary<string, int> MonthWorkload { get; set; } = new();
        /// <summary>Count of recent matches (last ~60 days) per team, for FrequencyPenalty.</summary>
        public Dictionary<int, int> RecentTeamMatchCount { get; set; } = new();
        /// <summary>Count of assignments per team in the last 30 days, for SameTeamMonthlyPenalty.</summary>
        public Dictionary<int, int> SameTeamMonthlyCount { get; set; } = new();

        public bool IsUnavailable(DateTime matchDate)
        {
            var d = matchDate.Date;
            foreach (var interval in Unavailabilities)
                if (d >= interval.Start.Date && d <= interval.End.Date) return true;
            return false;
        }
    }

    private class SolverVariable
    {
        public int Index { get; set; }
        public Match Match { get; set; } = null!;
        public MatchRoleType Role { get; set; }
    }
}
