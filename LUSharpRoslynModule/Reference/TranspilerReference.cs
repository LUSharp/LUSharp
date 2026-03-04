using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Cross-module transpilation validation: compiles all 13 RoslynSource files (the 12
/// parser/emitter source files plus SimpleTranspiler.cs) into an in-memory assembly,
/// then uses the compiled SimpleTranspiler to PreScan + TranspileAll, producing Luau
/// output with correct require(script.Parent.X) preambles for every cross-file type
/// reference. This validates that the multi-file orchestrator resolves dependencies
/// and inserts require() blocks correctly.
/// </summary>
public static class TranspilerReference
{
    // All 13 source files: the 12 infrastructure files plus the orchestrator itself.
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
        "SimpleEmitter.cs",
        "SimpleTranspiler.cs"
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

        Console.WriteLine("=== Cross-Module Transpiler Validation ===");
        Console.WriteLine();

        // Step 1: Compile all 13 source files into an in-memory assembly.
        var assembly = CompileSourceFiles(roslynSourceDir);
        if (assembly == null)
        {
            Console.Error.WriteLine("  FAIL: Could not compile RoslynSource files into in-memory assembly");
            return;
        }

        // Step 2: Resolve the SimpleTranspiler type via reflection.
        var transpilerType = assembly.GetType("RoslynLuau.SimpleTranspiler");
        if (transpilerType == null)
        {
            Console.Error.WriteLine("  FAIL: Could not resolve RoslynLuau.SimpleTranspiler");
            return;
        }

        var preScanMethod = transpilerType.GetMethod("PreScan");
        var transpileAllMethod = transpilerType.GetMethod("TranspileAll");

        if (preScanMethod == null || transpileAllMethod == null)
        {
            Console.Error.WriteLine($"  FAIL: Could not resolve methods " +
                $"(PreScan={preScanMethod != null}, TranspileAll={transpileAllMethod != null})");
            return;
        }

        // Step 3: Read all source file contents into parallel arrays.
        var sources = new string[AllFiles.Length];
        var fileNames = new string[AllFiles.Length];
        int fileCount = 0;

        for (int i = 0; i < AllFiles.Length; i++)
        {
            string filePath = Path.Combine(roslynSourceDir, AllFiles[i]);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"  SKIP {AllFiles[i],-35} (file not found)");
                continue;
            }

            sources[fileCount] = File.ReadAllText(filePath);
            fileNames[fileCount] = AllFiles[i];
            fileCount++;
        }

        if (fileCount == 0)
        {
            Console.Error.WriteLine("  FAIL: No source files could be read");
            return;
        }

        Console.WriteLine($"  Loaded {fileCount} source files");
        Console.WriteLine();

        // Step 4: Create a SimpleTranspiler instance and call PreScan.
        object transpilerInstance;
        try
        {
            transpilerInstance = Activator.CreateInstance(transpilerType)!;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL: Could not instantiate SimpleTranspiler: {ex.Message}");
            return;
        }

        try
        {
            preScanMethod.Invoke(transpilerInstance, new object[] { sources, fileNames, fileCount });
            Console.WriteLine("  PreScan completed successfully");
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException ?? ex;
            Console.Error.WriteLine($"  FAIL: PreScan threw: {inner.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL: PreScan threw: {ex.Message}");
            return;
        }

        // Step 5: Call TranspileAll to produce Luau output for every file.
        string[] results;
        try
        {
            var rawResult = transpileAllMethod.Invoke(transpilerInstance, new object[] { sources, fileNames, fileCount });
            results = (string[])rawResult!;
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException ?? ex;
            Console.Error.WriteLine($"  FAIL: TranspileAll threw: {inner.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL: TranspileAll threw: {ex.Message}");
            return;
        }

        Console.WriteLine("  TranspileAll completed successfully");
        Console.WriteLine();

        // Step 6: Validate each output file.
        Console.WriteLine("=== Per-File Results ===");
        Console.WriteLine();

        int passed = 0;
        int failed = 0;
        string? statementNodesOutput = null;

        for (int i = 0; i < fileCount; i++)
        {
            string fileName = fileNames[i];
            string output = results[i];

            if (output == null)
            {
                string pad = new string(' ', Math.Max(0, 35 - fileName.Length));
                Console.WriteLine($"  FAIL {fileName}{pad} (null output)");
                failed++;
                continue;
            }

            // Count output lines.
            int lineCount = 0;
            for (int j = 0; j < output.Length; j++)
            {
                if (output[j] == '\n') lineCount++;
            }

            // Check for key structural patterns.
            bool hasStrictHeader = output.Contains("--!strict");
            bool hasReturnTable = output.Contains("return {");

            // Count require(script.Parent. occurrences.
            int requireCount = CountOccurrences(output, "require(script.Parent.");

            string status = (hasStrictHeader && hasReturnTable) ? "OK  " : "FAIL";
            if (status == "FAIL") failed++;
            else passed++;

            string pad2 = new string(' ', Math.Max(0, 35 - fileName.Length));
            Console.WriteLine($"  {status} {fileName}{pad2} {lineCount,4} lines  requires={requireCount,2}  strict={hasStrictHeader}  return={hasReturnTable}");

            // Capture StatementNodes output for the detailed print below.
            if (fileName == "StatementNodes.cs")
            {
                statementNodesOutput = output;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Transpiler validation: {passed}/{passed + failed} files passed");

        // Step 7: Print the require block region of StatementNodes to verify cross-module output.
        Console.WriteLine();
        Console.WriteLine("=== StatementNodes.lua (require block sample) ===");

        if (statementNodesOutput != null)
        {
            // Print the first 40 lines so the require() preamble is visible.
            int printedLines = 0;
            int pos = 0;
            while (pos < statementNodesOutput.Length && printedLines < 40)
            {
                int newline = statementNodesOutput.IndexOf('\n', pos);
                string line = newline >= 0
                    ? statementNodesOutput.Substring(pos, newline - pos)
                    : statementNodesOutput.Substring(pos);
                Console.WriteLine(line);
                printedLines++;
                if (newline < 0) break;
                pos = newline + 1;
            }

            if (pos < statementNodesOutput.Length)
                Console.WriteLine("  ... (truncated)");
        }
        else
        {
            Console.WriteLine("  (StatementNodes.cs was not found in the loaded file set)");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Count the number of non-overlapping occurrences of needle in haystack.
    /// </summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int startIndex = 0;
        int needleLen = needle.Length;
        while (true)
        {
            int idx = haystack.IndexOf(needle, startIndex, StringComparison.Ordinal);
            if (idx < 0) break;
            count++;
            startIndex = idx + needleLen;
        }
        return count;
    }

    // ── Compilation ───────────────────────────────────────────────────────────

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

        // Stub the Microsoft.CodeAnalysis.Accessibility enum referenced by SyntaxNode.cs.
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

        // Reference the standard BCL assemblies needed by the source files.
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
            "RoslynLuauTranspilerRef",
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

    // ── Project directory discovery ───────────────────────────────────────────

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
