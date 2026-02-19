using System.Net.Http.Headers;
using System.Text.Json;

namespace LUSharp;

internal static class UpdateChecker
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lusharp");

    private static readonly string CachePath = Path.Combine(CacheDir, "update-check.json");

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(2);

    public static void CheckAndNotify(string currentVersion)
    {
        try
        {
            string? latestVersion = GetCachedVersion();

            if (latestVersion == null)
            {
                latestVersion = GetLatestVersionAsync().GetAwaiter().GetResult();
                if (latestVersion != null)
                    WriteCache(latestVersion);
            }

            if (latestVersion != null && IsNewer(latestVersion, currentVersion))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"\x1b[33mUpdate available: v{currentVersion} \u2192 v{latestVersion}\x1b[0m");
                Console.Error.WriteLine($"\x1b[33mRun 'irm https://raw.githubusercontent.com/LUSharp/LUSharp/master/install.ps1 | iex' to update (Windows)\x1b[0m");
            }
        }
        catch
        {
            // Silently ignore all errors — update check is non-critical
        }
    }

    private static string? GetCachedVersion()
    {
        if (!File.Exists(CachePath))
            return null;

        try
        {
            var json = File.ReadAllText(CachePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("lastCheck", out var lastCheckProp) ||
                !root.TryGetProperty("latestVersion", out var versionProp))
                return null;

            var lastCheck = DateTime.Parse(lastCheckProp.GetString()!).ToUniversalTime();
            if (DateTime.UtcNow - lastCheck < CheckInterval)
                return versionProp.GetString();

            return null; // Cache expired
        }
        catch
        {
            return null;
        }
    }

    private static void WriteCache(string latestVersion)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var cache = new
            {
                lastCheck = DateTime.UtcNow.ToString("o"),
                latestVersion
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(cache));
        }
        catch
        {
            // Silently ignore write failures
        }
    }

    private static async Task<string?> GetLatestVersionAsync()
    {
        using var client = new HttpClient { Timeout = HttpTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LUSharp", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var response = await client.GetAsync(
            "https://api.github.com/repos/LUSharp/LUSharp/releases/latest");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("tag_name", out var tagProp))
        {
            var tag = tagProp.GetString();
            return tag?.TrimStart('v');
        }

        return null;
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) &&
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }
        return false;
    }
}
