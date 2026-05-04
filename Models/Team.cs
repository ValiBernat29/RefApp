using System.ComponentModel.DataAnnotations;

namespace RefApp.Models;

public enum League
{
    L6,
    L5A,
    L5B,
    L5C,
    L4
}

public class Team
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public League League { get; set; }

    /// <summary>
    /// Usual match day for this team's league (can be overridden per match).
    /// </summary>
    [Required]
    public DayOfWeek PreferredMatchDay { get; set; }

    /// <summary>
    /// City or village where this team's ground is located.
    /// Used for distance-based referee scoring.
    /// </summary>
    [StringLength(100)]
    public string? City { get; set; }

    /// <summary>Cached geocoded latitude (populated by GeocodingService).</summary>
    public double? Latitude { get; set; }

    /// <summary>Cached geocoded longitude (populated by GeocodingService).</summary>
    public double? Longitude { get; set; }
}

