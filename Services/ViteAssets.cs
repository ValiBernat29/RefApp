using System.Text.Json;

namespace RefApp.Services;

/// <summary>The built Vue app's entry script and stylesheets, as paths under wwwroot (for example "dist/app-3fa9c1.js").</summary>
public record ViteEntry(string Script, IReadOnlyList<string> Styles);

/// <summary>
/// Finds the Vue app's content-hashed file names by reading the manifest Vite writes (wwwroot/dist/manifest.json).
/// The page must load the entry script from exactly the URL the lazy-loaded chunks import it from. Appending a
/// version query string to it would make the browser treat it as a second module and start the app twice.
/// </summary>
public class ViteAssets
{
    private readonly string _manifestPath;
    private readonly object _lock = new();
    private DateTime _loadedAt;
    private ViteEntry? _entry;

    public ViteAssets(IWebHostEnvironment env)
    {
        _manifestPath = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "dist", "manifest.json");
    }

    public ViteEntry GetEntry()
    {
        if (!File.Exists(_manifestPath))
        {
            throw new InvalidOperationException(
                "The Vue app has not been built: wwwroot/dist/manifest.json is missing. Run 'npm ci && npm run build' in ClientApp.");
        }

        // Re-read only when the file changes (a rebuild while the server is running).
        var stamp = File.GetLastWriteTimeUtc(_manifestPath);
        lock (_lock)
        {
            if (_entry == null || stamp != _loadedAt)
            {
                _entry = Parse(File.ReadAllText(_manifestPath));
                _loadedAt = stamp;
            }
            return _entry;
        }
    }

    private static ViteEntry Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var item in doc.RootElement.EnumerateObject())
        {
            var chunk = item.Value;
            if (!chunk.TryGetProperty("isEntry", out var isEntry) || !isEntry.GetBoolean()) continue;

            var styles = chunk.TryGetProperty("css", out var css)
                ? css.EnumerateArray().Select(c => $"dist/{c.GetString()}").ToList()
                : new List<string>();
            return new ViteEntry($"dist/{chunk.GetProperty("file").GetString()}", styles);
        }
        throw new InvalidOperationException("wwwroot/dist/manifest.json has no entry chunk. Rebuild the Vue app.");
    }
}
