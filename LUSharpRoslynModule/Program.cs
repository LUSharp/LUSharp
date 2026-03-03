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
            Console.WriteLine("  transpile <file.cs>       Transpile a single C# file to Luau");
            Console.WriteLine("  transpile-all             Transpile all files in RoslynSource/");
            Console.WriteLine("  reference <command>       Run reference test harness");
            return 1;
        }

        var command = args[0].ToLower();

        switch (command)
        {
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
