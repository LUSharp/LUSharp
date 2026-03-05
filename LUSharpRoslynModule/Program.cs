using Microsoft.CodeAnalysis.CSharp;
using LUSharpRoslynModule.Transpiler;

namespace LUSharpRoslynModule;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("LUSharpRoslynModule — Roslyn C# to Luau transpiler");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  generate                  Generate RoslynSource/ files via reflection");
            Console.WriteLine("  transpile <file.cs>       Transpile a single C# file to Luau");
            Console.WriteLine("  transpile-all             Transpile all files in RoslynSource/");
            Console.WriteLine("  reference <command>       Run reference test harness (self-emit, transpiler, ...)");
            Console.WriteLine("  transpile-project <dir>   Transpile all .cs files in a directory (recursive)");
            return 1;
        }

        var command = args[0].ToLower();

        switch (command)
        {
            case "generate":
                return GenerateSources();
            case "transpile":
                if (args.Length < 2) { Console.Error.WriteLine("Error: specify a .cs file"); return 1; }
                return TranspileFile(args[1]);
            case "transpile-all":
                return TranspileAll();
            case "transpile-project":
                if (args.Length < 2) { Console.Error.WriteLine("Usage: transpile-project <directory>"); return 1; }
                return TranspileProject(args[1]);
            case "reference":
                return RunReference(args.Skip(1).ToArray());
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                return 1;
        }
    }

    static int GenerateSources()
    {
        // Find the project root (directory containing Program.cs / .csproj)
        var projectDir = FindProjectDirectory();
        if (projectDir == null)
        {
            Console.Error.WriteLine("Error: could not find LUSharpRoslynModule project directory");
            return 1;
        }

        var roslynSourceDir = Path.Combine(projectDir, "RoslynSource");
        Directory.CreateDirectory(roslynSourceDir);

        // Generate SyntaxKind.cs via reflection
        var syntaxKindPath = Path.Combine(roslynSourceDir, "SyntaxKind.cs");
        var memberCount = GenerateSyntaxKind(syntaxKindPath);

        Console.WriteLine($"Generated {syntaxKindPath}");
        Console.WriteLine($"  {memberCount} enum members");

        return 0;
    }

    static int GenerateSyntaxKind(string outputPath)
    {
        var names = Enum.GetNames<SyntaxKind>();
        var values = Enum.GetValues<SyntaxKind>();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace Microsoft.CodeAnalysis.CSharp;");
        sb.AppendLine();
        sb.AppendLine("public enum SyntaxKind : ushort");
        sb.AppendLine("{");

        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var value = (ushort)values[i];
            var comma = i < names.Length - 1 ? "," : "";
            sb.AppendLine($"    {name} = {value}{comma}");
        }

        sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());
        return names.Length;
    }

    static string? FindProjectDirectory()
    {
        // Walk up from the current directory looking for LUSharpRoslynModule.csproj
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            var csproj = Path.Combine(dir, "LUSharpRoslynModule", "LUSharpRoslynModule.csproj");
            if (File.Exists(csproj))
                return Path.Combine(dir, "LUSharpRoslynModule");

            // Also check if we're already in the project directory
            var localCsproj = Path.Combine(dir, "LUSharpRoslynModule.csproj");
            if (File.Exists(localCsproj))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    static int TranspileFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: file not found: {filePath}");
            return 1;
        }

        var projectDir = FindProjectDirectory();
        if (projectDir == null)
        {
            Console.Error.WriteLine("Error: could not find LUSharpRoslynModule project directory");
            return 1;
        }

        var outDir = Path.Combine(projectDir, "out");
        Directory.CreateDirectory(outDir);

        var sourceCode = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);

        var transpiler = new RoslynToLuau();

        // Pre-scan all source files for type-to-module mapping and overload resolution
        var roslynSourceDir = Path.Combine(projectDir, "RoslynSource");
        if (Directory.Exists(roslynSourceDir))
        {
            var allFiles = Directory.GetFiles(roslynSourceDir, "*.cs")
                .Select(f => (File.ReadAllText(f), Path.GetFileName(f)));
            transpiler.PreScan(allFiles);
        }

        var result = transpiler.Transpile(sourceCode, fileName);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Failed to transpile {fileName}:");
            foreach (var error in result.Errors)
                Console.Error.WriteLine($"  {error}");
            return 1;
        }

        var outputName = Path.GetFileNameWithoutExtension(fileName) + ".lua";
        var outputPath = Path.Combine(outDir, outputName);
        File.WriteAllText(outputPath, result.LuauSource);

        Console.WriteLine($"Transpiled {fileName} -> {outputPath}");
        return 0;
    }

    static int TranspileAll()
    {
        var projectDir = FindProjectDirectory();
        if (projectDir == null)
        {
            Console.Error.WriteLine("Error: could not find LUSharpRoslynModule project directory");
            return 1;
        }

        var roslynSourceDir = Path.Combine(projectDir, "RoslynSource");
        if (!Directory.Exists(roslynSourceDir))
        {
            Console.Error.WriteLine($"Error: RoslynSource/ directory not found at {roslynSourceDir}");
            Console.Error.WriteLine("Run 'generate' first to create source files.");
            return 1;
        }

        var outDir = Path.Combine(projectDir, "out");
        Directory.CreateDirectory(outDir);

        var csFiles = Directory.GetFiles(roslynSourceDir, "*.cs");
        if (csFiles.Length == 0)
        {
            Console.WriteLine("No .cs files found in RoslynSource/");
            return 0;
        }

        var transpiler = new RoslynToLuau();

        // Pre-scan all files to build global overload map for cross-type dispatch
        var sourceFiles = csFiles.Select(f => (File.ReadAllText(f), Path.GetFileName(f)));
        transpiler.PreScan(sourceFiles);

        int succeeded = 0;
        int failed = 0;

        foreach (var csFile in csFiles)
        {
            var sourceCode = File.ReadAllText(csFile);
            var fileName = Path.GetFileName(csFile);

            var result = transpiler.Transpile(sourceCode, fileName);

            if (result.Success)
            {
                var outputName = Path.GetFileNameWithoutExtension(fileName) + ".lua";
                var outputPath = Path.Combine(outDir, outputName);
                File.WriteAllText(outputPath, result.LuauSource);

                Console.WriteLine($"  OK  {fileName} -> {outputName}");
                succeeded++;
            }
            else
            {
                Console.Error.WriteLine($"  FAIL  {fileName}:");
                foreach (var error in result.Errors)
                    Console.Error.WriteLine($"    {error}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Transpile complete: {succeeded} succeeded, {failed} failed ({csFiles.Length} total)");
        return failed > 0 ? 1 : 0;
    }

    static int TranspileProject(string projectDir)
    {
        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"Error: directory not found: {projectDir}");
            return 1;
        }

        var outDir = Path.Combine(projectDir, "luau-out");
        Directory.CreateDirectory(outDir);

        // Recursively find all .cs files (exclude obj/, bin/, test directories)
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.AltDirectorySeparatorChar + "obj" + Path.AltDirectorySeparatorChar)
                     && !f.Contains(Path.AltDirectorySeparatorChar + "bin" + Path.AltDirectorySeparatorChar))
            .ToArray();

        if (csFiles.Length == 0)
        {
            Console.WriteLine("No .cs files found in directory.");
            return 0;
        }

        Console.WriteLine($"Found {csFiles.Length} C# files in {projectDir}");

        var transpiler = new RoslynToLuau();

        // Pre-scan all files
        Console.Write("Pre-scanning...");
        var sourceFiles = csFiles.Select(f => (File.ReadAllText(f), Path.GetFileName(f)));
        transpiler.PreScan(sourceFiles);
        Console.WriteLine(" done");

        int succeeded = 0;
        int failed = 0;
        int totalTodos = 0;

        foreach (var csFile in csFiles)
        {
            var sourceCode = File.ReadAllText(csFile);
            var fileName = Path.GetFileName(csFile);
            var relativePath = Path.GetRelativePath(projectDir, csFile);

            try
            {
                var result = transpiler.Transpile(sourceCode, fileName);

                if (result.Success)
                {
                    var outputName = Path.GetFileNameWithoutExtension(fileName) + ".lua";
                    var outputPath = Path.Combine(outDir, outputName);
                    File.WriteAllText(outputPath, result.LuauSource);

                    // Count TODOs in output
                    var todoCount = result.LuauSource.Split("--[[TODO:").Length - 1;
                    totalTodos += todoCount;

                    var todoStr = todoCount > 0 ? $"  ({todoCount} TODOs)" : "";
                    Console.WriteLine($"  OK    {relativePath}{todoStr}");
                    succeeded++;
                }
                else
                {
                    Console.Error.WriteLine($"  FAIL  {relativePath}: {result.Errors.FirstOrDefault()}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  CRASH {relativePath}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {succeeded} OK, {failed} failed ({csFiles.Length} total)");
        if (totalTodos > 0)
            Console.WriteLine($"Remaining TODOs: {totalTodos}");
        Console.WriteLine($"Output: {outDir}");
        return 0;
    }

    static int RunReference(string[] args)
    {
        var subcommand = args.Length > 0 ? args[0] : "all";
        switch (subcommand)
        {
            case "syntax-kind":
            case "all":
                Reference.SyntaxKindReference.PrintAll();
                break;
            case "syntax-kind-count":
                Reference.SyntaxKindReference.PrintCount();
                break;
            case "syntax-kind-spot":
                Reference.SyntaxKindReference.PrintSpotCheck();
                break;
            case "syntax-facts":
                Reference.SyntaxFactsReference.PrintAll();
                break;
            case "syntax-facts-chars":
                Reference.SyntaxFactsReference.PrintCharClassification();
                break;
            case "syntax-facts-gettext":
                Reference.SyntaxFactsReference.PrintGetText();
                break;
            case "tokenizer":
                Reference.TokenizerReference.PrintAll();
                break;
            case "parser":
                Reference.ParserReference.PrintAll();
                break;
            case "parser-extended":
                Reference.ExtendedParserReference.PrintAll();
                break;
            case "walker":
                Reference.WalkerReference.PrintAll();
                break;
            case "expanded-parser":
                Reference.ExpandedParserReference.PrintAll();
                break;
            case "self-parse":
                Reference.SelfParseReference.PrintAll();
                break;
            case "self-parse-all":
                Reference.FullSelfParseReference.PrintAll();
                break;
            case "emitter":
                Reference.EmitterReference.PrintAll();
                break;
            case "self-emit":
                Reference.SelfEmitReference.PrintAll();
                break;
            case "transpiler":
                Reference.TranspilerReference.PrintAll();
                break;
            case "emit-file":
                if (args.Length < 2) { Console.Error.WriteLine("Usage: reference emit-file <FileName.cs>"); return 1; }
                Reference.SelfEmitReference.EmitSingleFile(args[1]);
                break;
            default:
                Console.Error.WriteLine($"Unknown reference: {subcommand}");
                return 1;
        }
        return 0;
    }
}
