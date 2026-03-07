namespace RefApp.Models;

public class MatchAssignment
{
    public int Id { get; set; }

    public int MatchId { get; set; }

    public Match Match { get; set; } = null!;

    public string RefereeId { get; set; } = string.Empty;

    public ApplicationUser? Referee { get; set; }

    public MatchRoleType RoleType { get; set; }
}

public enum MatchRoleType
{
    Main,
    Assistant1,
    Assistant2
}

