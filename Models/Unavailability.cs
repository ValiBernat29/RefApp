using System.ComponentModel.DataAnnotations;
using RefApp.Validation;

namespace RefApp.Models;

public class Unavailability
{
    public int Id { get; set; }

    public string RefereeId { get; set; } = string.Empty;

    public ApplicationUser Referee { get; set; } = null!;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    [UnavailabilityDateRange]
    public DateTime EndDate { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

