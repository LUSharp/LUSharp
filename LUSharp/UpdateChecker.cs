using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LUSharp;

internal static class UpdateChecker
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lusharp");

    private static readonly string CachePath = Path.Combine(CacheDir, "update-check.json");

    private const string Repo = "LUSharp/LUSharp";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan NotifyTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(5);

    // --- Passive update notice (after every command) ---

    public static void CheckAndNotify(string currentVersion)
    {
        try
        {
            string? latestVersion = GetCachedVersion();

            if (latestVersion == null)
            {
                latestVersion = GetLatestReleaseTagAsync(NotifyTimeout).GetAwaiter().GetResult();
                if (latestVersion != null)
                    WriteCache(latestVersion);
            }

            if (latestVersion != null && IsNewer(latestVersion, currentVersion))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"\x1b[33mUpdate available: v{currentVersion} \u2192 v{latestVersion}\x1b[0m");
                Console.Error.WriteLine($"\x1b[33mRun 'lusharp update' to update\x1b[0m");
            }
        }
        catch
        {
            // Silently ignore all errors — update check is non-critical
        }
    }

    // --- Active self-update (lusharp update) ---

    public static int RunUpdate(string currentVersion)
    {
        Console.WriteLine("Checking for updates...");

        string? latestVersion;
        try
        {
            latestVersion = GetLatestReleaseTagAsync(UpdateTimeout).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Log(Logger.LogSeverity.Error, $"Failed to check for updates: {ex.Message}");
            return 1;
        }

        if (latestVersion == null)
        {
            Logger.Log(Logger.LogSeverity.Error, "Could not determine latest version.");
            return 1;
        }

        if (!IsNewer(latestVersion, currentVersion))
        {
            Console.WriteLine($"Already up to date (v{currentVersion}).");
            return 0;
        }

        Console.WriteLine($"Updating v{currentVersion} \u2192 v{latestVersion}...");

        string? downloadUrl;
        try
        {
            downloadUrl = GetReleaseAssetUrlAsync(latestVersion).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Log(Logger.LogSeverity.Error, $"Failed to get download URL: {ex.Message}");
            return 1;
        }

        if (downloadUrl == null)
        {
            Logger.Log(Logger.LogSeverity.Error, $"No release asset found for this platform.");
            return 1;
        }

        try
        {
            DownloadAndInstall(downloadUrl, latestVersion);
            WriteCache(latestVersion);
            Console.WriteLine($"\x1b[32mSuccessfully updated to v{latestVersion}!\x1b[0m");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Log(Logger.LogSeverity.Error, $"Update failed: {ex.Message}");
            return 1;
        }
    }

    // --- GitHub API helpers ---

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LUSharp", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static async Task<string?> GetLatestReleaseTagAsync(TimeSpan timeout)
    {
        using var client = CreateClient(timeout);
        var response = await client.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("tag_name", out var tagProp))
            return tagProp.GetString()?.TrimStart('v');

        return null;
    }

    private static async Task<string?> GetReleaseAssetUrlAsync(string version)
    {
        var assetName = GetPlatformAssetName();
        if (assetName == null) return null;

        using var client = CreateClient(UpdateTimeout);
        var response = await client.GetAsync($"https://api.github.com/repos/{Repo}/releases/tags/v{version}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var nameProp) &&
                nameProp.GetString() == assetName &&
                asset.TryGetProperty("browser_download_url", out var urlProp))
            {
                return urlProp.GetString();
            }
        }

        return null;
    }

    private static string? GetPlatformAssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "lusharp-win-x64.zip";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "lusharp-linux-x64.tar.gz";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "lusharp-osx-x64.tar.gz";
        return null;
    }

    // --- Download & install ---

    private static void DownloadAndInstall(string downloadUrl, string version)
    {
        var installDir = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("Cannot determine install directory.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"lusharp-update-{version}");
        var tempArchive = Path.Combine(Path.GetTempPath(), $"lusharp-update-{version}{GetArchiveExtension()}");

        try
        {
            // Download
            Console.WriteLine($"  Downloading...");
            using (var client = CreateClient(UpdateTimeout))
            {
                using var stream = client.GetStreamAsync(downloadUrl).GetAwaiter().GetResult();
                using var file = File.Create(tempArchive);
                stream.CopyTo(file);
            }

            // Extract
            Console.WriteLine($"  Extracting...");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            if (tempArchive.EndsWith(".zip"))
                ZipFile.ExtractToDirectory(tempArchive, tempDir);
            else
                ExtractTarGz(tempArchive, tempDir);

            // Replace files
            Console.WriteLine($"  Installing to {installDir}...");
            foreach (var srcFile in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, srcFile);
                var destFile = Path.Combine(installDir, relativePath);
                var destDir = Path.GetDirectoryName(destFile)!;

                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                // On Windows, rename the running exe instead of overwriting
                if (File.Exists(destFile) && IsCurrentProcess(destFile))
                {
                    var oldPath = destFile + ".old";
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                    File.Move(destFile, oldPath);
                }

                File.Copy(srcFile, destFile, overwrite: true);
            }
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempArchive))
                try { File.Delete(tempArchive); } catch { }
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { }
        }

        // Clean up .old files from previous updates
        CleanupOldFiles(installDir);
    }

    private static bool IsCurrentProcess(string filePath)
    {
        var processPath = Environment.ProcessPath;
        if (processPath == null) return false;
        return string.Equals(
            Path.GetFullPath(filePath),
            Path.GetFullPath(processPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupOldFiles(string dir)
    {
        try
        {
            foreach (var old in Directory.GetFiles(dir, "*.old"))
                try { File.Delete(old); } catch { }
        }
        catch { }
    }

    private static string GetArchiveExtension()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".zip" : ".tar.gz";
    }

    private static void ExtractTarGz(string archivePath, string destDir)
    {
        Directory.CreateDirectory(destDir);
        // Use tar command available on modern Linux/macOS (and Windows 10+)
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tar",
            ArgumentList = { "-xzf", archivePath, "-C", destDir },
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start tar.");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"tar extraction failed: {err}");
        }
    }

    // --- Cache ---

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

    internal static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) &&
            Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }
        return false;
    }
}
