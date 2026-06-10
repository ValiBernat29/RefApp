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

    // â”€â”€ Penalty weights â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const double Over40KmPenalty          = 500.0;
    private const double KmPenaltyMultiplier       = 1.0;
    private const double WorkloadPenaltyMultiplier = 8.0;
    private const double RoleMatchBonus            = 15.0;

    // ROLE PRIORITY: raised from 800 â†’ 5000 so wrong-role is nearly equivalent to leaving unassigned.
    // Combined with LocalSearchNullPenalty, the local search will actively unassign wrong-role refs
    // and then re-fill the slot with the correct-role ref in the next iteration.
    private const double WrongRolePenalty          = 5000.0;

    // When a slot currently holds a wrong-role ref, local search treats NULL as costing only this
    // much â€” less than WrongRolePenalty â€” so it prefers emptying the slot over keeping the wrong ref.
    // The empty slot is then filled with a correct-role ref in the very next local-search pass.
    private const double LocalSearchNullPenalty    = 2500.0;

    private const double SameTeamMonthlyPenalty    = 300.0;
    private const double MissingCoordsDefaultPenalty = 20.0;
    private const double DummyRefereePenalty       = 10000.0; // hard unassigned (B&B fallback)
    private const double NoCarPenalty              = 800.0;
    private const double SpreadPenaltyWeight       = 3.0;
    private const double OnTheWayBonus             = 40.0;

    private static readonly double[] FrequencyPenalty = { 0, 15, 35, 60, 1000, 1000 };

    private const double MinHoursBetweenMatches    = 5.0;
    private const string AradCityName              = "Arad";
    private const int    MaxSearchStates           = 50000;
    private const int    LocalSearchMaxIterations  = 30;

    public SmartAllocationEngine(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AllocationResult> AllocateRefereesAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // â”€â”€ Step 1: Bulk Fetch â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .Where(m => m.MatchDate >= startDate && m.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        if (!matches.Any())
            return new AllocationResult
            {
                Success = true,
                Message = "No matches found in the specified date range.",
                TotalMatchesToAssign = 0
            };

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

        // â”€â”€ Step 2: Build Solver State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        var thirtyDaysAgo = startDate.AddDays(-30);
        var sixtyDaysAgo  = startDate.AddDays(-60);

        var solverRefs = new List<SolverReferee>();
        foreach (var r in referees)
        {
            var sr = new SolverReferee
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
                sr.Unavailabilities.Add((u.StartDate.Date, u.EndDate.Date));

            foreach (var teamId in refusals.Where(rf => rf.RefereeId == r.Id).Select(rf => rf.TeamId))
                sr.BlockedTeamIds.Add(teamId);

            // Past history per team (for 21-day hard block)
            foreach (var a in pastAssignments.Where(a => a.RefereeId == r.Id && a.Match.MatchDate < startDate))
            {
                AddTeamDate(sr.PastOfficiatedTeams, a.Match.HomeTeamId, a.Match.MatchDate);
                AddTeamDate(sr.PastOfficiatedTeams, a.Match.AwayTeamId, a.Match.MatchDate);
            }

            // Historical monthly workload (PAST only â€” not current batch)
            foreach (var g in pastAssignments
                .Where(a => a.RefereeId == r.Id)
                .GroupBy(a => a.Match.MatchDate.ToString("yyyy_MM")))
                sr.MonthWorkload[g.Key] = g.Count();

            // Recent team match count (60-day window, for frequency penalty)
            foreach (var g in pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= sixtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { (TeamId: a.Match.HomeTeamId, Date: a.Match.MatchDate), (TeamId: a.Match.AwayTeamId, Date: a.Match.MatchDate) })
                .GroupBy(x => x.TeamId))
                sr.RecentTeamMatchCount[g.Key] = g.Count();

            // 30-day same-team count
            foreach (var g in pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate >= thirtyDaysAgo && a.Match.MatchDate < startDate)
                .SelectMany(a => new[] { (TeamId: a.Match.HomeTeamId, Date: a.Match.MatchDate), (TeamId: a.Match.AwayTeamId, Date: a.Match.MatchDate) })
                .GroupBy(x => x.TeamId))
                sr.SameTeamMonthlyCount[g.Key] = g.Count();

            solverRefs.Add(sr);
        }

        // Sort matches: most-constrained league first, then chronological
        var sortedMatches = matches
            .OrderBy(m => m.HomeTeam?.League == League.L4 ? 0
                        : (m.HomeTeam?.League is League.L5A or League.L5B or League.L5C ? 1 : 2))
            .ThenBy(m => m.MatchDate)
            .ToList();

        var variables = new List<SolverVariable>();
        for (int mi = 0; mi < sortedMatches.Count; mi++)
        {
            variables.Add(new SolverVariable { Match = sortedMatches[mi], Role = MatchRoleType.Main });
            variables.Add(new SolverVariable { Match = sortedMatches[mi], Role = MatchRoleType.Assistant1 });
            variables.Add(new SolverVariable { Match = sortedMatches[mi], Role = MatchRoleType.Assistant2 });
        }

        int V = variables.Count;

        // â”€â”€ Step 3: Branch & Bound â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        var currentAssignment = new string?[V];
        var bestAssignment    = new string?[V];
        double bestPenalty    = double.MaxValue;
        int stateCount        = 0;

        void Search(int vIdx, double currentPenalty)
        {
            stateCount++;
            if (stateCount > MaxSearchStates) return;
            if (currentPenalty >= bestPenalty) return;

            if (vIdx == V)
            {
                double finalPenalty = currentPenalty
                    + ComputeAllMatchBonuses(sortedMatches, currentAssignment, solverRefs);

                if (finalPenalty < bestPenalty)
                {
                    bestPenalty = finalPenalty;
                    Array.Copy(currentAssignment, bestAssignment, V);
                }
                return;
            }

            var v     = variables[vIdx];
            var match = v.Match;
            var role  = v.Role;
            int mBase = (vIdx / 3) * 3;

            // Refs already assigned to this match in the current branch
            var assignedHere = new HashSet<string>();
            for (int j = mBase; j < vIdx; j++)
                if (currentAssignment[j] != null) assignedHere.Add(currentAssignment[j]!);

            // Compute batch workload counts from the current branch
            var batchWorkload = new Dictionary<string, int>();
            for (int j = 0; j < vIdx; j++)
            {
                var id = currentAssignment[j];
                if (id == null) continue;
                var mk = variables[j].Match.MatchDate.ToString("yyyy_MM");
                if (mk == match.MatchDate.ToString("yyyy_MM"))
                {
                    batchWorkload.TryGetValue(id, out var bw);
                    batchWorkload[id] = bw + 1;
                }
            }

            var candidates = new List<(SolverReferee? Ref, double Penalty)>();

            foreach (var r in solverRefs)
            {
                if (assignedHere.Contains(r.Id)) continue;
                if (r.IsUnavailable(match.MatchDate)) continue;
                if (r.AssignedTimes.Any(t => Math.Abs((match.MatchDate - t).TotalHours) < MinHoursBetweenMatches))
                    continue;
                if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId)) continue;
                if (!MeetsRankRequirement(r.Rank, match.HomeTeam!.League)) continue;
                if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                    HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate)) continue;
                if (HasCityConflict(r, match)) continue;

                double penalty = ComputeSlotPenalty(r, match, role, batchWorkload);
                candidates.Add((r, penalty));
            }

            candidates.Add((null, DummyRefereePenalty));

            // Role-correct refs always explored before wrong-role — belt-and-suspenders on top of WrongRolePenalty=5000.
            foreach (var c in candidates
                .OrderBy(c => c.Ref != null && IsWrongRoleForSlot(c.Ref, role) ? 1 : 0)
                .ThenBy(c => c.Penalty))
            {
                currentAssignment[vIdx] = c.Ref?.Id;

                if (c.Ref != null)
                {
                    c.Ref.AssignedTimes.Add(match.MatchDate);
                    AddTeamDate(c.Ref.PastOfficiatedTeams, match.HomeTeamId, match.MatchDate);
                    AddTeamDate(c.Ref.PastOfficiatedTeams, match.AwayTeamId, match.MatchDate);

                    var mk = match.MatchDate.ToString("yyyy_MM");
                    c.Ref.MonthWorkload.TryGetValue(mk, out var mw);
                    c.Ref.MonthWorkload[mk] = mw + 1;

                    c.Ref.RecentTeamMatchCount.TryGetValue(match.HomeTeamId, out var rh);
                    c.Ref.RecentTeamMatchCount[match.HomeTeamId] = rh + 1;
                    c.Ref.RecentTeamMatchCount.TryGetValue(match.AwayTeamId, out var ra);
                    c.Ref.RecentTeamMatchCount[match.AwayTeamId] = ra + 1;

                    c.Ref.SameTeamMonthlyCount.TryGetValue(match.HomeTeamId, out var smh);
                    c.Ref.SameTeamMonthlyCount[match.HomeTeamId] = smh + 1;
                    c.Ref.SameTeamMonthlyCount.TryGetValue(match.AwayTeamId, out var sma);
                    c.Ref.SameTeamMonthlyCount[match.AwayTeamId] = sma + 1;

                    Search(vIdx + 1, currentPenalty + c.Penalty);

                    // Revert
                    c.Ref.AssignedTimes.Remove(match.MatchDate);
                    c.Ref.PastOfficiatedTeams[match.HomeTeamId].Remove(match.MatchDate);
                    c.Ref.PastOfficiatedTeams[match.AwayTeamId].Remove(match.MatchDate);
                    c.Ref.MonthWorkload[mk] = mw;
                    c.Ref.RecentTeamMatchCount[match.HomeTeamId] = rh;
                    c.Ref.RecentTeamMatchCount[match.AwayTeamId] = ra;
                    c.Ref.SameTeamMonthlyCount[match.HomeTeamId] = smh;
                    c.Ref.SameTeamMonthlyCount[match.AwayTeamId] = sma;
                }
                else
                {
                    Search(vIdx + 1, currentPenalty + DummyRefereePenalty);
                }

                currentAssignment[vIdx] = null;
            }
        }

        Search(0, 0.0);

        // If B&B produced no valid assignment (hit state limit with no base case),
        // seed with a greedy assignment so local search has something to improve.
        if (bestPenalty == double.MaxValue)
        {
            // Reset solver state
            foreach (var r in solverRefs)
            {
                r.AssignedTimes.Clear();
                // Restore PastOfficiatedTeams to historical only
                foreach (var kvp in r.PastOfficiatedTeams.ToList())
                    r.PastOfficiatedTeams[kvp.Key] = kvp.Value.Where(d => d < startDate).ToList();
            }

            GreedySeed(variables, solverRefs, sortedMatches, bestAssignment, startDate);
            bestPenalty = ComputeTotalObjective(variables, bestAssignment, solverRefs, sortedMatches);
        }

        // â”€â”€ Step 4: Local Search (2-opt slot swaps) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //
        // After B&B (or greedy seed), try to improve by swapping individual slots.
        // This corrects suboptimal choices caused by the B&B state limit.

        LocalSearch(variables, bestAssignment, solverRefs, sortedMatches, startDate);

        // â”€â”€ Step 5: Persist and Build Result â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            if (mainRefId  == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: no Main Referee.");
            if (asst1RefId == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: no Assistant 1.");
            if (asst2RefId == null) warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: no Assistant 2.");

            bool matchHasCar = false;
            foreach (var refId in new[] { mainRefId, asst1RefId, asst2RefId })
                if (refId != null && referees.FirstOrDefault(ref_ => ref_.Id == refId)?.HasCar == true)
                { matchHasCar = true; break; }

            if (!matchHasCar && cnt > 0)
            {
                noCarWarnings++;
                warnings.Add($"âš  {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}: no ref with a car assigned.");
            }

            foreach (var refId in new[] { mainRefId, asst1RefId, asst2RefId })
            {
                if (refId == null) continue;
                var r = referees.FirstOrDefault(ref_ => ref_.Id == refId);
                if (r?.Latitude.HasValue == true && r.Longitude.HasValue &&
                    match.HomeTeam?.Latitude.HasValue == true && match.HomeTeam.Longitude.HasValue)
                {
                    totalTravelDist += HaversineKm(
                        r.Latitude.Value, r.Longitude.Value,
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
            AvgTravelDistanceKm           = assignedRolesCount > 0
                                            ? Math.Round(totalTravelDist / assignedRolesCount, 1)
                                            : 0,
            NoCarWarningsCount            = noCarWarnings,
            Warnings                      = warnings
        };
    }

    // â”€â”€ Local Search â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Iterates over all slots and tries replacing the assigned ref with every feasible
    /// alternative. Accepts any improvement. Repeats until no improvement or iteration limit.
    /// </summary>
    private void LocalSearch(
        List<SolverVariable> variables,
        string?[] assignment,
        List<SolverReferee> solverRefs,
        List<Match> sortedMatches,
        DateTime batchStart)
    {
        int V = variables.Count;

        for (int iter = 0; iter < LocalSearchMaxIterations; iter++)
        {
            bool anyImproved = false;

            for (int vIdx = 0; vIdx < V; vIdx++)
            {
                var v     = variables[vIdx];
                var match = v.Match;
                var role  = v.Role;
                int mIdx  = vIdx / 3;
                int mBase = mIdx * 3;

                string? currentRefId = assignment[vIdx];
                double currentSlot = SlotPenaltyFromAssignment(currentRefId, match, role, assignment, variables, solverRefs);
                double currentMatchBonus = ComputeSingleMatchBonus(mIdx, assignment, solverRefs, match);

                string? bestId    = currentRefId;
                double  bestDelta = 0.0;

                foreach (var r in solverRefs)
                {
                    if (r.Id == currentRefId) continue;

                    // Hard: already in same match
                    bool inSameMatch = false;
                    for (int j = mBase; j < mBase + 3; j++)
                        if (j != vIdx && assignment[j] == r.Id) { inSameMatch = true; break; }
                    if (inSameMatch) continue;

                    // Hard: 5-hour gap (check all OTHER slots in assignment)
                    bool timeConflict = false;
                    for (int j = 0; j < V; j++)
                    {
                        if (j / 3 == mIdx) continue; // same match
                        if (assignment[j] == r.Id &&
                            Math.Abs((match.MatchDate - variables[j].Match.MatchDate).TotalHours) < MinHoursBetweenMatches)
                        { timeConflict = true; break; }
                    }
                    if (timeConflict) continue;

                    if (r.IsUnavailable(match.MatchDate)) continue;
                    if (!MeetsRankRequirement(r.Rank, match.HomeTeam!.League)) continue;
                    if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId)) continue;
                    if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                        HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate)) continue;
                    if (HasCityConflict(r, match)) continue;

                    // Compute delta if we swap in r
                    assignment[vIdx] = r.Id; // temporary
                    double newSlot        = SlotPenaltyFromAssignment(r.Id, match, role, assignment, variables, solverRefs);
                    double newMatchBonus  = ComputeSingleMatchBonus(mIdx, assignment, solverRefs, match);
                    double delta = (newSlot - currentSlot) + (newMatchBonus - currentMatchBonus);
                    assignment[vIdx] = currentRefId; // revert

                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        bestId    = r.Id;
                    }
                }

                // Also try removing the ref (assigning null).
                // KEY FIX: if the current ref is in the WRONG role, treat null as cheaper than
                // keeping them. The freed slot will be re-filled with a correct-role ref on the
                // very next local-search pass, effectively performing a two-step swap that pure
                // single-slot search would otherwise miss.
                if (currentRefId != null)
                {
                    var curRef = solverRefs.FirstOrDefault(sr => sr.Id == currentRefId);
                    bool isWrongRole = curRef != null
                        && curRef.PreferredRole != RefereePreferredRole.None
                        && IsWrongRoleForSlot(curRef, role);

                    assignment[vIdx] = null;
                    // Wrong-role: prefer null (2500) over keeping them (5000+).
                    // Correct/no-pref role: prefer keeping them (DummyRefereePenalty = 10000 makes null unattractive).
                    double nullSlot       = isWrongRole ? LocalSearchNullPenalty : DummyRefereePenalty;
                    double nullMatchBonus = ComputeSingleMatchBonus(mIdx, assignment, solverRefs, match);
                    double nullDelta      = (nullSlot - currentSlot) + (nullMatchBonus - currentMatchBonus);
                    assignment[vIdx] = currentRefId;

                    if (nullDelta < bestDelta)
                    {
                        bestDelta = nullDelta;
                        bestId    = null;
                    }
                }

                if (bestId != currentRefId)
                {
                    assignment[vIdx] = bestId;
                    anyImproved = true;
                }
            }

            if (!anyImproved) break;
        }
    }

    // â”€â”€ Greedy Seed â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void GreedySeed(
        List<SolverVariable> variables,
        List<SolverReferee> solverRefs,
        List<Match> sortedMatches,
        string?[] assignment,
        DateTime batchStart)
    {
        var assignedTimes = solverRefs.ToDictionary(r => r.Id, _ => new List<DateTime>());

        for (int vIdx = 0; vIdx < variables.Count; vIdx++)
        {
            var v     = variables[vIdx];
            var match = v.Match;
            var role  = v.Role;
            int mBase = (vIdx / 3) * 3;

            var assignedHere = new HashSet<string>();
            for (int j = mBase; j < vIdx; j++)
                if (assignment[j] != null) assignedHere.Add(assignment[j]!);

            var batchWorkload = new Dictionary<string, int>();
            for (int j = 0; j < vIdx; j++)
            {
                var id = assignment[j];
                if (id == null) continue;
                var mk = variables[j].Match.MatchDate.ToString("yyyy_MM");
                if (mk == match.MatchDate.ToString("yyyy_MM"))
                {
                    batchWorkload.TryGetValue(id, out var bw);
                    batchWorkload[id] = bw + 1;
                }
            }

            SolverReferee? best = null;
            double bestPenalty = double.MaxValue;

            foreach (var r in solverRefs)
            {
                if (assignedHere.Contains(r.Id)) continue;
                if (r.IsUnavailable(match.MatchDate)) continue;
                if (assignedTimes[r.Id].Any(t => Math.Abs((match.MatchDate - t).TotalHours) < MinHoursBetweenMatches)) continue;
                if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId)) continue;
                if (!MeetsRankRequirement(r.Rank, match.HomeTeam!.League)) continue;
                if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                    HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate)) continue;
                if (HasCityConflict(r, match)) continue;

                double p = ComputeSlotPenalty(r, match, role, batchWorkload);
                if (p < bestPenalty) { bestPenalty = p; best = r; }
            }

            assignment[vIdx] = best?.Id;
            if (best != null) assignedTimes[best.Id].Add(match.MatchDate);
        }
    }

    // â”€â”€ Objective helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private double ComputeTotalObjective(
        List<SolverVariable> variables,
        string?[] assignment,
        List<SolverReferee> solverRefs,
        List<Match> sortedMatches)
    {
        double total = 0;
        var bw = new Dictionary<string, int>();

        for (int vIdx = 0; vIdx < variables.Count; vIdx++)
        {
            var id = assignment[vIdx];
            total += id != null
                ? SlotPenaltyFromAssignment(id, variables[vIdx].Match, variables[vIdx].Role, assignment, variables, solverRefs)
                : DummyRefereePenalty;
        }

        for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
            total += ComputeSingleMatchBonus(mIdx, assignment, solverRefs, sortedMatches[mIdx]);

        return total;
    }

    /// <summary>
    /// Slot-level penalty: distance, workload (historical + batch), role, frequency, monthly team.
    /// Workload is computed from the assignment array (no separate state tracking needed).
    /// </summary>
    private double SlotPenaltyFromAssignment(
        string? refId,
        Match match,
        MatchRoleType role,
        string?[] assignment,
        List<SolverVariable> variables,
        List<SolverReferee> solverRefs)
    {
        if (refId == null) return DummyRefereePenalty;

        var r = solverRefs.FirstOrDefault(sr => sr.Id == refId);
        if (r == null) return DummyRefereePenalty;

        var monthKey = match.MatchDate.ToString("yyyy_MM");
        int batchCount = 0;
        for (int j = 0; j < variables.Count; j++)
            if (assignment[j] == r.Id && variables[j].Match.MatchDate.ToString("yyyy_MM") == monthKey)
                batchCount++;

        var batchWorkload = new Dictionary<string, int> { [r.Id] = Math.Max(0, batchCount - 1) };
        return ComputeSlotPenalty(r, match, role, batchWorkload);
    }

    /// <summary>Slot-level penalty used during B&B and greedy seed.</summary>
    private double ComputeSlotPenalty(
        SolverReferee r,
        Match match,
        MatchRoleType role,
        Dictionary<string, int> batchWorkload)
    {
        double penalty = 0.0;

        // 1. Distance
        if (r.Latitude.HasValue && r.Longitude.HasValue &&
            match.HomeTeam!.Latitude.HasValue && match.HomeTeam!.Longitude.HasValue)
        {
            var dist = HaversineKm(r.Latitude.Value, r.Longitude.Value,
                                   match.HomeTeam.Latitude.Value, match.HomeTeam.Longitude.Value);
            penalty += dist * KmPenaltyMultiplier;
            if (dist > 40.0) penalty += Over40KmPenalty;
        }
        else penalty += MissingCoordsDefaultPenalty;

        // 2. Workload â€” historical + in-batch count (no +1 base; first assignment costs 0)
        r.MonthWorkload.TryGetValue(match.MatchDate.ToString("yyyy_MM"), out var histW);
        batchWorkload.TryGetValue(r.Id, out var batchW);
        penalty += (histW + batchW) * WorkloadPenaltyMultiplier;

        // 3. Role preference
        if (r.PreferredRole != RefereePreferredRole.None)
        {
            bool wrongRole =
                (role == MatchRoleType.Main && r.PreferredRole == RefereePreferredRole.AssistantReferee) ||
                ((role == MatchRoleType.Assistant1 || role == MatchRoleType.Assistant2)
                    && r.PreferredRole == RefereePreferredRole.MainReferee);
            penalty += wrongRole ? WrongRolePenalty : -RoleMatchBonus;
        }

        // 4. Frequency (last 60 days)
        r.RecentTeamMatchCount.TryGetValue(match.HomeTeamId, out var rh);
        r.RecentTeamMatchCount.TryGetValue(match.AwayTeamId, out var ra);
        penalty += FrequencyPenalty[Math.Min(rh + ra, FrequencyPenalty.Length - 1)];

        // 5. Same-team 30-day overexposure
        r.SameTeamMonthlyCount.TryGetValue(match.HomeTeamId, out var mh);
        r.SameTeamMonthlyCount.TryGetValue(match.AwayTeamId, out var ma);
        if (mh > 2 || ma > 2) penalty += SameTeamMonthlyPenalty;

        return penalty;
    }

    /// <summary>
    /// Per-match bonus/penalty computed after all 3 slots are known:
    /// car penalty, spread penalty, on-the-way bonus.
    /// </summary>
    private static double ComputeSingleMatchBonus(
        int mIdx, string?[] assignment, List<SolverReferee> solverRefs, Match match)
    {
        var refIds  = new[] { assignment[mIdx * 3], assignment[mIdx * 3 + 1], assignment[mIdx * 3 + 2] };
        var refObjs = refIds.Where(id => id != null)
                            .Select(id => solverRefs.FirstOrDefault(r => r.Id == id))
                            .Where(r => r != null)
                            .ToList()!;

        if (refObjs.Count == 0) return 0.0;

        double bonus = 0.0;

        // Car
        if (!refObjs.Any(r => r!.HasCar)) bonus += NoCarPenalty;

        // Spread: avg pairwise distance between ref homes
        var withCoords = refObjs.Where(r => r!.Latitude.HasValue).ToList();
        if (withCoords.Count >= 2)
        {
            var dists = new List<double>();
            for (int i = 0; i < withCoords.Count; i++)
                for (int j = i + 1; j < withCoords.Count; j++)
                    dists.Add(HaversineKm(
                        withCoords[i]!.Latitude!.Value, withCoords[i]!.Longitude!.Value,
                        withCoords[j]!.Latitude!.Value, withCoords[j]!.Longitude!.Value));
            bonus += SpreadPenaltyWeight * dists.Average();
        }

        // On the way: reward if ref A is on the route from ref B to the match
        if (match.HomeTeam?.Latitude.HasValue == true)
        {
            double mLat = match.HomeTeam.Latitude!.Value, mLon = match.HomeTeam.Longitude!.Value;
            for (int i = 0; i < refObjs.Count; i++)
            {
                for (int j = 0; j < refObjs.Count; j++)
                {
                    if (i == j) continue;
                    var rA = refObjs[i]; var rB = refObjs[j];
                    if (!rA!.Latitude.HasValue || !rB!.Latitude.HasValue) continue;
                    double dBC = HaversineKm(rB!.Latitude!.Value, rB.Longitude!.Value, mLat, mLon);
                    double dBA = HaversineKm(rB.Latitude!.Value,  rB.Longitude!.Value, rA.Latitude!.Value, rA.Longitude!.Value);
                    double dAC = HaversineKm(rA.Latitude!.Value,  rA.Longitude!.Value, mLat, mLon);
                    if (dBC > 1.0 && (dBA + dAC) <= 1.25 * dBC)
                        bonus -= OnTheWayBonus;
                }
            }
        }

        return bonus;
    }

    private static double ComputeAllMatchBonuses(
        List<Match> sortedMatches, string?[] assignment, List<SolverReferee> solverRefs)
    {
        double total = 0;
        for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
            total += ComputeSingleMatchBonus(mIdx, assignment, solverRefs, sortedMatches[mIdx]);
        return total;
    }

    // â”€â”€ Hard-constraint helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static bool HasCityConflict(SolverReferee r, Match match)
    {
        if (string.IsNullOrWhiteSpace(r.HomeCity)) return false;
        if (r.HomeCity.Equals(AradCityName, StringComparison.OrdinalIgnoreCase)) return false;

        var homeCity = match.HomeTeam?.City?.Trim() ?? "";
        var awayCity = match.AwayTeam?.City?.Trim() ?? "";
        return (!string.IsNullOrEmpty(homeCity) && homeCity.Equals(r.HomeCity, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrEmpty(awayCity) && awayCity.Equals(r.HomeCity, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>Returns true when a ref's preferred role does NOT match the slot being filled.</summary>
    private static bool IsWrongRoleForSlot(SolverReferee r, MatchRoleType role) =>
        (role == MatchRoleType.Main && r.PreferredRole == RefereePreferredRole.AssistantReferee) ||
        ((role == MatchRoleType.Assistant1 || role == MatchRoleType.Assistant2)
            && r.PreferredRole == RefereePreferredRole.MainReferee);


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
        if (!r.PastOfficiatedTeams.TryGetValue(teamId, out var dates)) return false;
        return dates.Any(d => Math.Abs((matchDate.Date - d.Date).Days) <= 21);
    }

    private static void AddTeamDate(Dictionary<int, List<DateTime>> dict, int teamId, DateTime date)
    {
        if (!dict.ContainsKey(teamId)) dict[teamId] = new List<DateTime>();
        dict[teamId].Add(date);
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

    // â”€â”€ Solver internal classes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private class SolverReferee
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RefereeRank Rank { get; set; }
        public RefereePreferredRole PreferredRole { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool HasCar { get; set; }
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
            return Unavailabilities.Any(i => d >= i.Start.Date && d <= i.End.Date);
        }
    }

    private class SolverVariable
    {
        public Match Match { get; set; } = null!;
        public MatchRoleType Role { get; set; }
    }
}

