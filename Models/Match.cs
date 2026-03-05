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
    [StringLength(200)]
    [Display(Name = "Home Team")]
    public string HomeTeam { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Away Team")]
    public string AwayTeam { get; set; } = string.Empty;

    public ICollection<MatchAssignment> Assignments { get; set; } = new List<MatchAssignment>();
}

