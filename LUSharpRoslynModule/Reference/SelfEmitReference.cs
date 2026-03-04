using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Self-emit validation: compiles all 12 RoslynSource files into an in-memory assembly,
/// then uses the compiled SimpleParser + SimpleEmitter to parse and emit each source file
/// as Luau. This validates that every source file round-trips through the full pipeline.
/// </summary>
public static class SelfEmitReference
{
    // All 12 source files that define the parser/emitter infrastructure
    private static readonly string[] AllFiles =
    {
        "SyntaxToken.cs",
        "SyntaxNode.cs",
        "SyntaxKind.cs",
        "SyntaxFacts.cs",
        "SlidingTextWindow.cs",
        "SimpleTokenizer.cs",
        "SimpleParser.cs",
        "DeclarationNodes.cs",
        "ExpressionNodes.cs",
        "StatementNodes.cs",
        "SyntaxWalker.cs",
        "SimpleEmitter.cs"
    };

    public static void PrintAll()
    {
        var projectDir = FindProjectDirectory();
        if (projectDir == null)
        {
            Console.Error.WriteLine("Error: could not find LUSharpRoslynModule project directory");
            return;
        }

        var roslynSourceDir = Path.Combine(projectDir, "RoslynSource");
        if (!Directory.Exists(roslynSourceDir))
        {
            Console.Error.WriteLine($"Error: RoslynSource/ directory not found at {roslynSourceDir}");
            return;
        }

        Console.WriteLine("=== Self-Emit Validation ===");

        // Step 1: Compile all source files into an in-memory assembly
        var assembly = CompileSourceFiles(roslynSourceDir);
        if (assembly == null)
        {
            Console.Error.WriteLine("  FAIL: Could not compile RoslynSource files into in-memory assembly");
            return;
        }

        // Step 2: Resolve SimpleParser and SimpleEmitter types via reflection
        var parserType = assembly.GetType("RoslynLuau.SimpleParser");
        var emitterType = assembly.GetType("RoslynLuau.SimpleEmitter");
        var syntaxNodeType = assembly.GetType("RoslynLuau.SyntaxNode");

        if (parserType == null || emitterType == null || syntaxNodeType == null)
        {
            Console.Error.WriteLine($"  FAIL: Could not resolve types " +
                $"(SimpleParser={parserType != null}, SimpleEmitter={emitterType != null}, SyntaxNode={syntaxNodeType != null})");
            return;
        }

        var parseMethod = parserType.GetMethod("ParseCompilationUnit");
        var emitMethod = emitterType.GetMethod("Emit");

        if (parseMethod == null || emitMethod == null)
        {
            Console.Error.WriteLine($"  FAIL: Could not resolve methods " +
                $"(ParseCompilationUnit={parseMethod != null}, Emit={emitMethod != null})");
            return;
        }

        // Step 3: Parse and emit each file
        int passed = 0;
        int failed = 0;

        for (int i = 0; i < AllFiles.Length; i++)
        {
            string fileName = AllFiles[i];
            string filePath = Path.Combine(roslynSourceDir, fileName);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"  SKIP {fileName,-35} (file not found)");
                continue;
            }

            string source = File.ReadAllText(filePath);

            try
            {
                // Parse with SimpleParser (via reflection)
                var parser = Activator.CreateInstance(parserType, source);
                var compilationUnit = parseMethod.Invoke(parser, null);

                // Emit with SimpleEmitter (via reflection)
                var emitter = Activator.CreateInstance(emitterType);
                string luauOutput = (string)emitMethod.Invoke(emitter, new[] { compilationUnit })!;

                // Count output lines
                int lineCount = 0;
                for (int j = 0; j < luauOutput.Length; j++)
                {
                    if (luauOutput[j] == '\n') lineCount++;
                }

                // Check for key patterns
                bool hasStrictHeader = luauOutput.Contains("--!strict");
                bool hasReturnTable = luauOutput.Contains("return {");

                string pad = new string(' ', Math.Max(0, 35 - fileName.Length));
                Console.WriteLine($"  OK   {fileName}{pad} {lineCount} Luau lines  strict={hasStrictHeader}  return={hasReturnTable}");
                passed++;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                string pad = new string(' ', Math.Max(0, 35 - fileName.Length));
                Console.WriteLine($"  FAIL {fileName}{pad} {inner.Message}");
                failed++;
            }
            catch (Exception ex)
            {
                string pad = new string(' ', Math.Max(0, 35 - fileName.Length));
                Console.WriteLine($"  FAIL {fileName}{pad} {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Self-emit: {passed}/{passed + failed} files emitted successfully");
    }

    public static void EmitSingleFile(string targetFileName)
    {
        var projectDir = FindProjectDirectory();
        if (projectDir == null) { Console.Error.WriteLine("Error: project dir not found"); return; }
        var roslynSourceDir = Path.Combine(projectDir, "RoslynSource");
        var assembly = CompileSourceFiles(roslynSourceDir);
        if (assembly == null) { Console.Error.WriteLine("Compile failed"); return; }

        var parserType = assembly.GetType("RoslynLuau.SimpleParser")!;
        var emitterType = assembly.GetType("RoslynLuau.SimpleEmitter")!;
        var parseMethod = parserType.GetMethod("ParseCompilationUnit")!;
        var emitMethod = emitterType.GetMethod("Emit")!;

        var filePath = Path.Combine(roslynSourceDir, targetFileName);
        if (!File.Exists(filePath)) { Console.Error.WriteLine($"File not found: {filePath}"); return; }
        var source = File.ReadAllText(filePath);
        var parser = Activator.CreateInstance(parserType, source);
        var cu = parseMethod.Invoke(parser, null);
        var emitter = Activator.CreateInstance(emitterType);
        var output = (string)emitMethod.Invoke(emitter, new[] { cu })!;
        Console.Write(output);
    }

    private static Assembly? CompileSourceFiles(string sourceDir)
    {
        var syntaxTrees = new List<Microsoft.CodeAnalysis.SyntaxTree>();

        foreach (var file in AllFiles)
        {
            var filePath = Path.Combine(sourceDir, file);
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"  Warning: {file} not found, skipping from compilation");
                continue;
            }

            var source = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
            syntaxTrees.Add(tree);
        }

        // Stub types referenced from the real Roslyn but not included in our source files
        var stubs = @"
namespace Microsoft.CodeAnalysis
{
    public enum Accessibility
    {
        NotApplicable = 0,
        Private = 1,
        ProtectedAndInternal = 2,
        Protected = 3,
        Internal = 4,
        ProtectedOrInternal = 5,
        Public = 6,
    }
}";
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(stubs, path: "Stubs.cs"));

        // Reference the standard BCL assemblies needed by the source files
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Console.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Globalization.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Linq.dll")),
        };

        var compilation = CSharpCompilation.Create(
            "RoslynLuauSelfEmit",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Disable));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(20);
            foreach (var error in errors)
                Console.Error.WriteLine($"  Compile error: {error}");
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    // Same directory finder as Program.cs
    private static string? FindProjectDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            var csproj = Path.Combine(dir, "LUSharpRoslynModule", "LUSharpRoslynModule.csproj");
            if (File.Exists(csproj))
                return Path.Combine(dir, "LUSharpRoslynModule");
            var localCsproj = Path.Combine(dir, "LUSharpRoslynModule.csproj");
            if (File.Exists(localCsproj))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
