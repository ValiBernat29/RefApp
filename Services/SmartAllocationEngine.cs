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

    // ── Penalty weights ───────────────────────────────────────────────────────
    private const double Over40KmPenalty          = 500.0;
    private const double KmPenaltyMultiplier       = 1.0;   // 1 pt per km
    private const double WorkloadPenaltyMultiplier = 30.0;  // 30 pts per monthly assignment
    private const double RoleMatchBonus            = 15.0;  // −15 pts when preferred role matches
    private const double WrongRolePenalty          = 800.0; // +800 pts wrong role — strong avoidance
    private const double SameTeamMonthlyPenalty    = 300.0; // +300 pts >2× same team in 30 days
    private const double MissingCoordsDefaultPenalty = 20.0;
    private const double DummyRefereePenalty       = 10000.0;
    private const double NoCarPenalty              = 800.0;

    // Spread penalty: discourages assigning refs from opposite ends of the county
    // Applied per-match at base case as: SpreadPenaltyWeight × avgPairwiseKm
    private const double SpreadPenaltyWeight       = 3.0;

    // "On the way" bonus: reward if a ref is roughly on the route of another ref to the match
    // Bonus per eligible pair (max 3 pairs per match)
    private const double OnTheWayBonus             = 40.0;

    // Frequency penalty table: index = combined recent match count for either team (last 60 days)
    private static readonly double[] FrequencyPenalty = { 0, 15, 35, 60, 1000, 1000 };

    // Hard gap: referees must have at least this many hours between assigned matches
    private const double MinHoursBetweenMatches    = 5.0;

    // City conflict: refs cannot officiate teams from their own city (Arad refs are exempt)
    private const string AradCityName             = "Arad";

    private const int MaxSearchStates = 50000;

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

        var historyStart  = startDate.AddDays(-60);
        var workloadStart = new DateTime(startDate.Year, startDate.Month, 1);
        var fetchStart    = historyStart < workloadStart ? historyStart : workloadStart;

        var pastAssignments = await _context.MatchAssignments
            .Include(a => a.Match).ThenInclude(m => m.HomeTeam)
            .Include(a => a.Match).ThenInclude(m => m.AwayTeam)
            .Where(a => a.Match.MatchDate >= fetchStart && a.Match.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        // ── Step 2: Build Solver State ─────────────────────────────────────────

        var thirtyDaysAgo = startDate.AddDays(-30);
        var sixtyDaysAgo  = startDate.AddDays(-60);

        var solverRefs = new List<SolverReferee>();
        foreach (var r in referees)
        {
            var solverRef = new SolverReferee
            {
                Id            = r.Id,
                Name          = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Rank          = r.Rank,
                PreferredRole = r.PreferredRole,
                Latitude      = r.Latitude,
                Longitude     = r.Longitude,
                HasCar        = r.HasCar,
                HomeCity      = (r.HomeCity ?? "").Trim()
            };

            foreach (var u in unavailabilities.Where(u => u.RefereeId == r.Id))
                solverRef.Unavailabilities.Add((u.StartDate.Date, u.EndDate.Date));

            foreach (var teamId in refusals.Where(rf => rf.RefereeId == r.Id).Select(rf => rf.TeamId))
                solverRef.BlockedTeamIds.Add(teamId);

            var refPast = pastAssignments.Where(a => a.RefereeId == r.Id && a.Match.MatchDate < startDate);
            foreach (var a in refPast)
            {
                void AddDate(int teamId, DateTime d)
                {
                    if (!solverRef.PastOfficiatedTeams.ContainsKey(teamId))
                        solverRef.PastOfficiatedTeams[teamId] = new List<DateTime>();
                    solverRef.PastOfficiatedTeams[teamId].Add(d);
                }
                AddDate(a.Match.HomeTeamId, a.Match.MatchDate);
                AddDate(a.Match.AwayTeamId, a.Match.MatchDate);
            }

            var refWorkloads = pastAssignments
                .Where(a => a.RefereeId == r.Id)
                .GroupBy(a => a.Match.MatchDate.ToString("yyyy_MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in refWorkloads) solverRef.MonthWorkload[kvp.Key] = kvp.Value;

            // Frequency per team over last 60 days
            var recentByTeam = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= sixtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { a.Match.HomeTeamId, a.Match.AwayTeamId })
                .GroupBy(tid => tid)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in recentByTeam) solverRef.RecentTeamMatchCount[kvp.Key] = kvp.Value;

            // Same-team count over last 30 days
            var monthlyByTeam = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= thirtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { a.Match.HomeTeamId, a.Match.AwayTeamId })
                .GroupBy(tid => tid)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in monthlyByTeam) solverRef.SameTeamMonthlyCount[kvp.Key] = kvp.Value;

            solverRefs.Add(solverRef);
        }

        // Sort matches: most constrained league first, then chronological
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
            if (currentPenalty >= bestPenalty) return;

            if (vIdx == variables.Count)
            {
                // Base case: add per-match penalties that require all 3 slots to be known
                double finalPenalty = currentPenalty;

                for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
                {
                    var m       = sortedMatches[mIdx];
                    var refIds  = new[] { currentAssignment[mIdx * 3], currentAssignment[mIdx * 3 + 1], currentAssignment[mIdx * 3 + 2] };
                    var refObjs = refIds.Where(id => id != null).Select(id => solverRefs.First(r => r.Id == id)).ToList();

                    // Car penalty
                    if (!refObjs.Any(r => r.HasCar) && refObjs.Count > 0)
                        finalPenalty += NoCarPenalty;

                    // Spread penalty: average pairwise distance between refs' home locations
                    if (refObjs.Count >= 2 && refObjs.All(r => r.Latitude.HasValue && r.Longitude.HasValue))
                    {
                        var pairs = new List<double>();
                        for (int i = 0; i < refObjs.Count; i++)
                            for (int j = i + 1; j < refObjs.Count; j++)
                                pairs.Add(HaversineKm(
                                    refObjs[i].Latitude!.Value, refObjs[i].Longitude!.Value,
                                    refObjs[j].Latitude!.Value, refObjs[j].Longitude!.Value));
                        finalPenalty += SpreadPenaltyWeight * pairs.Average();
                    }

                    // "On the way" bonus: for each pair (A, B) going to match at C,
                    // bonus if A is roughly on the route from B to C.
                    // Condition: dist(B→A) + dist(A→C) ≤ 1.25 × dist(B→C)  (within 25% detour)
                    if (m.HomeTeam?.Latitude.HasValue == true && m.HomeTeam.Longitude.HasValue)
                    {
                        double mLat = m.HomeTeam.Latitude!.Value, mLon = m.HomeTeam.Longitude!.Value;
                        for (int i = 0; i < refObjs.Count; i++)
                        {
                            for (int j = 0; j < refObjs.Count; j++)
                            {
                                if (i == j) continue;
                                var rA = refObjs[i]; var rB = refObjs[j];
                                if (!rA.Latitude.HasValue || !rB.Latitude.HasValue) continue;

                                double dBC = HaversineKm(rB.Latitude!.Value, rB.Longitude!.Value, mLat, mLon);
                                double dBA = HaversineKm(rB.Latitude!.Value, rB.Longitude!.Value, rA.Latitude!.Value, rA.Longitude!.Value);
                                double dAC = HaversineKm(rA.Latitude!.Value, rA.Longitude!.Value, mLat, mLon);

                                // If going via A costs ≤25% more than going direct, A is "on the way"
                                if (dBC > 1.0 && (dBA + dAC) <= 1.25 * dBC)
                                    finalPenalty -= OnTheWayBonus; // reward
                            }
                        }
                    }
                }

                if (finalPenalty < bestPenalty)
                {
                    bestPenalty = finalPenalty;
                    Array.Copy(currentAssignment, bestAssignment, variables.Count);
                }
                return;
            }

            var v     = variables[vIdx];
            var match = v.Match;
            var role  = v.Role;

            // Refs already assigned to this match in current branch
            var assignedHere = new List<string>();
            for (int i = 0; i < vIdx; i++)
                if (variables[i].Match.Id == match.Id && currentAssignment[i] != null)
                    assignedHere.Add(currentAssignment[i]!);

            var candidates = new List<(SolverReferee? Ref, double LocalPenalty)>();

            foreach (var r in solverRefs)
            {
                // ── Hard Constraints ──────────────────────────────────────────

                if (assignedHere.Contains(r.Id)) continue;
                if (r.IsUnavailable(match.MatchDate)) continue;

                // 5-hour scheduling gap
                if (r.AssignedTimes.Any(t => Math.Abs((match.MatchDate - t).TotalHours) < MinHoursBetweenMatches))
                    continue;

                if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId))
                    continue;

                if (!MeetsRankRequirement(r.Rank, match.HomeTeam!.League)) continue;

                // 21-day history conflict (hard)
                if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                    HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate))
                    continue;

                // City conflict: non-Arad refs cannot officiate teams from their own city
                if (!string.IsNullOrWhiteSpace(r.HomeCity) &&
                    !r.HomeCity.Equals(AradCityName, StringComparison.OrdinalIgnoreCase))
                {
                    var homeCity = match.HomeTeam?.City?.Trim() ?? "";
                    var awayCity = match.AwayTeam?.City?.Trim() ?? "";
                    if ((!string.IsNullOrEmpty(homeCity) && homeCity.Equals(r.HomeCity, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(awayCity) && awayCity.Equals(r.HomeCity, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                // ── Soft Penalties ────────────────────────────────────────────

                double penalty = 0.0;

                // 1. Travel distance
                if (r.Latitude.HasValue && r.Longitude.HasValue &&
                    match.HomeTeam!.Latitude.HasValue && match.HomeTeam!.Longitude.HasValue)
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

                    penalty += wrongRole ? WrongRolePenalty : -RoleMatchBonus;
                }

                // 4. Frequency (last 60 days, either team)
                var recentTotal = Math.Min(
                    (r.RecentTeamMatchCount.TryGetValue(match.HomeTeamId, out var rh) ? rh : 0) +
                    (r.RecentTeamMatchCount.TryGetValue(match.AwayTeamId, out var ra) ? ra : 0),
                    FrequencyPenalty.Length - 1);
                penalty += FrequencyPenalty[recentTotal];

                // 5. Same-team 30-day overexposure (>2× per team)
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

        var warnings = new List<string>();
        int fullyAssigned = 0, partiallyAssigned = 0;
        double totalTravelDist = 0.0;
        int assignedRolesCount = 0, noCarWarnings = 0;

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
                if (referees.FirstOrDefault(ref_ => ref_.Id == refId)?.HasCar == true) { matchHasCar = true; break; }
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
            TotalMatchesToAssign          = sortedMatches.Count,
            FullyAssignedMatchesCount     = fullyAssigned,
            PartiallyAssignedMatchesCount = partiallyAssigned,
            TotalTravelDistanceKm         = Math.Round(totalTravelDist, 1),
            AvgTravelDistanceKm           = assignedRolesCount > 0 ? Math.Round(totalTravelDist / assignedRolesCount, 1) : 0,
            NoCarWarningsCount            = noCarWarnings,
            Warnings                      = warnings
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
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
        /// <summary>Referee's home city/village, used for the city-conflict hard constraint.</summary>
        public string HomeCity { get; set; } = string.Empty;

        public HashSet<int> BlockedTeamIds { get; set; } = new();
        public List<DateTime> AssignedTimes { get; set; } = new();
        public List<(DateTime Start, DateTime End)> Unavailabilities { get; set; } = new();
        public Dictionary<int, List<DateTime>> PastOfficiatedTeams { get; set; } = new();
        public Dictionary<string, int> MonthWorkload { get; set; } = new();
        public Dictionary<int, int> RecentTeamMatchCount { get; set; } = new();
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
