using RefApp.Models;

namespace RefApp.ViewModels;

public class AssignRefereesViewModel
{
    public int MatchId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }
    public string Location { get; set; } = string.Empty;

    /// <summary>Referees sorted for the Main slot (MainReferee role first, then by distance).</summary>
    public List<RefereeOption> EligibleReferees { get; set; } = new();

    /// <summary>Referees sorted for the Assistant 1 slot (AssistantReferee role first, then by distance).</summary>
    public List<RefereeOption> EligibleRefereesAsst1 { get; set; } = new();

    /// <summary>Referees sorted for the Assistant 2 slot (AssistantReferee role first, then by distance).</summary>
    public List<RefereeOption> EligibleRefereesAsst2 { get; set; } = new();

    public string? MainRefereeId { get; set; }
    public string? Assistant1Id { get; set; }
    public string? Assistant2Id { get; set; }
}

public class RefereeOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public bool IsUnavailable { get; set; }

    /// <summary>Same day AND same hour as this match → hard block.</summary>
    public bool HasConflictingMatch { get; set; }

    /// <summary>Same day but different hour → soft warning only.</summary>
    public bool HasOtherMatchThatDay { get; set; }

    // ── Suitability Scoring (Phase 3) ─────────────────────────────────────
    /// <summary>Computed score; higher = better candidate. Board sees ordering, not the number.</summary>
    public double SuitabilityScore { get; set; } = 100;

    /// <summary>Straight-line km between referee home and match location. Null if coordinates unavailable.</summary>
    public double? DistanceKm { get; set; }

    /// <summary>How many of the last 5 matches for either team this referee officiated.</summary>
    public int RecentMatchCount { get; set; }

    /// <summary>Home team has a refusal on record for this referee.</summary>
    public bool IsRefusedByHomeTeam { get; set; }

    /// <summary>Away team has a refusal on record for this referee.</summary>
    public bool IsRefusedByAwayTeam { get; set; }

    /// <summary>The referee's designated preferred role (Main / Assistant / None).</summary>
    public RefereePreferredRole PreferredRole { get; set; }

    /// <summary>True when the referee's preferred role matches the slot being scored.</summary>
    public bool RoleMatchBoostApplied { get; set; }
}
