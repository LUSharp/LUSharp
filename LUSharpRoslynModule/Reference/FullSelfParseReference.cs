using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Full self-parsing validation: compiles ALL 11 core RoslynSource files into an in-memory
/// assembly, then uses the compiled SimpleParser + TreePrinter to parse each source file.
/// This is the ultimate self-hosting test — the parser parsing its own C# source.
/// </summary>
public static class FullSelfParseReference
{
    // The 11 core source files that define the parser itself
    private static readonly string[] CoreFiles =
    {
        "SyntaxToken.cs",
        "SyntaxNode.cs",
        "ExpressionNodes.cs",
        "StatementNodes.cs",
        "DeclarationNodes.cs",
        "SyntaxKind.cs",
        "SlidingTextWindow.cs",
        "SimpleTokenizer.cs",
        "SimpleParser.cs",
        "SyntaxWalker.cs",
        "SyntaxFacts.cs"
    };

    public static void PrintAll()
    {
        var projectDir = FindProjectDir();
        if (projectDir == null)
        {
            Console.Error.WriteLine("Error: could not find LUSharpRoslynModule project directory");
            return;
        }

        var sourceDir = Path.Combine(projectDir, "RoslynSource");
        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Error: RoslynSource/ directory not found at {sourceDir}");
            return;
        }

        Console.WriteLine("=== Full Self-Parse Validation ===");
        Console.WriteLine($"Source directory: {sourceDir}");
        Console.WriteLine($"Files to parse: {CoreFiles.Length}");
        Console.WriteLine();

        // Step 1: Compile all source files into an in-memory assembly
        Console.WriteLine("Step 1: Compiling RoslynSource files into in-memory assembly...");
        var assembly = CompileSourceFiles(sourceDir);
        if (assembly == null)
        {
            Console.Error.WriteLine("  FAIL: Could not compile source files");
            return;
        }
        Console.WriteLine("  OK: Assembly compiled successfully");
        Console.WriteLine();

        // Step 2: Get the SimpleParser and TreePrinter types via reflection
        var parserType = assembly.GetType("RoslynLuau.SimpleParser");
        var treePrinterType = assembly.GetType("RoslynLuau.TreePrinter");

        if (parserType == null || treePrinterType == null)
        {
            Console.Error.WriteLine($"  FAIL: Could not find types (SimpleParser={parserType != null}, TreePrinter={treePrinterType != null})");
            return;
        }

        // Step 3: Parse each source file using the compiled parser
        Console.WriteLine("Step 2: Self-parsing each source file...");
        int passed = 0;
        int failed = 0;

        foreach (var file in CoreFiles)
        {
            var filePath = Path.Combine(sourceDir, file);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"  SKIP {file,-30} (file not found)");
                continue;
            }

            try
            {
                string source = File.ReadAllText(filePath);

                // Create SimpleParser instance: new SimpleParser(source)
                var parser = Activator.CreateInstance(parserType, source);

                // Call ParseCompilationUnit()
                var parseMethod = parserType.GetMethod("ParseCompilationUnit");
                var unit = parseMethod!.Invoke(parser, null);

                // Create TreePrinter and call Visit(unit)
                var printer = Activator.CreateInstance(treePrinterType);
                var visitMethod = treePrinterType.GetMethod("Visit",
                    new[] { assembly.GetType("RoslynLuau.SyntaxNode")! });
                visitMethod!.Invoke(printer, new[] { unit });

                // Call GetOutput()
                var getOutputMethod = treePrinterType.GetMethod("GetOutput");
                var output = (string)getOutputMethod!.Invoke(printer, null)!;

                int lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

                Console.WriteLine($"  OK   {file,-30} {lines,5} tree lines");
                passed++;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                Console.WriteLine($"  FAIL {file,-30} {inner.Message}");
                failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL {file,-30} {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Self-parse: {passed}/{CoreFiles.Length} files parsed successfully");
        if (failed > 0)
            Console.WriteLine($"  ({failed} failed)");
    }

    private static Assembly? CompileSourceFiles(string sourceDir)
    {
        var syntaxTrees = new List<Microsoft.CodeAnalysis.SyntaxTree>();

        foreach (var file in CoreFiles)
        {
            var filePath = Path.Combine(sourceDir, file);
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"  Warning: {file} not found, skipping");
                continue;
            }

            var source = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
            syntaxTrees.Add(tree);
        }

        // Add stub types that the source files reference from the real Roslyn but we don't include
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

        // Reference the standard BCL assemblies
        var references = new List<MetadataReference>();

        // Add references from the current runtime
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)); // System.Runtime
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Console.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Globalization.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Linq.dll")));

        var compilation = CSharpCompilation.Create(
            "RoslynLuauSelfParse",
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

    private static string? FindProjectDir()
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
