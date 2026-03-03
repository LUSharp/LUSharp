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
            Console.WriteLine("  reference <command>       Run reference test harness");
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
            default:
                Console.Error.WriteLine($"Unknown reference: {subcommand}");
                return 1;
        }
        return 0;
    }
}
