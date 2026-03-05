using Microsoft.AspNetCore.Identity;

namespace RefApp.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public ICollection<MatchAssignment> MatchAssignments { get; set; } = new List<MatchAssignment>();

    public ICollection<Unavailability> Unavailabilities { get; set; } = new List<Unavailability>();
}

