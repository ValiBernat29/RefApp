using System.ComponentModel.DataAnnotations;

namespace RefApp.Models;

public class TeamRefereeRefusal
{
    public int Id { get; set; }

    [Required]
    public int TeamId { get; set; }

    public Team Team { get; set; } = null!;

    [Required]
    public string RefereeId { get; set; } = string.Empty;

    public ApplicationUser Referee { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime DateRefused { get; set; } = DateTime.UtcNow;
}
