# Roslyn-to-Luau Transpilation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a standalone C#→Luau transpiler in LUSharpRoslynModule that converts decompiled Roslyn source files to Luau, validated via a TestPlugin in Roblox Studio. Layer 1 target: SyntaxKind enum.

**Architecture:** LUSharpRoslynModule uses Roslyn NuGet to parse decompiled Roslyn `.cs` files, walks the syntax trees, and emits Luau modules. Output goes into a TestPlugin that runs in Roblox Studio alongside a .NET BCL runtime library. C# reference harness produces expected output for comparison.

**Tech Stack:** C# / .NET 8.0, Microsoft.CodeAnalysis.CSharp 5.0, ILSpy CLI (decompilation), Luau (TestPlugin), Rojo (plugin build)

---

### Task 1: Project Setup — LUSharpRoslynModule

**Files:**
- Modify: `LUSharpRoslynModule/LUSharpRoslynModule.csproj`
- Modify: `LUSharpRoslynModule/Program.cs`

**Step 1: Add Roslyn NuGet reference and create folder structure**

Edit `LUSharpRoslynModule/LUSharpRoslynModule.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
  </ItemGroup>

</Project>
```

**Step 2: Create directory structure**

```bash
mkdir -p LUSharpRoslynModule/Transpiler
mkdir -p LUSharpRoslynModule/RoslynSource
mkdir -p LUSharpRoslynModule/Reference
mkdir -p LUSharpRoslynModule/out
```

**Step 3: Stub out Program.cs with CLI argument handling**

Replace `LUSharpRoslynModule/Program.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  transpile <file.cs>       Transpile C# file to Luau");
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
```

**Step 4: Verify it builds**

Run: `dotnet build LUSharpRoslynModule`
Expected: Build succeeded with 0 errors.

**Step 5: Commit**

```bash
git add LUSharpRoslynModule/
git commit -m "feat(roslyn-module): scaffold project with Roslyn NuGet and CLI"
```

---

### Task 2: Install ILSpy CLI and Decompile SyntaxKind

**Files:**
- Create: `LUSharpRoslynModule/RoslynSource/SyntaxKind.cs`

**Step 1: Install ILSpy CLI tool**

```bash
dotnet tool install -g ilspycmd
```

Expected: Tool installed successfully (or already installed).

**Step 2: Locate the Roslyn assembly**

The DLL is at: `~/.nuget/packages/microsoft.codeanalysis.csharp/5.0.0/lib/net9.0/Microsoft.CodeAnalysis.CSharp.dll`

Verify it exists:
```bash
ls ~/.nuget/packages/microsoft.codeanalysis.csharp/5.0.0/lib/net9.0/Microsoft.CodeAnalysis.CSharp.dll
```

**Step 3: Decompile SyntaxKind enum**

```bash
ilspycmd ~/.nuget/packages/microsoft.codeanalysis.csharp/5.0.0/lib/net9.0/Microsoft.CodeAnalysis.CSharp.dll -t Microsoft.CodeAnalysis.CSharp.SyntaxKind > LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
```

If `ilspycmd -t` flag doesn't support type filtering, use the alternative approach — write a small C# helper that uses reflection to generate the source:

```csharp
// Fallback: generate SyntaxKind.cs via reflection
using Microsoft.CodeAnalysis.CSharp;

var sb = new System.Text.StringBuilder();
sb.AppendLine("namespace Microsoft.CodeAnalysis.CSharp;");
sb.AppendLine();
sb.AppendLine("public enum SyntaxKind : ushort");
sb.AppendLine("{");
foreach (var name in Enum.GetNames<SyntaxKind>())
{
    var value = (ushort)(SyntaxKind)Enum.Parse<SyntaxKind>(name);
    sb.AppendLine($"    {name} = {value},");
}
sb.AppendLine("}");
File.WriteAllText("LUSharpRoslynModule/RoslynSource/SyntaxKind.cs", sb.ToString());
```

**Step 4: Verify the decompiled file exists and looks correct**

```bash
head -20 LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
wc -l LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
```

Expected: An enum with ~600+ members, each with an integer value.

**Step 5: Commit**

```bash
git add LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
git commit -m "feat(roslyn-module): decompile Roslyn SyntaxKind enum source"
```

---

### Task 3: Build TypeMapper

**Files:**
- Create: `LUSharpRoslynModule/Transpiler/TypeMapper.cs`

**Step 1: Create TypeMapper with C# → Luau type mappings**

Create `LUSharpRoslynModule/Transpiler/TypeMapper.cs`:

```csharp
namespace LUSharpRoslynModule.Transpiler;

/// <summary>
/// Maps C# types to their Luau equivalents.
/// </summary>
public static class TypeMapper
{
    private static readonly Dictionary<string, string> PrimitiveMap = new()
    {
        ["int"] = "number",
        ["uint"] = "number",
        ["long"] = "number",
        ["ulong"] = "number",
        ["short"] = "number",
        ["ushort"] = "number",
        ["byte"] = "number",
        ["sbyte"] = "number",
        ["float"] = "number",
        ["double"] = "number",
        ["decimal"] = "number",
        ["bool"] = "boolean",
        ["string"] = "string",
        ["char"] = "number",
        ["object"] = "any",
        ["void"] = "()",
        ["Int32"] = "number",
        ["UInt32"] = "number",
        ["Int64"] = "number",
        ["UInt64"] = "number",
        ["Int16"] = "number",
        ["UInt16"] = "number",
        ["Byte"] = "number",
        ["SByte"] = "number",
        ["Single"] = "number",
        ["Double"] = "number",
        ["Decimal"] = "number",
        ["Boolean"] = "boolean",
        ["String"] = "string",
        ["Char"] = "number",
        ["Object"] = "any",
        ["Void"] = "()",
    };

    /// <summary>
    /// Maps a C# type name to a Luau type annotation.
    /// Returns null if no mapping exists (caller should use the type name as-is).
    /// </summary>
    public static string? MapType(string csharpType)
    {
        if (PrimitiveMap.TryGetValue(csharpType, out var luauType))
            return luauType;
        return null;
    }

    /// <summary>
    /// Maps a C# enum base type to a Luau type.
    /// Enums backed by integer types become `number` in Luau.
    /// </summary>
    public static string MapEnumBaseType(string? baseType)
    {
        return baseType switch
        {
            "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" => "number",
            _ => "number" // default: all enums are number-backed in Luau
        };
    }
}
```

**Step 2: Verify it builds**

Run: `dotnet build LUSharpRoslynModule`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add LUSharpRoslynModule/Transpiler/TypeMapper.cs
git commit -m "feat(roslyn-module): add TypeMapper for C# to Luau type conversion"
```

---

### Task 4: Build LuauEmitter — Enum Support

**Files:**
- Create: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

This is the core emitter. Starting with enum support only (for SyntaxKind).

**Step 1: Create LuauEmitter with enum emission**

Create `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`:

```csharp
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpRoslynModule.Transpiler;

/// <summary>
/// Emits Luau source code from Roslyn syntax nodes.
/// </summary>
public class LuauEmitter
{
    private readonly StringBuilder _sb = new();
    private int _indent = 0;

    public string GetOutput() => _sb.ToString();

    public void EmitHeader()
    {
        AppendLine("--!strict");
        AppendLine("-- Auto-generated by LUSharpRoslynModule");
        AppendLine("-- Do not edit manually");
        AppendLine();
    }

    public void EmitEnum(EnumDeclarationSyntax enumDecl)
    {
        var name = enumDecl.Identifier.Text;

        // Check for [Flags] attribute
        bool isFlags = enumDecl.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(a => a.Name.ToString() is "Flags" or "System.Flags" or "FlagsAttribute" or "System.FlagsAttribute");

        AppendLine($"local {name} = table.freeze({{");
        _indent++;

        foreach (var member in enumDecl.Members)
        {
            var memberName = member.Identifier.Text;
            var value = member.EqualsValue?.Value.ToString() ?? "0";
            AppendLine($"{memberName} = {value},");
        }

        _indent--;
        AppendLine("})");
        AppendLine();

        // Export type as number (integer-valued enum)
        AppendLine($"export type {name} = number");
        AppendLine();

        // If [Flags], emit bitwise helper
        if (isFlags)
        {
            EmitFlagsHelpers(name);
        }

        // Reverse lookup table for debugging
        AppendLine($"local {name}_Name: {{ [number]: string }} = table.freeze({{");
        _indent++;
        foreach (var member in enumDecl.Members)
        {
            var memberName = member.Identifier.Text;
            var value = member.EqualsValue?.Value.ToString() ?? "0";
            AppendLine($"[{value}] = \"{memberName}\",");
        }
        _indent--;
        AppendLine("})");
        AppendLine();

        AppendLine($"return {{ {name} = {name}, {name}_Name = {name}_Name }}");
    }

    private void EmitFlagsHelpers(string enumName)
    {
        AppendLine($"local function {enumName}_HasFlag(value: number, flag: number): boolean");
        _indent++;
        AppendLine("return bit32.band(value, flag) == flag");
        _indent--;
        AppendLine("end");
        AppendLine();
    }

    private void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
            return;
        }
        _sb.Append(new string('\t', _indent));
        _sb.AppendLine(line);
    }
}
```

**Step 2: Verify it builds**

Run: `dotnet build LUSharpRoslynModule`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add LUSharpRoslynModule/Transpiler/LuauEmitter.cs
git commit -m "feat(roslyn-module): add LuauEmitter with enum support"
```

---

### Task 5: Build RoslynToLuau Orchestrator

**Files:**
- Create: `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs`

**Step 1: Create the orchestrator that ties parsing to emission**

Create `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpRoslynModule.Transpiler;

/// <summary>
/// Orchestrates C# → Luau transpilation.
/// Parses C# source with Roslyn, walks the syntax tree, emits Luau via LuauEmitter.
/// </summary>
public class RoslynToLuau
{
    /// <summary>
    /// Transpile a single C# source file to Luau.
    /// </summary>
    public TranspileResult Transpile(string sourceCode, string fileName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
        var root = tree.GetCompilationUnitRoot();

        // Check for parse errors
        var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (diagnostics.Count > 0)
        {
            return new TranspileResult
            {
                Success = false,
                FileName = fileName,
                Errors = diagnostics.Select(d => d.ToString()).ToList()
            };
        }

        var emitter = new LuauEmitter();
        emitter.EmitHeader();

        // Walk top-level declarations
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                // Future: ClassDeclarationSyntax, StructDeclarationSyntax, etc.
                default:
                    Console.Error.WriteLine($"Warning: unsupported top-level declaration: {member.Kind()}");
                    break;
            }
        }

        return new TranspileResult
        {
            Success = true,
            FileName = fileName,
            LuauSource = emitter.GetOutput()
        };
    }

    private void EmitNamespaceMembers(SyntaxList<MemberDeclarationSyntax> members, LuauEmitter emitter)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                // Future: classes, structs, etc.
                default:
                    Console.Error.WriteLine($"Warning: unsupported member: {member.Kind()}");
                    break;
            }
        }
    }
}

public class TranspileResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = "";
    public string LuauSource { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}
```

**Step 2: Wire up Program.cs to use the orchestrator**

Update `LUSharpRoslynModule/Program.cs` — replace the `TranspileFile` and `TranspileAll` stubs:

```csharp
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
            Console.WriteLine("  transpile <file.cs>       Transpile a single C# file to Luau");
            Console.WriteLine("  transpile-all             Transpile all files in RoslynSource/");
            Console.WriteLine("  reference <command>       Run reference test harness");
            return 1;
        }

        var command = args[0].ToLower();

        return command switch
        {
            "transpile" when args.Length >= 2 => TranspileFile(args[1]),
            "transpile" => Error("Error: specify a .cs file"),
            "transpile-all" => TranspileAll(),
            "reference" => RunReference(args.Skip(1).ToArray()),
            _ => Error($"Unknown command: {command}")
        };
    }

    static int TranspileFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        var source = File.ReadAllText(filePath);
        var transpiler = new RoslynToLuau();
        var result = transpiler.Transpile(source, Path.GetFileName(filePath));

        if (!result.Success)
        {
            Console.Error.WriteLine($"Transpilation failed for {filePath}:");
            foreach (var error in result.Errors)
                Console.Error.WriteLine($"  {error}");
            return 1;
        }

        // Write output
        var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out");
        Directory.CreateDirectory(outDir);

        var outFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(filePath) + ".lua");
        File.WriteAllText(outFile, result.LuauSource);
        Console.WriteLine($"OK: {filePath} -> {outFile}");
        return 0;
    }

    static int TranspileAll()
    {
        var srcDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "RoslynSource");
        if (!Directory.Exists(srcDir))
        {
            Console.Error.WriteLine($"RoslynSource directory not found: {srcDir}");
            return 1;
        }

        var files = Directory.GetFiles(srcDir, "*.cs");
        if (files.Length == 0)
        {
            Console.WriteLine("No .cs files found in RoslynSource/");
            return 0;
        }

        var transpiler = new RoslynToLuau();
        var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out");
        Directory.CreateDirectory(outDir);

        int success = 0, failed = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var result = transpiler.Transpile(source, Path.GetFileName(file));

            if (result.Success)
            {
                var outFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".lua");
                File.WriteAllText(outFile, result.LuauSource);
                Console.WriteLine($"OK: {Path.GetFileName(file)}");
                success++;
            }
            else
            {
                Console.Error.WriteLine($"FAIL: {Path.GetFileName(file)}");
                foreach (var error in result.Errors)
                    Console.Error.WriteLine($"  {error}");
                failed++;
            }
        }

        Console.WriteLine($"\nDone: {success} succeeded, {failed} failed");
        return failed > 0 ? 1 : 0;
    }

    static int RunReference(string[] args)
    {
        Console.WriteLine("[TODO] Run reference test");
        return 0;
    }

    static int Error(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
```

**Step 3: Build and test with SyntaxKind.cs**

```bash
dotnet build LUSharpRoslynModule
dotnet run --project LUSharpRoslynModule -- transpile LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
```

Expected: `OK: SyntaxKind.cs -> out/SyntaxKind.lua` (or error messages showing what needs fixing in the emitter).

**Step 4: Inspect the output**

```bash
head -30 LUSharpRoslynModule/out/SyntaxKind.lua
wc -l LUSharpRoslynModule/out/SyntaxKind.lua
```

Expected: A valid Luau file with `--!strict`, a `table.freeze()` call, and ~600+ enum entries.

**Step 5: Fix any issues**

If the decompiled SyntaxKind.cs uses features the emitter doesn't handle (hex literals, bitwise expressions in values, comments, etc.), fix the `LuauEmitter.EmitEnum` method to handle them. Common issues:
- Hex literals (`0x1234`) — Luau supports these natively, pass through as-is
- Bitwise OR in values (`Flag1 | Flag2`) — emit as `bit32.bor(Flag1, Flag2)` or pre-compute
- XML doc comments — skip/ignore

**Step 6: Commit**

```bash
git add LUSharpRoslynModule/
git commit -m "feat(roslyn-module): add RoslynToLuau orchestrator and wire CLI"
```

---

### Task 6: Build Reference Test Harness

**Files:**
- Create: `LUSharpRoslynModule/Reference/SyntaxKindReference.cs`
- Modify: `LUSharpRoslynModule/Program.cs` (wire up reference command)

**Step 1: Create SyntaxKind reference output generator**

Create `LUSharpRoslynModule/Reference/SyntaxKindReference.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Generates reference output by querying the real Roslyn SyntaxKind enum.
/// Used for validating that the transpiled Luau version matches.
/// </summary>
public static class SyntaxKindReference
{
    public static void PrintAll()
    {
        foreach (var name in Enum.GetNames<SyntaxKind>())
        {
            var value = (ushort)(SyntaxKind)Enum.Parse<SyntaxKind>(name);
            Console.WriteLine($"{name}={value}");
        }
    }

    public static void PrintCount()
    {
        Console.WriteLine($"SyntaxKind member count: {Enum.GetNames<SyntaxKind>().Length}");
    }

    /// <summary>
    /// Spot-check specific values that are commonly used.
    /// </summary>
    public static void PrintSpotCheck()
    {
        var checks = new[]
        {
            SyntaxKind.None,
            SyntaxKind.ClassKeyword,
            SyntaxKind.IntKeyword,
            SyntaxKind.StringKeyword,
            SyntaxKind.IfKeyword,
            SyntaxKind.SemicolonToken,
            SyntaxKind.OpenBraceToken,
            SyntaxKind.CloseBraceToken,
            SyntaxKind.IdentifierToken,
            SyntaxKind.NumericLiteralToken,
            SyntaxKind.StringLiteralToken,
        };

        foreach (var kind in checks)
        {
            Console.WriteLine($"{kind}={(ushort)kind}");
        }
    }
}
```

**Step 2: Wire reference command in Program.cs**

Update the `RunReference` method in `Program.cs`:

```csharp
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
```

**Step 3: Generate reference output and save**

```bash
dotnet run --project LUSharpRoslynModule -- reference syntax-kind > LUSharpRoslynModule/Reference/syntax-kind-expected.txt
dotnet run --project LUSharpRoslynModule -- reference syntax-kind-count
```

Expected: A file with all `Name=Value` pairs, and a count printed to console.

**Step 4: Commit**

```bash
git add LUSharpRoslynModule/Reference/
git commit -m "feat(roslyn-module): add SyntaxKind reference test harness"
```

---

### Task 7: Create TestPlugin Structure

**Files:**
- Create: `TestPlugin/test-plugin.project.json` (Rojo project file)
- Create: `TestPlugin/src/init.server.lua`
- Create: `TestPlugin/src/runtime/init.lua`
- Create: `TestPlugin/src/modules/.gitkeep`

**Step 1: Create Rojo project file**

Create `TestPlugin/test-plugin.project.json`:

```json
{
  "name": "LUSharp-TestPlugin",
  "tree": {
    "$path": "src"
  }
}
```

**Step 2: Create plugin entry point**

Create `TestPlugin/src/init.server.lua`:

```lua
--!strict
-- LUSharp TestPlugin — Validates transpiled Roslyn Luau modules
-- Compare output with: dotnet run --project LUSharpRoslynModule -- reference syntax-kind

local PLUGIN_VERSION = "0.1.0"

warn("[LUSharp-Test] TestPlugin v" .. PLUGIN_VERSION .. " loaded")

-- Module loader
local modules = script:FindFirstChild("modules")
local runtime = script:FindFirstChild("runtime")

if not modules then
	warn("[LUSharp-Test] No modules folder found — nothing to test")
	return
end

-- Test runner
local function runSyntaxKindTest()
	local syntaxKindModule = modules:FindFirstChild("SyntaxKind")
	if not syntaxKindModule then
		warn("[LUSharp-Test] SyntaxKind module not found, skipping")
		return
	end

	local ok, result = pcall(require, syntaxKindModule)
	if not ok then
		warn("[LUSharp-Test] FAIL: SyntaxKind failed to load: " .. tostring(result))
		return
	end

	local SyntaxKind = result.SyntaxKind
	local SyntaxKind_Name = result.SyntaxKind_Name

	if not SyntaxKind then
		warn("[LUSharp-Test] FAIL: SyntaxKind table not found in module return")
		return
	end

	-- Count members
	local count = 0
	for _ in SyntaxKind do
		count += 1
	end
	print("[LUSharp-Test] SyntaxKind member count: " .. count)

	-- Print all values (for comparison with C# reference output)
	-- Format: Name=Value (matches C# reference format)
	local entries: { { name: string, value: number } } = {}
	for name, value in SyntaxKind do
		table.insert(entries, { name = name, value = value })
	end

	-- Sort by value for deterministic output
	table.sort(entries, function(a, b) return a.value < b.value end)

	for _, entry in entries do
		print(entry.name .. "=" .. entry.value)
	end

	-- Spot check reverse lookup
	if SyntaxKind_Name then
		local spotValue = SyntaxKind.None
		if spotValue ~= nil and SyntaxKind_Name[spotValue] then
			print("[LUSharp-Test] Reverse lookup SyntaxKind[" .. spotValue .. "] = " .. SyntaxKind_Name[spotValue])
		end
	end

	warn("[LUSharp-Test] SyntaxKind test completed")
end

-- Run all tests
warn("[LUSharp-Test] Starting tests...")
runSyntaxKindTest()
warn("[LUSharp-Test] All tests finished")
```

**Step 3: Create minimal runtime module**

Create `TestPlugin/src/runtime/init.lua`:

```lua
--!strict
-- LUSharp .NET BCL Runtime for Luau
-- Provides .NET type equivalents used by transpiled Roslyn code

local Runtime = {}

-- System namespace
Runtime.System = {
	-- Char utilities (Layer 2)
	-- String utilities (Layer 2)
	-- Math utilities (Layer 3)
}

-- Collections namespace (Layer 3+)
Runtime.Collections = {}

-- Text namespace (Layer 3+)
Runtime.Text = {}

-- Bit operations helper for [Flags] enums
Runtime.Flags = {
	HasFlag = function(value: number, flag: number): boolean
		return bit32.band(value, flag) == flag
	end,
	SetFlag = function(value: number, flag: number): number
		return bit32.bor(value, flag)
	end,
	ClearFlag = function(value: number, flag: number): number
		return bit32.band(value, bit32.bnot(flag))
	end,
}

return Runtime
```

**Step 4: Create modules placeholder**

```bash
touch TestPlugin/src/modules/.gitkeep
```

**Step 5: Verify Rojo can build the plugin**

```bash
rojo build TestPlugin/test-plugin.project.json -o TestPlugin/LUSharp-TestPlugin.rbxmx
```

Expected: Plugin file created at `TestPlugin/LUSharp-TestPlugin.rbxmx`.

If Rojo is not installed, note that and skip — the plugin can also be loaded manually in Studio.

**Step 6: Commit**

```bash
git add TestPlugin/
git commit -m "feat(test-plugin): scaffold TestPlugin with runtime and test harness"
```

---

### Task 8: Transpile SyntaxKind and Deploy to TestPlugin

**Files:**
- The `LUSharpRoslynModule/out/SyntaxKind.lua` produced by Task 5
- Copy to: `TestPlugin/src/modules/SyntaxKind.lua`

**Step 1: Run the transpiler on SyntaxKind**

```bash
dotnet run --project LUSharpRoslynModule -- transpile LUSharpRoslynModule/RoslynSource/SyntaxKind.cs
```

Expected: `OK: SyntaxKind.cs -> out/SyntaxKind.lua`

**Step 2: Review the output for obvious issues**

```bash
head -50 LUSharpRoslynModule/out/SyntaxKind.lua
tail -10 LUSharpRoslynModule/out/SyntaxKind.lua
```

Check that:
- Starts with `--!strict`
- Has `local SyntaxKind = table.freeze({`
- Each entry is `MemberName = numericValue,`
- Ends with `return { SyntaxKind = SyntaxKind, ... }`

**Step 3: Copy to TestPlugin**

```bash
cp LUSharpRoslynModule/out/SyntaxKind.lua TestPlugin/src/modules/SyntaxKind.lua
```

**Step 4: Build TestPlugin**

```bash
rojo build TestPlugin/test-plugin.project.json -o TestPlugin/LUSharp-TestPlugin.rbxmx
```

**Step 5: Install TestPlugin in Roblox Studio**

Copy the built `.rbxmx` to the Roblox Studio plugins folder:

```bash
cp TestPlugin/LUSharp-TestPlugin.rbxmx "$LOCALAPPDATA/Roblox/Plugins/LUSharp-TestPlugin.rbxmx"
```

**Step 6: Commit**

```bash
git add TestPlugin/src/modules/SyntaxKind.lua
git commit -m "feat(test-plugin): add transpiled SyntaxKind module"
```

---

### Task 9: Validate — Compare C# and Luau Output

**Step 1: Generate C# reference output**

```bash
dotnet run --project LUSharpRoslynModule -- reference syntax-kind > /tmp/syntax-kind-csharp.txt
dotnet run --project LUSharpRoslynModule -- reference syntax-kind-count
```

Note the member count and save the full output.

**Step 2: Run TestPlugin in Roblox Studio**

1. Open Roblox Studio
2. The TestPlugin should load automatically
3. Check the Output window for `[LUSharp-Test]` messages
4. The plugin will print all `Name=Value` pairs

**Step 3: Compare outputs**

Copy the Roblox Studio output and compare with the C# reference:
- Member counts should match
- Each `Name=Value` pair should be identical
- Sort order may differ (Luau iterates tables in insertion order vs C# sorts alphabetically)

**Step 4: Fix any discrepancies**

Common issues to fix:
- **Missing members:** Emitter skipped some entries (check for comments, conditional compilation, etc.)
- **Wrong values:** Hex vs decimal mismatch, expression evaluation needed
- **Extra entries:** Emitter produced duplicate or synthetic entries

**Step 5: Re-transpile and re-test if fixes were needed**

Repeat Steps 1-4 until outputs match.

**Step 6: Final commit**

```bash
git add -A
git commit -m "feat(roslyn-module): validated SyntaxKind transpilation — Layer 1 complete"
```

---

### Task 10: Add Copy Script for Workflow Automation

**Files:**
- Create: `LUSharpRoslynModule/deploy.sh`

**Step 1: Create a deploy script that transpiles and copies to TestPlugin**

Create `LUSharpRoslynModule/deploy.sh`:

```bash
#!/bin/bash
# Transpile all RoslynSource files and deploy to TestPlugin

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUT_DIR="$SCRIPT_DIR/out"
MODULES_DIR="$REPO_ROOT/TestPlugin/src/modules"

echo "=== Transpiling RoslynSource files ==="
dotnet run --project "$SCRIPT_DIR" -- transpile-all

echo ""
echo "=== Copying to TestPlugin/src/modules ==="
mkdir -p "$MODULES_DIR"
cp "$OUT_DIR"/*.lua "$MODULES_DIR/"
echo "Copied $(ls "$OUT_DIR"/*.lua | wc -l) modules"

# Build TestPlugin if rojo is available
if command -v rojo &> /dev/null; then
    echo ""
    echo "=== Building TestPlugin ==="
    rojo build "$REPO_ROOT/TestPlugin/test-plugin.project.json" -o "$REPO_ROOT/TestPlugin/LUSharp-TestPlugin.rbxmx"
    echo "Built TestPlugin.rbxmx"

    # Copy to Roblox plugins folder
    PLUGINS_DIR="$LOCALAPPDATA/Roblox/Plugins"
    if [ -d "$PLUGINS_DIR" ]; then
        cp "$REPO_ROOT/TestPlugin/LUSharp-TestPlugin.rbxmx" "$PLUGINS_DIR/"
        echo "Installed to $PLUGINS_DIR"
    fi
else
    echo "Rojo not found — skipping plugin build"
fi

echo ""
echo "=== Done ==="
```

**Step 2: Make it executable and test**

```bash
chmod +x LUSharpRoslynModule/deploy.sh
bash LUSharpRoslynModule/deploy.sh
```

**Step 3: Commit**

```bash
git add LUSharpRoslynModule/deploy.sh
git commit -m "feat(roslyn-module): add deploy script for transpile+copy+build workflow"
```

---

## Layer 1 Definition of Done

Layer 1 is complete when:
- [ ] `dotnet run --project LUSharpRoslynModule -- transpile-all` succeeds for SyntaxKind.cs
- [ ] `LUSharpRoslynModule/out/SyntaxKind.lua` is valid Luau (no syntax errors)
- [ ] TestPlugin loads in Roblox Studio without errors
- [ ] SyntaxKind member count matches between C# and Luau
- [ ] All `Name=Value` pairs match between C# reference and Luau output
- [ ] Reverse lookup table works in Luau

## Next Layers (future plans)

After Layer 1 is validated:
- **Layer 2:** Decompile `CharacterInfo.cs` + `SlidingTextWindow.cs`, extend emitter for static methods and string operations, expand runtime with `System.Char` and `System.String`
- **Layer 3:** Decompile `Lexer.cs`, extend emitter for classes with instance state, switch statements, exception handling; expand runtime with collections and `StringBuilder`
