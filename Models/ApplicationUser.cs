using Microsoft.AspNetCore.Identity;

namespace RefApp.Models;

public enum RefereeRank
{
    None,
    L6_5,
    L4,
    Above
}

public enum RefereePreferredRole
{
    None,           // no preference set
    MainReferee,
    AssistantReferee
}

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public ICollection<MatchAssignment> MatchAssignments { get; set; } = new List<MatchAssignment>();

    public ICollection<Unavailability> Unavailabilities { get; set; } = new List<Unavailability>();

    public RefereeRank Rank { get; set; } = RefereeRank.None;

    public RefereePreferredRole PreferredRole { get; set; } = RefereePreferredRole.None;

    public string? HomeCity { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

