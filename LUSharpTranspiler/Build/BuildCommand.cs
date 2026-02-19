using Microsoft.CodeAnalysis;
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
            Console.Error.WriteLine("No lusharp.json found. Run 'lusharp new' to create a project or specify a project directory.");
            return 1;
        }

        var srcDir = Path.Combine(projectDir, config.Build.Src.TrimStart('.', '/'));
        var outDir = outOverride ?? Path.Combine(projectDir, config.Build.Out.TrimStart('.', '/'));

        Console.WriteLine($"Building {config.Name} → {outDir}");

        var files = ScanFiles(srcDir);
        if (!files.Any()) { Console.Error.WriteLine("No .cs files found."); return 1; }

        var parsed = new List<ParsedFile>();
        int totalErrors = 0;
        int filesWithErrors = 0;

        foreach (var f in files)
        {
            var code = File.ReadAllText(f);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);

            var diagnostics = tree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (diagnostics.Any())
            {
                filesWithErrors++;
                foreach (var diag in diagnostics)
                {
                    var span = diag.Location.GetLineSpan();
                    var line = span.StartLinePosition.Line + 1;
                    var col = span.StartLinePosition.Character + 1;
                    var relPath = Path.GetRelativePath(projectDir, f);
                    Console.Error.WriteLine($"{relPath}({line},{col}): error {diag.Id}: {diag.GetMessage()}");
                    totalErrors++;
                }
            }

            parsed.Add(new ParsedFile(f, tree));
        }

        if (totalErrors > 0)
        {
            Console.Error.WriteLine($"\nBuild failed: {totalErrors} error(s) in {filesWithErrors} file(s).");
            return 1;
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
