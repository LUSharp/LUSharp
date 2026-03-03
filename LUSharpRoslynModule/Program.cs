using Microsoft.CodeAnalysis.CSharp;

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
        Console.WriteLine($"[TODO] Transpile: {filePath}");
        return 0;
    }

    static int TranspileAll()
    {
        Console.WriteLine("[TODO] Transpile all RoslynSource/ files");
        return 0;
    }

    static int RunReference(string[] args)
    {
        Console.WriteLine("[TODO] Run reference test");
        return 0;
    }
}
