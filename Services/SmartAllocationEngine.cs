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

    // Soft constraint coefficients
    private const double Over40KmPenalty = 500.0;
    private const double KmPenaltyMultiplier = 1.0; // 1 point per km
    private const double WorkloadPenaltyMultiplier = 30.0; // 30 points per monthly assignment
    private const double RoleMatchBonus = 15.0; // -15 points if preferred role matches
    private const double MissingCoordsDefaultPenalty = 20.0; // penalty if location coords are missing
    private const double DummyRefereePenalty = 10000.0; // huge penalty for unassigned slots

    private const int MaxSearchStates = 20000; // safety limit to prevent hangs

    public SmartAllocationEngine(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AllocationResult> AllocateRefereesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // ── Step 1: Bulk Fetch State from Database ───────────────────────────
        
        // 1. Fetch upcoming matches in range
        var matches = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Assignments)
            .Where(m => m.MatchDate >= startDate && m.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        if (!matches.Any())
        {
            return new AllocationResult
            {
                Success = true,
                Message = "No matches found in the specified date range.",
                TotalMatchesToAssign = 0
            };
        }

        // 2. Fetch all referees (users in the "Referee" role)
        var referees = await _context.Users
            .Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Referee")))
            .ToListAsync(cancellationToken);

        // 3. Fetch unavailabilities overlapping the window
        var unavailabilities = await _context.Unavailabilities
            .Where(u => u.StartDate <= endDate && u.EndDate >= startDate)
            .ToListAsync(cancellationToken);

        // 4. Fetch all team referee refusals (conflicts of interest / blocked teams)
        var refusals = await _context.TeamRefereeRefusals.ToListAsync(cancellationToken);

        // 5. Fetch past assignments for the 21-day history check and workload balancing
        var historyStart = startDate.AddDays(-21);
        var workloadStart = new DateTime(startDate.Year, startDate.Month, 1);
        var fetchStart = historyStart < workloadStart ? historyStart : workloadStart;

        var pastAssignments = await _context.MatchAssignments
            .Include(a => a.Match)
            .ThenInclude(m => m.HomeTeam)
            .Include(a => a.Match)
            .ThenInclude(m => m.AwayTeam)
            .Where(a => a.Match.MatchDate >= fetchStart && a.Match.MatchDate <= endDate)
            .ToListAsync(cancellationToken);

        // ── Step 2: Map to Solver-friendly In-Memory Structures ───────────────
        
        var solverRefs = new List<SolverReferee>();
        foreach (var r in referees)
        {
            var solverRef = new SolverReferee
            {
                Id = r.Id,
                Name = r.DisplayName ?? r.UserName ?? r.Email ?? r.Id,
                Rank = r.Rank,
                PreferredRole = r.PreferredRole,
                Latitude = r.Latitude,
                Longitude = r.Longitude
            };

            // Map unavailabilities
            var refUnavail = unavailabilities.Where(u => u.RefereeId == r.Id);
            foreach (var u in refUnavail)
            {
                solverRef.Unavailabilities.Add((u.StartDate.Date, u.EndDate.Date));
            }

            // Map blocked teams (refusals)
            var refRefusals = refusals.Where(rf => rf.RefereeId == r.Id).Select(rf => rf.TeamId);
            foreach (var teamId in refRefusals)
            {
                solverRef.BlockedTeamIds.Add(teamId);
            }

            // Map past officiating history (assignments where match was played)
            var refPast = pastAssignments
                .Where(a => a.RefereeId == r.Id && a.Match.MatchDate < startDate)
                .ToList();
            foreach (var a in refPast)
            {
                var matchDate = a.Match.MatchDate;
                // Add HomeTeam
                if (!solverRef.PastOfficiatedTeams.ContainsKey(a.Match.HomeTeamId))
                    solverRef.PastOfficiatedTeams[a.Match.HomeTeamId] = new List<DateTime>();
                solverRef.PastOfficiatedTeams[a.Match.HomeTeamId].Add(matchDate);

                // Add AwayTeam
                if (!solverRef.PastOfficiatedTeams.ContainsKey(a.Match.AwayTeamId))
                    solverRef.PastOfficiatedTeams[a.Match.AwayTeamId] = new List<DateTime>();
                solverRef.PastOfficiatedTeams[a.Match.AwayTeamId].Add(matchDate);
            }

            // Map workload count in calendar months
            var refWorkloads = pastAssignments
                .Where(a => a.RefereeId == r.Id)
                .GroupBy(a => a.Match.MatchDate.ToString("yyyy_MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var kvp in refWorkloads)
            {
                solverRef.MonthWorkload[kvp.Key] = kvp.Value;
            }

            solverRefs.Add(solverRef);
        }

        // Sort matches to process the most constrained ones first (higher leagues, then chronological)
        var sortedMatches = matches
            .OrderBy(m => m.HomeTeam?.League == League.L4 ? 0 : 
                          (m.HomeTeam?.League == League.L5A || m.HomeTeam?.League == League.L5B || m.HomeTeam?.League == League.L5C ? 1 : 2))
            .ThenBy(m => m.MatchDate)
            .ToList();

        // Build list of variables to assign
        var variables = new List<SolverVariable>();
        int varIndex = 0;
        foreach (var m in sortedMatches)
        {
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Main });
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Assistant1 });
            variables.Add(new SolverVariable { Index = varIndex++, Match = m, Role = MatchRoleType.Assistant2 });
        }

        // ── Step 3: Run the Branch & Bound Search Solver ───────────────────────
        
        var currentAssignment = new string?[variables.Count];
        var bestAssignment = new string?[variables.Count];
        double bestPenalty = double.MaxValue;
        int stateCount = 0;

        void Search(int vIdx, double currentPenalty)
        {
            stateCount++;
            if (stateCount > MaxSearchStates)
                return; // hit limit, stop searching further branches

            // Base Case: All variables assigned
            if (vIdx == variables.Count)
            {
                if (currentPenalty < bestPenalty)
                {
                    bestPenalty = currentPenalty;
                    Array.Copy(currentAssignment, bestAssignment, variables.Count);
                }
                return;
            }

            // Pruning
            if (currentPenalty >= bestPenalty)
                return;

            var v = variables[vIdx];
            var match = v.Match;
            var role = v.Role;

            // Find other referees already assigned to the same match in this branch
            var assignedRefsForThisMatch = new List<string>();
            for (int i = 0; i < vIdx; i++)
            {
                if (variables[i].Match.Id == match.Id && currentAssignment[i] != null)
                {
                    assignedRefsForThisMatch.Add(currentAssignment[i]!);
                }
            }

            // Find eligible candidates and compute their local penalty
            var candidates = new List<(SolverReferee? Ref, double LocalPenalty)>();

            // 1. Try real referee candidates
            foreach (var r in solverRefs)
            {
                // -- Hard Constraint Check --
                
                // A referee can only be assigned to one role per match
                if (assignedRefsForThisMatch.Contains(r.Id))
                    continue;

                // Availability check (unavailability dates)
                if (r.IsUnavailable(match.MatchDate))
                    continue;

                // Scheduling conflict check (same day AND hour)
                var slotKey = match.MatchDate.ToString("yyyyMMdd_HH");
                if (r.AssignedSlots.Contains(slotKey))
                    continue;

                // Conflict of interest check (blocked teams)
                if (r.BlockedTeamIds.Contains(match.HomeTeamId) || r.BlockedTeamIds.Contains(match.AwayTeamId))
                    continue;

                // Certification check
                if (!MeetsRankRequirement(r.Rank, match.HomeTeam.League))
                    continue;

                // Recent history check (21 days)
                if (HasRecentHistoryConflict(r, match.HomeTeamId, match.MatchDate) ||
                    HasRecentHistoryConflict(r, match.AwayTeamId, match.MatchDate))
                    continue;

                // -- Soft Constraint / Penalty calculation --
                double penalty = 0.0;

                // Travel Distance Penalty
                if (r.Latitude.HasValue && r.Longitude.HasValue && match.HomeTeam.Latitude.HasValue && match.HomeTeam.Longitude.HasValue)
                {
                    var dist = HaversineKm(r.Latitude.Value, r.Longitude.Value, match.HomeTeam.Latitude.Value, match.HomeTeam.Longitude.Value);
                    penalty += dist * KmPenaltyMultiplier;
                    if (dist > 40.0)
                    {
                        penalty += Over40KmPenalty; // heavy penalty
                    }
                }
                else
                {
                    penalty += MissingCoordsDefaultPenalty;
                }

                // Workload Balancing Penalty
                var monthKey = match.MatchDate.ToString("yyyy_MM");
                r.MonthWorkload.TryGetValue(monthKey, out var w);
                penalty += (w + 1) * WorkloadPenaltyMultiplier;

                // Role preference bonus
                if (role == MatchRoleType.Main && r.PreferredRole == RefereePreferredRole.MainReferee)
                    penalty -= RoleMatchBonus;
                else if ((role == MatchRoleType.Assistant1 || role == MatchRoleType.Assistant2) && r.PreferredRole == RefereePreferredRole.AssistantReferee)
                    penalty -= RoleMatchBonus;

                candidates.Add((r, penalty));
            }

            // 2. Always offer a Dummy/Unassigned fallback option to avoid failure
            candidates.Add((null, DummyRefereePenalty));

            // Sort candidates by penalty (lowest first) to guide the greedy search
            var sortedCandidates = candidates.OrderBy(c => c.LocalPenalty).ToList();

            foreach (var c in sortedCandidates)
            {
                if (c.Ref != null)
                {
                    // Apply assignments to referee's state
                    var slotKey = match.MatchDate.ToString("yyyyMMdd_HH");
                    c.Ref.AssignedSlots.Add(slotKey);

                    if (!c.Ref.PastOfficiatedTeams.ContainsKey(match.HomeTeamId))
                        c.Ref.PastOfficiatedTeams[match.HomeTeamId] = new List<DateTime>();
                    c.Ref.PastOfficiatedTeams[match.HomeTeamId].Add(match.MatchDate);

                    if (!c.Ref.PastOfficiatedTeams.ContainsKey(match.AwayTeamId))
                        c.Ref.PastOfficiatedTeams[match.AwayTeamId] = new List<DateTime>();
                    c.Ref.PastOfficiatedTeams[match.AwayTeamId].Add(match.MatchDate);

                    var monthKey = match.MatchDate.ToString("yyyy_MM");
                    c.Ref.MonthWorkload.TryGetValue(monthKey, out var w);
                    c.Ref.MonthWorkload[monthKey] = w + 1;

                    currentAssignment[vIdx] = c.Ref.Id;

                    // Recurse
                    Search(vIdx + 1, currentPenalty + c.LocalPenalty);

                    // Revert assignment
                    c.Ref.AssignedSlots.Remove(slotKey);
                    c.Ref.PastOfficiatedTeams[match.HomeTeamId].Remove(match.MatchDate);
                    c.Ref.PastOfficiatedTeams[match.AwayTeamId].Remove(match.MatchDate);
                    c.Ref.MonthWorkload[monthKey] = w;
                }
                else
                {
                    currentAssignment[vIdx] = null; // unassigned
                    Search(vIdx + 1, currentPenalty + c.LocalPenalty);
                }

                currentAssignment[vIdx] = null;
            }
        }

        // Start search
        Search(0, 0.0);

        // ── Step 4: Save Computed Schedule to Database and Build Statistics ───
        
        var warnings = new List<string>();
        int totalMatches = matches.Count;
        int fullyAssigned = 0;
        int partiallyAssigned = 0;
        double totalTravelDist = 0.0;
        int assignedRolesCount = 0;

        // Group assignments by match
        var assignmentsToSave = new List<MatchAssignment>();

        for (int mIdx = 0; mIdx < sortedMatches.Count; mIdx++)
        {
            var match = sortedMatches[mIdx];
            var mainRefId = bestAssignment[mIdx * 3];
            var asst1RefId = bestAssignment[mIdx * 3 + 1];
            var asst2RefId = bestAssignment[mIdx * 3 + 2];

            int assignedCount = 0;
            if (mainRefId != null) assignedCount++;
            if (asst1RefId != null) assignedCount++;
            if (asst2RefId != null) assignedCount++;

            if (assignedCount == 3)
                fullyAssigned++;
            else if (assignedCount > 0)
                partiallyAssigned++;

            if (mainRefId == null)
                warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Main Referee assigned.");
            if (asst1RefId == null)
                warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Assistant 1 assigned.");
            if (asst2RefId == null)
                warnings.Add($"Match {match.HomeTeam?.Name} vs {match.AwayTeam?.Name} has no Assistant 2 assigned.");

            // Calculate travel distance
            foreach (var refId in new[] { mainRefId, asst1RefId, asst2RefId })
            {
                if (refId == null) continue;
                var r = referees.First(referee => referee.Id == refId);
                if (r.Latitude.HasValue && r.Longitude.HasValue && match.HomeTeam?.Latitude.HasValue == true && match.HomeTeam?.Longitude.HasValue == true)
                {
                    var dist = HaversineKm(r.Latitude.Value, r.Longitude.Value, match.HomeTeam.Latitude.Value, match.HomeTeam.Longitude.Value);
                    totalTravelDist += dist;
                    assignedRolesCount++;
                }
            }

            // Remove existing assignments for this match in the DB
            _context.MatchAssignments.RemoveRange(match.Assignments);

            // Add new assignments
            if (mainRefId != null)
                _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = mainRefId, RoleType = MatchRoleType.Main });
            if (asst1RefId != null)
                _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst1RefId, RoleType = MatchRoleType.Assistant1 });
            if (asst2RefId != null)
                _context.MatchAssignments.Add(new MatchAssignment { MatchId = match.Id, RefereeId = asst2RefId, RoleType = MatchRoleType.Assistant2 });
        }

        // Save to Database
        await _context.SaveChangesAsync(cancellationToken);

        return new AllocationResult
        {
            Success = true,
            Message = $"Auto-allocation complete. Processed {totalMatches} matches.",
            TotalMatchesToAssign = totalMatches,
            FullyAssignedMatchesCount = fullyAssigned,
            PartiallyAssignedMatchesCount = partiallyAssigned,
            TotalTravelDistanceKm = Math.Round(totalTravelDist, 1),
            AvgTravelDistanceKm = assignedRolesCount > 0 ? Math.Round(totalTravelDist / assignedRolesCount, 1) : 0,
            Warnings = warnings
        };
    }

    private static bool MeetsRankRequirement(RefereeRank rank, League league)
    {
        if (rank == RefereeRank.None) return false;
        if (rank == RefereeRank.Above) return true;

        if (league == League.L4)
        {
            return rank == RefereeRank.L4;
        }
        else // L5A, L5B, L5C, L6
        {
            return rank == RefereeRank.L4 || rank == RefereeRank.L6_5;
        }
    }

    private static bool HasRecentHistoryConflict(SolverReferee r, int teamId, DateTime matchDate)
    {
        if (r.PastOfficiatedTeams.TryGetValue(teamId, out var dates))
        {
            foreach (var d in dates)
            {
                if (Math.Abs((matchDate.Date - d.Date).Days) <= 21)
                {
                    return true;
                }
            }
        }
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

    // Helper classes for Solver state
    private class SolverReferee
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RefereeRank Rank { get; set; }
        public RefereePreferredRole PreferredRole { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public HashSet<int> BlockedTeamIds { get; set; } = new();
        public HashSet<string> AssignedSlots { get; set; } = new();
        public List<(DateTime Start, DateTime End)> Unavailabilities { get; set; } = new();
        public Dictionary<int, List<DateTime>> PastOfficiatedTeams { get; set; } = new();
        public Dictionary<string, int> MonthWorkload { get; set; } = new();

        public bool IsUnavailable(DateTime matchDate)
        {
            var dateOnly = matchDate.Date;
            foreach (var interval in Unavailabilities)
            {
                if (dateOnly >= interval.Start.Date && dateOnly <= interval.End.Date)
                {
                    return true;
                }
            }
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
