using Microsoft.EntityFrameworkCore;
using RefApp.Data;

namespace RefApp.Services;

/// <summary>
/// After the app has started serving requests, fills in coordinates for teams that have a
/// City but no Latitude/Longitude yet. Runs once per process start. Teams whose lookup fails
/// are retried on the next start.
/// </summary>
public class TeamGeocodingBackgroundService : BackgroundService
{
    // Nominatim usage policy: at most one request per second.
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromMilliseconds(1100);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TeamGeocodingBackgroundService> _logger;

    public TeamGeocodingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<TeamGeocodingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // An unhandled exception in a BackgroundService stops the whole host in .NET 8,
        // so everything below is caught and logged.
        try
        {
            // Wait until the web server is accepting requests.
            var started = new TaskCompletionSource();
            using var registration = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var geocoding = scope.ServiceProvider.GetRequiredService<GeocodingService>();

            var teams = await context.Teams
                .Where(t => !string.IsNullOrEmpty(t.City) && t.Latitude == null)
                .ToListAsync(stoppingToken);
            if (teams.Count == 0) return;

            _logger.LogInformation("Geocoding {Count} team(s) without coordinates", teams.Count);

            var failed = new List<string>();
            foreach (var team in teams)
            {
                var coords = await geocoding.GeocodeAsync(team.City!);
                if (coords.HasValue)
                {
                    team.Latitude = coords.Value.Lat;
                    team.Longitude = coords.Value.Lon;
                    // Save as we go so progress survives a shutdown mid-run.
                    await context.SaveChangesAsync(stoppingToken);
                }
                else
                {
                    failed.Add($"{team.Name} ({team.City})");
                }

                await Task.Delay(DelayBetweenRequests, stoppingToken);
            }

            if (failed.Count > 0)
                _logger.LogWarning("Could not geocode: {Teams}", string.Join(", ", failed));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Team geocoding failed");
        }
    }
}
