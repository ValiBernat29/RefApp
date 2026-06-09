using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RefApp.Services;

public class AllocationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalMatchesToAssign { get; set; }
    public int FullyAssignedMatchesCount { get; set; }
    public int PartiallyAssignedMatchesCount { get; set; }
    public double TotalTravelDistanceKm { get; set; }
    public double AvgTravelDistanceKm { get; set; }
    /// <summary>Number of matches where no car-equipped referee could be assigned.</summary>
    public int NoCarWarningsCount { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
}

public interface ISmartAllocationEngine
{
    Task<AllocationResult> AllocateRefereesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
