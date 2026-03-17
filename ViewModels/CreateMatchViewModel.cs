using System.ComponentModel.DataAnnotations;
using RefApp.Models;

namespace RefApp.ViewModels;

public class CreateMatchViewModel
{
    public int? Id { get; set; }

    [Display(Name = "League")]
    public League? League { get; set; }

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

    [Required]
    [Display(Name = "Away Team")]
    public int AwayTeamId { get; set; }

    [Required]
    [Display(Name = "Match Date & Time")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime MatchTime { get; set; }
    public IEnumerable<Team>? Teams { get; set; }
}

