using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Backend;

namespace LUSharpTranspiler.Build;

public static class BuildCommand
{
    public static int Run(string projectDir, string? outOverride = null, bool release = false)
    {
        ProjectConfig config;
        try { config = ProjectConfig.LoadFromDirectory(projectDir); }
        catch (FileNotFoundException e)
        {
            Console.Error.WriteLine($"ERROR: {e.Message}");
            return 1;
        }

        var srcDir = Path.Combine(projectDir, config.Build.Src.TrimStart('.', '/'));
        var outDir = outOverride ?? Path.Combine(projectDir, config.Build.Out.TrimStart('.', '/'));

        Console.WriteLine($"Building {config.Name} → {outDir}");

        var files = ScanFiles(srcDir);
        if (!files.Any()) { Console.Error.WriteLine("No .cs files found."); return 1; }

        var parsed = new List<ParsedFile>();
        foreach (var f in files)
        {
            var code = File.ReadAllText(f);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
            parsed.Add(new ParsedFile(f, tree));
        }

        var pipeline = new TransformPipeline();
        var modules = pipeline.Run(parsed);

        int written = 0;
        foreach (var module in modules)
        {
            var text = ModuleEmitter.Emit(module);
            var path = Path.Combine(outDir, module.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
            Console.WriteLine($"  > {Path.GetFileName(module.SourceFile)} → {module.OutputPath}");
            written++;
        }

        Console.WriteLine($"\nBuild complete. {written} file(s) written.");
        return 0;
    }

    private static IEnumerable<string> ScanFiles(string srcDir) =>
        Directory.Exists(srcDir)
            ? Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
}
