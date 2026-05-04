using Microsoft.AspNetCore.Identity;

namespace RefApp.Models;

public enum RefereeRank
{
    None,
    L6_5,
    L4,
    Above
}

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public ICollection<MatchAssignment> MatchAssignments { get; set; } = new List<MatchAssignment>();

    public ICollection<Unavailability> Unavailabilities { get; set; } = new List<Unavailability>();

    public RefereeRank Rank { get; set; } = RefereeRank.None;

    public string? HomeCity { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

