using System.ComponentModel.DataAnnotations;

namespace RefApp.Models;

public class Match
{
    public int Id { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Match Date")]
    public DateTime MatchDate { get; set; }

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Home Team")]
    public int HomeTeamId { get; set; }

    public Team HomeTeam { get; set; } = null!;

    [Required]
    [Display(Name = "Away Team")]
    public int AwayTeamId { get; set; }

    public Team AwayTeam { get; set; } = null!;

    public ICollection<MatchAssignment> Assignments { get; set; } = new List<MatchAssignment>();
}

