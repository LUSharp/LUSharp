using LUSharp.Project;
using LUSharpTranspiler.Build;
using Newtonsoft.Json;

namespace LUSharp;

internal static class ProjectFixer
{
    private enum CheckStatus { Ok, Fixed, Warn, Error }

    public static int Run(string projectDir)
    {
        Console.WriteLine($"Checking project in {Path.GetFullPath(projectDir)}");

        int fixed_ = 0;
        int warnings = 0;

        Report(CheckLusharpJson(projectDir), "lusharp.json", ref fixed_, ref warnings);
        Report(CheckRojoConfig(projectDir), "default.project.json", ref fixed_, ref warnings);
        Report(CheckGitignore(projectDir), ".gitignore", ref fixed_, ref warnings);
        Report(CheckSrcDirs(projectDir), "src/ directories", ref fixed_, ref warnings);
        Report(CheckLibDir(projectDir), "lib/", ref fixed_, ref warnings);
        Report(CheckApiDll(projectDir), "lib/LUSharpAPI.dll", ref fixed_, ref warnings);

        Report(CheckCsproj(projectDir), ref fixed_, ref warnings);
        Report(CheckMSBuildTarget(projectDir), ref fixed_, ref warnings);

        Console.WriteLine();
        if (fixed_ == 0 && warnings == 0)
            Console.WriteLine("Everything looks good.");
        else
            Console.WriteLine($"Fixed {fixed_} issue(s). {warnings} warning(s).");

        return 0;
    }

    private static void Report((CheckStatus status, string label) result,
        ref int fixed_, ref int warnings)
    {
        Report((result.status, ""), result.label, ref fixed_, ref warnings);
    }

    private static void Report((CheckStatus status, string detail) result, string label,
        ref int fixed_, ref int warnings)
    {
        var orig = Console.ForegroundColor;
        switch (result.status)
        {
            case CheckStatus.Ok:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [OK]    ");
                break;
            case CheckStatus.Fixed:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("  [FIXED] ");
                fixed_++;
                break;
            case CheckStatus.Warn:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("  [WARN]  ");
                warnings++;
                break;
            case CheckStatus.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [ERROR] ");
                warnings++;
                break;
        }
        Console.ForegroundColor = orig;
        Console.Write(label);
        if (!string.IsNullOrEmpty(result.detail))
            Console.Write($" — {result.detail}");
        Console.WriteLine();
    }

    private static (CheckStatus, string) CheckLusharpJson(string dir)
    {
        var path = Path.Combine(dir, "lusharp.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<ProjectConfig>(json);
                if (config != null && !string.IsNullOrWhiteSpace(config.Name))
                    return (CheckStatus.Ok, "");
            }
            catch (JsonException) { }
        }

        // Regenerate with defaults
        var name = Path.GetFileName(Path.GetFullPath(dir));
        var newConfig = new ProjectConfig { Name = name };
        File.WriteAllText(path, newConfig.ToJson());
        return (CheckStatus.Fixed, "regenerated with defaults");
    }

    private static (CheckStatus, string) CheckRojoConfig(string dir)
    {
        var path = Path.Combine(dir, "default.project.json");
        if (File.Exists(path))
            return (CheckStatus.Ok, "");

        var name = GetProjectName(dir);
        ProjectScaffolder.WriteRojoConfig(dir, name);
        return (CheckStatus.Fixed, "regenerated");
    }

    private static (CheckStatus, string) CheckGitignore(string dir)
    {
        var path = Path.Combine(dir, ".gitignore");
        var required = new[] { "bin/", "obj/", "out/", "*.user" };

        if (File.Exists(path))
        {
            var content = File.ReadAllText(path);
            var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToHashSet();
            var missing = required.Where(e => !lines.Contains(e)).ToList();
            if (missing.Count == 0)
                return (CheckStatus.Ok, "");

            File.AppendAllText(path, "\n" + string.Join("\n", missing) + "\n");
            return (CheckStatus.Fixed, $"appended {missing.Count} missing entry(ies)");
        }

        File.WriteAllText(path, string.Join("\n", required) + "\n");
        return (CheckStatus.Fixed, "created");
    }

    private static (CheckStatus, string) CheckSrcDirs(string dir)
    {
        var subdirs = new[] { "client", "server", "shared" };
        var created = new List<string>();
        foreach (var sub in subdirs)
        {
            var p = Path.Combine(dir, "src", sub);
            if (!Directory.Exists(p))
            {
                Directory.CreateDirectory(p);
                created.Add(sub);
            }
        }

        return created.Count == 0
            ? (CheckStatus.Ok, "")
            : (CheckStatus.Fixed, $"created {string.Join(", ", created)}");
    }

    private static (CheckStatus, string) CheckLibDir(string dir)
    {
        var p = Path.Combine(dir, "lib");
        if (Directory.Exists(p))
            return (CheckStatus.Ok, "");

        Directory.CreateDirectory(p);
        return (CheckStatus.Fixed, "created");
    }

    private static (CheckStatus, string) CheckApiDll(string dir)
    {
        var dest = Path.Combine(dir, "lib", "LUSharpAPI.dll");
        if (File.Exists(dest))
            return (CheckStatus.Ok, "");

        // Ensure lib/ exists
        Directory.CreateDirectory(Path.Combine(dir, "lib"));
        ProjectScaffolder.BundleApiDll(dir);

        // Verify it was actually copied
        if (File.Exists(dest))
            return (CheckStatus.Fixed, "recopied");

        return (CheckStatus.Warn, "LUSharpAPI.dll not found in install directory");
    }

    private static (CheckStatus, string) CheckCsproj(string dir)
    {
        var name = GetProjectName(dir);
        var path = Path.Combine(dir, $"{name}.csproj");
        if (File.Exists(path))
            return (CheckStatus.Ok, $"{name}.csproj");

        // Check if any .csproj exists with a different name
        var existing = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (existing.Length > 0)
            return (CheckStatus.Ok, Path.GetFileName(existing[0]));

        File.WriteAllText(path, ProjectScaffolder.GenerateCsproj());
        return (CheckStatus.Fixed, $"{name}.csproj — regenerated");
    }

    private static (CheckStatus, string) CheckMSBuildTarget(string dir)
    {
        var csprojFiles = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
            return (CheckStatus.Warn, ".csproj — no csproj file found");

        var csprojPath = csprojFiles[0];
        var csprojName = Path.GetFileName(csprojPath);
        var content = File.ReadAllText(csprojPath);

        if (content.Contains("LUSharpTranspile"))
            return (CheckStatus.Ok, $"{csprojName} MSBuild target");

        return (CheckStatus.Warn, $"{csprojName} missing LUSharpTranspile MSBuild target");
    }

    private static string GetProjectName(string dir)
    {
        var configPath = Path.Combine(dir, "lusharp.json");
        if (File.Exists(configPath))
        {
            try
            {
                var config = ProjectConfig.FromJson(File.ReadAllText(configPath));
                if (!string.IsNullOrWhiteSpace(config.Name))
                    return config.Name;
            }
            catch { }
        }
        return Path.GetFileName(Path.GetFullPath(dir));
    }
}
