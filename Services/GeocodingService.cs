using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RefApp.Models;

namespace RefApp.Services;

/// <summary>
/// Uses the free OpenStreetMap Nominatim API to geocode city/village names
/// into latitude/longitude coordinates and caches results on the entity.
/// </summary>
public class GeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeocodingService> _logger;

    // Nominatim requires a meaningful User-Agent header
    private const string UserAgent = "RefApp/1.0 (referee-appointment-system)";

    public GeocodingService(IHttpClientFactory httpClientFactory, ILogger<GeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Geocodes a city/village name (optionally with country context) and returns
    /// (latitude, longitude), or null if the location could not be resolved.
    /// </summary>
    public async Task<(double Lat, double Lon)?> GeocodeAsync(string cityName, string countryCode = "ro")
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("Nominatim");
            var url = $"search?q={Uri.EscapeDataString(cityName)}&countrycodes={countryCode}&format=json&limit=1";
            var results = await client.GetFromJsonAsync<NominatimResult[]>(url);

            if (results is { Length: > 0 }
                && double.TryParse(results[0].Lat, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(results[0].Lon, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                return (lat, lon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geocoding failed for '{City}'", cityName);
        }

        return null;
    }

    /// <summary>
    /// Calculates the straight-line (Haversine) distance in kilometres between two points.
    /// </summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // Earth radius in km
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;

    private class NominatimResult
    {
        [JsonPropertyName("lat")] public string Lat { get; set; } = "";
        [JsonPropertyName("lon")] public string Lon { get; set; } = "";
    }
}
