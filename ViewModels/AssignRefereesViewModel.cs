namespace RefApp.ViewModels;

public class AssignRefereesViewModel
{
    public int MatchId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }
    public string Location { get; set; } = string.Empty;

    public List<RefereeOption> EligibleReferees { get; set; } = new();

    public string? MainRefereeId { get; set; }
    public string? Assistant1Id { get; set; }
    public string? Assistant2Id { get; set; }
}

public class RefereeOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
