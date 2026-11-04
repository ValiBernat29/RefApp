using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RefApp.Data;
using RefApp.Models;
using RefApp.Services;

namespace RefApp.Controllers.Api;

[Route("api/board/teams")]
public class TeamsApiController : BoardApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly GeocodingService _geocoding;

    public TeamsApiController(ApplicationDbContext context, GeocodingService geocoding)
    {
        _context = context;
        _geocoding = geocoding;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var teams = await _context.Teams
            .OrderBy(t => t.League)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(teams.Select(TeamDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, ct);
        return team == null ? NotFound(new { error = "Team not found." }) : Ok(TeamDto.From(team));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TeamRequest request, CancellationToken ct)
    {
        var city = NormalizeCity(request.City);
        if (!IsValidCity(city)) return CityError();

        var team = new Team
        {
            Name = request.Name.Trim(),
            League = request.League,
            PreferredMatchDay = request.PreferredMatchDay,
            City = city,
        };
        await ApplyCoordinatesAsync(team);

        _context.Teams.Add(team);
        await _context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = team.Id }, TeamDto.From(team));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TeamRequest request, CancellationToken ct)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, ct);
        if (team == null) return NotFound(new { error = "Team not found." });

        var city = NormalizeCity(request.City);
        if (!IsValidCity(city)) return CityError();

        team.Name = request.Name.Trim();
        team.League = request.League;
        team.PreferredMatchDay = request.PreferredMatchDay;

        if (team.City != city)
        {
            team.City = city;
            await ApplyCoordinatesAsync(team);

            // Keep the venue of this team's home matches in step with its city.
            var homeMatches = await _context.Matches.Where(m => m.HomeTeamId == team.Id).ToListAsync(ct);
            foreach (var match in homeMatches)
                match.Location = team.City ?? team.Name;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(TeamDto.From(team));
    }

    /// <summary>Deletes the team together with every match it plays in and those matches' assignments.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var team = await _context.Teams.FindAsync(new object[] { id }, ct);
        if (team == null) return NotFound(new { error = "Team not found." });

        var matches = await _context.Matches
            .Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id)
            .ToListAsync(ct);
        var matchIds = matches.Select(m => m.Id).ToList();

        if (matchIds.Count > 0)
        {
            var assignments = await _context.MatchAssignments
                .Where(a => matchIds.Contains(a.MatchId))
                .ToListAsync(ct);
            _context.MatchAssignments.RemoveRange(assignments);
        }

        _context.Matches.RemoveRange(matches);
        _context.Teams.Remove(team);
        await _context.SaveChangesAsync(ct);

        return Ok(new { deletedMatches = matches.Count });
    }

    private static string? NormalizeCity(string? city)
    {
        var trimmed = city?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsValidCity(string? city) => city == null || AradLocalities.All.Contains(city);

    private IActionResult CityError()
    {
        ModelState.AddModelError(nameof(TeamRequest.City), "Please select a valid city or village from the list.");
        return ValidationProblem(ModelState);
    }

    private async Task ApplyCoordinatesAsync(Team team)
    {
        if (team.City == null)
        {
            team.Latitude = null;
            team.Longitude = null;
            return;
        }

        var coords = await _geocoding.GeocodeAsync(team.City);
        team.Latitude = coords?.Lat;
        team.Longitude = coords?.Lon;
    }
}

public class TeamRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(League))]
    public League League { get; set; }

    [EnumDataType(typeof(DayOfWeek))]
    public DayOfWeek PreferredMatchDay { get; set; }

    [StringLength(100)]
    public string? City { get; set; }
}

public record TeamDto(
    int Id,
    string Name,
    League League,
    DayOfWeek PreferredMatchDay,
    string? City,
    bool HasCoordinates)
{
    public static TeamDto From(Team t) =>
        new(t.Id, t.Name, t.League, t.PreferredMatchDay, t.City, t.Latitude != null && t.Longitude != null);
}
