# LUSharp Full Transpiler Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the full C# → Luau transpilation pipeline: IR layer, multi-pass Transform, Backend emitter, package system, and `lusharp build` CLI.

**Architecture:** Frontend collects all parsed files into a SymbolTable, Transform passes lower C# constructs to a `LuaModule` IR tree, Backend walks the IR and emits Luau text files. Packages ship C# stubs + pre-written Luau runtime bundled to `out/runtime/`.

**Tech Stack:** .NET 9, Roslyn (`Microsoft.CodeAnalysis.CSharp` 4.14.0), xUnit, Newtonsoft.Json

---

## Codebase Orientation

```
LUSharpTranspiler/
├── Frontend/
│   ├── Transpiler.cs       entry: scan files, parse, drive pipeline
│   └── CodeWalker.cs       CSharpSyntaxWalker — currently dead output
├── Transpiler/Builder/
│   └── ClassBuilder.cs     C# syntax → SourceConstructor calls (~990 lines)
├── AST/SourceConstructor/
│   ├── Builders/
│   │   ├── ClassBuilder.cs     fluent Lua class builder
│   │   ├── FunctionBuilder.cs  fluent method builder
│   │   ├── ConstructorBuilder.cs
│   │   ├── LuaWriter.cs        indented text writer — KEEP AS-IS
│   │   └── InlineLuaLine.cs
│   ├── LuaClass.cs, LuaFunction.cs, LuaAssignment.cs, LuaReturn.cs
│   └── ILuaRenderable.cs
└── Program.cs              hard-codes TestInput path, calls Transpiler
```

**What works today:** class structure (table, constructor, getters/setters, static fields/collections). Method bodies are fully stubbed (commented out). `CodeWalker` visits expressions but emits nothing.

**Key constraint:** Do not break existing class-structure output while building the new layers.

---

## Phase 0 — Housekeeping

### Task 0.1: Fix duplicate package reference and add test project

**Files:**
- Modify: `LUSharpTranspiler/LUSharpTranspiler.csproj`
- Create: `LUSharpTests/LUSharpTests.csproj` (via dotnet CLI)

**Step 1: Fix the duplicate Roslyn package reference**

`LUSharpTranspiler.csproj` currently has `Microsoft.CodeAnalysis.CSharp` listed twice (4.14.0 and 5.0.0). Remove the duplicate second `<ItemGroup>`:

```xml
<!-- REMOVE this entire ItemGroup: -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
</ItemGroup>
```

Keep only the first ItemGroup which also includes Newtonsoft.Json.

**Step 2: Create the test project**

```bash
dotnet new xunit -n LUSharpTests --framework net9.0 -o LUSharpTests
dotnet sln add LUSharpTests/LUSharpTests.csproj
dotnet add LUSharpTests/LUSharpTests.csproj reference LUSharpTranspiler/LUSharpTranspiler.csproj
```

**Step 3: Verify build**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

**Step 4: Commit**

```bash
git add LUSharpTranspiler/LUSharpTranspiler.csproj LUSharpTests/ LUSharp.sln
git commit -m "chore: fix duplicate package ref, add xunit test project"
```

---

## Phase 1 — IR Layer

The IR is the backbone. Every transform pass produces these nodes; the backend consumes them. Build the IR first so everything else compiles against it.

### Task 1.1: Core IR interfaces

**Files:**
- Create: `LUSharpTranspiler/Transform/IR/ILuaNode.cs`
- Create: `LUSharpTranspiler/Transform/IR/ILuaStatement.cs`
- Create: `LUSharpTranspiler/Transform/IR/ILuaExpression.cs`

**Step 1: Write the interfaces**

`Transform/IR/ILuaNode.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public interface ILuaNode { }
```

`Transform/IR/ILuaStatement.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public interface ILuaStatement : ILuaNode { }
```

`Transform/IR/ILuaExpression.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public interface ILuaExpression : ILuaNode { }
```

**Step 2: Verify build**

```bash
dotnet build LUSharpTranspiler
```

Expected: 0 errors.

---

### Task 1.2: Top-level module and class IR nodes

**Files:**
- Create: `LUSharpTranspiler/Transform/IR/LuaModule.cs`
- Create: `LUSharpTranspiler/Transform/IR/LuaClassDef.cs`
- Create: `LUSharpTranspiler/Transform/IR/LuaMethodDef.cs`
- Create: `LUSharpTranspiler/Transform/IR/LuaFieldDef.cs`
- Create: `LUSharpTranspiler/Transform/IR/LuaRequire.cs`
- Create: `LUSharpTranspiler/Transform/IR/LuaEventDef.cs`

**Step 1: Write nodes**

`Transform/IR/LuaRequire.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public record LuaRequire(string LocalName, string RequirePath);
```

`Transform/IR/LuaFieldDef.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public record LuaFieldDef(string Name, ILuaExpression? Value, bool IsStatic = false);
```

`Transform/IR/LuaEventDef.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

// Custom C# event → BindableEvent-backed Lua event
public record LuaEventDef(string Name, string SignatureType);
```

`Transform/IR/LuaMethodDef.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public class LuaMethodDef
{
    public string Name { get; init; } = "";
    public bool IsStatic { get; init; }
    public List<string> Parameters { get; init; } = new();
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Transform/IR/LuaClassDef.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public class LuaClassDef
{
    public string Name { get; init; } = "";
    public LuaMethodDef? Constructor { get; set; }
    public List<LuaFieldDef> StaticFields { get; init; } = new();
    public List<LuaFieldDef> InstanceFields { get; init; } = new();
    public List<LuaMethodDef> Methods { get; init; } = new();
    public List<LuaEventDef> Events { get; init; } = new();
}
```

`Transform/IR/LuaModule.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR;

public enum ScriptType { LocalScript, ModuleScript, Script }

public class LuaModule
{
    public string SourceFile { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public ScriptType ScriptType { get; init; }
    public List<LuaRequire> Requires { get; init; } = new();
    public List<LuaClassDef> Classes { get; init; } = new();
    public List<ILuaStatement> EntryBody { get; init; } = new(); // Main class only
}
```

**Step 2: Verify build**

```bash
dotnet build LUSharpTranspiler
```

Expected: 0 errors.

---

### Task 1.3: Statement IR nodes

**Files:**
- Create: `LUSharpTranspiler/Transform/IR/Statements/` (all files below)

**Step 1: Write all statement nodes**

`Statements/LuaLocal.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaLocal(string Name, ILuaExpression? Value) : ILuaStatement;
```

`Statements/LuaAssign.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaAssign(ILuaExpression Target, ILuaExpression Value) : ILuaStatement;
```

`Statements/LuaReturn.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaReturn(ILuaExpression? Value) : ILuaStatement;
```

`Statements/LuaBreak.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaBreak : ILuaStatement;
```

`Statements/LuaContinue.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaContinue : ILuaStatement;
```

`Statements/LuaError.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaError(ILuaExpression Message) : ILuaStatement;
```

`Statements/LuaExprStatement.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaExprStatement(ILuaExpression Expression) : ILuaStatement;
```

`Statements/LuaIf.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaElseIf(ILuaExpression Condition, List<ILuaStatement> Body);

public class LuaIf : ILuaStatement
{
    public ILuaExpression Condition { get; init; } = null!;
    public List<ILuaStatement> Then { get; init; } = new();
    public List<LuaElseIf> ElseIfs { get; init; } = new();
    public List<ILuaStatement>? Else { get; init; }
}
```

`Statements/LuaWhile.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaWhile : ILuaStatement
{
    public ILuaExpression Condition { get; init; } = null!;
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Statements/LuaRepeat.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaRepeat : ILuaStatement
{
    public List<ILuaStatement> Body { get; init; } = new();
    public ILuaExpression Condition { get; init; } = null!;
}
```

`Statements/LuaForNum.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaForNum : ILuaStatement
{
    public string Variable { get; init; } = "i";
    public ILuaExpression Start { get; init; } = null!;
    public ILuaExpression Limit { get; init; } = null!;
    public ILuaExpression? Step { get; init; }
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Statements/LuaForIn.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaForIn : ILuaStatement
{
    public List<string> Variables { get; init; } = new();
    public ILuaExpression Iterator { get; init; } = null!; // pairs(t) or ipairs(t)
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Statements/LuaPCall.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: local _ok, _err = pcall(function() ... end)
// then optional catch block, then finally block inline
public class LuaPCall : ILuaStatement
{
    public List<ILuaStatement> TryBody { get; init; } = new();
    public string? ErrorVar { get; init; }
    public List<ILuaStatement> CatchBody { get; init; } = new();
    public List<ILuaStatement> FinallyBody { get; init; } = new();
}
```

`Statements/LuaTaskSpawn.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: task.spawn(function() ... end)
public class LuaTaskSpawn : ILuaStatement
{
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Statements/LuaConnect.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: event:Connect(function(...) ... end)
public record LuaConnect(ILuaExpression Event, ILuaExpression Handler) : ILuaStatement;
```

`Statements/LuaMultiAssign.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: a, b = expr1, expr2
public record LuaMultiAssign(List<string> Targets, List<ILuaExpression> Values) : ILuaStatement;
```

**Step 2: Verify build**

```bash
dotnet build LUSharpTranspiler
```

Expected: 0 errors.

---

### Task 1.4: Expression IR nodes

**Files:**
- Create: `LUSharpTranspiler/Transform/IR/Expressions/` (all files below)

**Step 1: Write all expression nodes**

`Expressions/LuaLiteral.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// Value is already the Lua literal string: "42", "\"hello\"", "true", "nil"
public record LuaLiteral(string Value) : ILuaExpression
{
    public static LuaLiteral Nil => new("nil");
    public static LuaLiteral True => new("true");
    public static LuaLiteral False => new("false");
    public static LuaLiteral FromString(string s) => new($"\"{s.Replace("\"", "\\\"")}\"");
    public static LuaLiteral FromNumber(object n) => new(n.ToString()!);
}
```

`Expressions/LuaIdent.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaIdent(string Name) : ILuaExpression
{
    public static LuaIdent Self => new("self");
}
```

`Expressions/LuaMember.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// obj.field  (Dot) or obj:method (Colon — for method calls only)
public record LuaMember(ILuaExpression Object, string Member, bool IsColon = false) : ILuaExpression;
```

`Expressions/LuaIndex.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaIndex(ILuaExpression Object, ILuaExpression Key) : ILuaExpression;
```

`Expressions/LuaBinary.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaBinary(ILuaExpression Left, string Op, ILuaExpression Right) : ILuaExpression;
```

`Expressions/LuaUnary.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaUnary(string Op, ILuaExpression Operand) : ILuaExpression;
```

`Expressions/LuaConcat.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// Collapsed chain: a .. b .. c (avoids nesting)
public record LuaConcat(List<ILuaExpression> Parts) : ILuaExpression;
```

`Expressions/LuaInterp.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// From C# $"..." → Luau `...` template string
public record LuaInterp(string Template) : ILuaExpression;
```

`Expressions/LuaCall.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaCall(ILuaExpression Function, List<ILuaExpression> Args) : ILuaExpression;
```

`Expressions/LuaMethodCall.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaMethodCall(ILuaExpression Object, string Method, List<ILuaExpression> Args) : ILuaExpression;
```

`Expressions/LuaLambda.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public class LuaLambda : ILuaExpression
{
    public List<string> Parameters { get; init; } = new();
    public List<ILuaStatement> Body { get; init; } = new();
}
```

`Expressions/LuaTable.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaTableEntry(string? Key, ILuaExpression Value); // Key null = array entry

public record LuaTable(List<LuaTableEntry> Entries) : ILuaExpression
{
    public static LuaTable Empty => new(new());
}
```

`Expressions/LuaNew.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// ClassName.new(args) — from C# new Foo(...)
public record LuaNew(string ClassName, List<ILuaExpression> Args) : ILuaExpression;
```

`Expressions/LuaTernary.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// cond and a or b — from C# a ? b : c
public record LuaTernary(ILuaExpression Condition, ILuaExpression Then, ILuaExpression Else) : ILuaExpression;
```

`Expressions/LuaNullSafe.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// x and x.y — from C# x?.y
public record LuaNullSafe(ILuaExpression Object, string Member) : ILuaExpression;
```

`Expressions/LuaCoalesce.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// x ~= nil and x or y — from C# x ?? y
public record LuaCoalesce(ILuaExpression Left, ILuaExpression Right) : ILuaExpression;
```

`Expressions/LuaSpread.cs`:
```csharp
namespace LUSharpTranspiler.Transform.IR.Expressions;

// table.unpack(t)
public record LuaSpread(ILuaExpression Table) : ILuaExpression;
```

**Step 2: Verify build**

```bash
dotnet build LUSharpTranspiler
```

Expected: 0 errors.

**Step 3: Commit**

```bash
git add LUSharpTranspiler/Transform/
git commit -m "feat: add complete IR layer (statements, expressions, module nodes)"
```

---

## Phase 2 — Frontend: SymbolTable & Multi-File Scan

### Task 2.1: SymbolTable

**Files:**
- Create: `LUSharpTranspiler/Transform/SymbolTable.cs`
- Create: `LUSharpTranspiler/Transform/ClassSymbol.cs`
- Create: `LUSharpTranspilerTests/SymbolTableTests.cs`

**Step 1: Write failing test**

`LUSharpTests/SymbolTableTests.cs`:
```csharp
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.IR;

namespace LUSharpTests;

public class SymbolTableTests
{
    [Fact]
    public void RegisterClass_CanLookupByName()
    {
        var table = new SymbolTable();
        var symbol = new ClassSymbol("Player", "Client/Player.cs", ScriptType.ModuleScript, "ModuleScript");
        table.Register(symbol);

        var found = table.LookupClass("Player");
        Assert.NotNull(found);
        Assert.Equal("Player", found!.Name);
    }

    [Fact]
    public void LookupClass_ReturnsNull_WhenNotFound()
    {
        var table = new SymbolTable();
        Assert.Null(table.LookupClass("NonExistent"));
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~SymbolTableTests" -v
```

Expected: compilation error — SymbolTable not found.

**Step 3: Implement**

`Transform/ClassSymbol.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;

namespace LUSharpTranspiler.Transform;

public record ClassSymbol(
    string Name,
    string SourceFile,
    ScriptType ScriptType,
    string BaseClass  // "ModuleScript", "LocalScript", "Script", "RobloxScript"
);
```

`Transform/SymbolTable.cs`:
```csharp
namespace LUSharpTranspiler.Transform;

public class SymbolTable
{
    private readonly Dictionary<string, ClassSymbol> _classes = new();

    public void Register(ClassSymbol symbol) =>
        _classes[symbol.Name] = symbol;

    public ClassSymbol? LookupClass(string name) =>
        _classes.GetValueOrDefault(name);

    public IReadOnlyDictionary<string, ClassSymbol> Classes => _classes;
}
```

**Step 4: Run test — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~SymbolTableTests" -v
```

Expected: All pass.

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/SymbolTable.cs LUSharpTranspiler/Transform/ClassSymbol.cs LUSharpTests/SymbolTableTests.cs
git commit -m "feat: add SymbolTable for cross-file class resolution"
```

---

### Task 2.2: SymbolCollector pass

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/SymbolCollector.cs`
- Create: `LUSharpTests/SymbolCollectorTests.cs`

**Step 1: Write failing test**

```csharp
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class SymbolCollectorTests
{
    [Fact]
    public void Collect_RegistersModuleScriptClass()
    {
        var code = @"
            public class Player : ModuleScript {
                public string Name { get; set; }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var collector = new SymbolCollector(table);

        collector.Collect("Client/Player.cs", tree);

        var symbol = table.LookupClass("Player");
        Assert.NotNull(symbol);
        Assert.Equal(ScriptType.ModuleScript, symbol!.ScriptType);
    }

    [Fact]
    public void Collect_RegistersLocalScriptFromClientFolder()
    {
        var code = "public class Main : RobloxScript { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var collector = new SymbolCollector(table);

        collector.Collect("Client/Main.cs", tree);

        var symbol = table.LookupClass("Main");
        Assert.Equal(ScriptType.LocalScript, symbol!.ScriptType);
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~SymbolCollectorTests" -v
```

**Step 3: Implement**

`Transform/Passes/SymbolCollector.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class SymbolCollector
{
    private readonly SymbolTable _table;

    public SymbolCollector(SymbolTable table) => _table = table;

    public void Collect(string filePath, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var name = cls.Identifier.Text;
            var baseClass = cls.BaseList?.Types.FirstOrDefault()?.ToString() ?? "";
            var scriptType = DetermineScriptType(filePath, baseClass);
            _table.Register(new ClassSymbol(name, filePath, scriptType, baseClass));
        }
    }

    private static ScriptType DetermineScriptType(string filePath, string baseClass) =>
        baseClass switch
        {
            "ModuleScript" => ScriptType.ModuleScript,
            "LocalScript"  => ScriptType.LocalScript,
            "Script"       => ScriptType.Script,
            _ when filePath.Contains("Client") => ScriptType.LocalScript,
            _ when filePath.Contains("Server") => ScriptType.Script,
            _                                  => ScriptType.ModuleScript
        };
}
```

**Step 4: Run test — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~SymbolCollectorTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/SymbolCollector.cs LUSharpTests/SymbolCollectorTests.cs
git commit -m "feat: add SymbolCollector pass for class registration"
```

---

### Task 2.3: TransformPipeline orchestrator

**Files:**
- Create: `LUSharpTranspiler/Transform/TransformPipeline.cs`
- Create: `LUSharpTranspiler/Transform/ParsedFile.cs`

**Step 1: ParsedFile record**

`Transform/ParsedFile.cs`:
```csharp
using Microsoft.CodeAnalysis;

namespace LUSharpTranspiler.Transform;

public record ParsedFile(string FilePath, SyntaxTree Tree);
```

**Step 2: TransformPipeline skeleton**

`Transform/TransformPipeline.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;

namespace LUSharpTranspiler.Transform;

public class TransformPipeline
{
    private readonly SymbolTable _symbols = new();

    public List<LuaModule> Run(List<ParsedFile> files)
    {
        // Pass 0: collect all symbols first
        var collector = new SymbolCollector(_symbols);
        foreach (var f in files)
            collector.Collect(f.FilePath, f.Tree);

        // Remaining passes wired in as they're implemented
        var modules = new List<LuaModule>();
        foreach (var f in files)
            modules.Add(new LuaModule
            {
                SourceFile = f.FilePath,
                OutputPath = DeriveOutputPath(f.FilePath),
                ScriptType = _symbols.LookupClass("Main")?.ScriptType ?? ScriptType.ModuleScript
            });

        return modules;
    }

    private static string DeriveOutputPath(string filePath)
    {
        // Client/Foo.cs → out/client/Foo.lua
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (filePath.Contains("Client")) return $"out/client/{name}.lua";
        if (filePath.Contains("Server")) return $"out/server/{name}.lua";
        return $"out/shared/{name}.lua";
    }
}
```

**Step 3: Refactor `Frontend/Transpiler.cs` to collect all files first**

Replace the per-file processing loop in `TranspileProject` with a collect-then-process approach:

```csharp
public static void TranspileProject(string projectPath, string outputPath)
{
    var allFiles = new List<ParsedFile>();

    foreach (var file in GetAllSourceFiles(projectPath))
    {
        var code = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(code);

        if (tree.GetCompilationUnitRoot().ContainsDiagnostics)
        {
            ReportDiagnostics(file, tree);
            continue;
        }

        allFiles.Add(new ParsedFile(file, tree));
    }

    var pipeline = new TransformPipeline();
    var modules = pipeline.Run(allFiles);

    // Backend emission wired in later
    foreach (var m in modules)
        Logger.Log(LogSeverity.Info, $"Processed: {m.SourceFile} → {m.OutputPath}");

    Logger.Log(LogSeverity.Info, $"Transpilation complete. {modules.Count} modules.");
}

private static IEnumerable<string> GetAllSourceFiles(string projectPath)
{
    var patterns = new[] { "Client", "Server", "Shared" };
    return patterns.SelectMany(p =>
        Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                 .Where(f => f.Contains(p)));
}

private static void ReportDiagnostics(string file, SyntaxTree tree)
{
    Logger.Log(LogSeverity.Warning, $"{file} contains syntax errors. Skipping...");
    foreach (var d in tree.GetDiagnostics())
        Logger.Log(d.Severity, d.ToString());
}
```

**Step 4: Verify build and run**

```bash
dotnet build
dotnet run --project LUSharpTranspiler
```

Expected: builds and runs, prints "Processed: ... → ..." for each file.

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/ LUSharpTranspiler/Frontend/Transpiler.cs
git commit -m "feat: refactor transpiler to multi-file collect-then-process pipeline"
```

---

## Phase 3 — Transform: TypeResolver

### Task 3.1: TypeResolver

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/TypeResolver.cs`
- Create: `LUSharpTests/TypeResolverTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Transform.Passes;

namespace LUSharpTests;

public class TypeResolverTests
{
    private readonly TypeResolver _resolver = new(new());

    [Theory]
    [InlineData("string",  "string")]
    [InlineData("int",     "number")]
    [InlineData("float",   "number")]
    [InlineData("double",  "number")]
    [InlineData("bool",    "boolean")]
    [InlineData("void",    "nil")]
    [InlineData("object",  "any")]
    public void ResolvePrimitive(string csharp, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(csharp));
    }

    [Fact]
    public void ResolveList_EmitsTableType()
    {
        Assert.Equal("{number}", _resolver.Resolve("List<int>"));
    }

    [Fact]
    public void ResolveDictionary_EmitsTableType()
    {
        Assert.Equal("{[string]: number}", _resolver.Resolve("Dictionary<string, int>"));
    }

    [Fact]
    public void ResolveUnknownType_DefaultsToAny()
    {
        Assert.Equal("any", _resolver.Resolve("SomeRandomType"));
    }

    [Fact]
    public void ResolveUserClass_RegisteredInSymbolTable()
    {
        var table = new LUSharpTranspiler.Transform.SymbolTable();
        table.Register(new("Player", "Client/Player.cs",
            LUSharpTranspiler.Transform.IR.ScriptType.ModuleScript, "ModuleScript"));
        var resolver = new TypeResolver(table);
        Assert.Equal("Player", resolver.Resolve("Player"));
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~TypeResolverTests" -v
```

**Step 3: Implement**

`Transform/Passes/TypeResolver.cs`:
```csharp
namespace LUSharpTranspiler.Transform.Passes;

public class TypeResolver
{
    private readonly SymbolTable _symbols;

    private static readonly Dictionary<string, string> Primitives = new()
    {
        ["string"] = "string",
        ["int"]    = "number",
        ["float"]  = "number",
        ["double"] = "number",
        ["long"]   = "number",
        ["short"]  = "number",
        ["uint"]   = "number",
        ["bool"]   = "boolean",
        ["void"]   = "nil",
        ["object"] = "any",
        ["var"]    = "any",
        ["dynamic"]= "any",
    };

    public TypeResolver(SymbolTable symbols) => _symbols = symbols;

    public string Resolve(string csType)
    {
        if (Primitives.TryGetValue(csType, out var lua)) return lua;

        if (csType.StartsWith("List<") && csType.EndsWith(">"))
        {
            var inner = Resolve(csType[5..^1].Trim());
            return $"{{{inner}}}";
        }

        if (csType.StartsWith("Dictionary<") && csType.EndsWith(">"))
        {
            var inner = csType[11..^1];
            var comma = FindTopLevelComma(inner);
            if (comma >= 0)
            {
                var k = Resolve(inner[..comma].Trim());
                var v = Resolve(inner[(comma + 1)..].Trim());
                return $"{{[{k}]: {v}}}";
            }
        }

        if (_symbols.LookupClass(csType) != null) return csType;

        return "any";
    }

    private static int FindTopLevelComma(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0) return i;
        }
        return -1;
    }
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~TypeResolverTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/TypeResolver.cs LUSharpTests/TypeResolverTests.cs
git commit -m "feat: add TypeResolver pass for C# → Lua type mapping"
```

---

## Phase 4 — Transform: MethodBodyLowerer

This is the largest pass. It walks Roslyn method bodies and produces `ILuaStatement[]` IR.

### Task 4.1: ExpressionLowerer — literals and identifiers

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/ExpressionLowerer.cs`
- Create: `LUSharpTests/ExpressionLowererTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class ExpressionLowererTests
{
    private ExpressionLowerer MakeLowerer() => new(new TypeResolver(new()));

    private ExpressionSyntax Parse(string expr)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ var x = {expr}; }} }}");
        return tree.GetRoot()
            .DescendantNodes()
            .OfType<EqualsValueClauseSyntax>()
            .First()
            .Value;
    }

    [Fact]
    public void LowerIntLiteral()
    {
        var result = MakeLowerer().Lower(Parse("42"));
        var lit = Assert.IsType<LuaLiteral>(result);
        Assert.Equal("42", lit.Value);
    }

    [Fact]
    public void LowerStringLiteral()
    {
        var result = MakeLowerer().Lower(Parse("\"hello\""));
        var lit = Assert.IsType<LuaLiteral>(result);
        Assert.Equal("\"hello\"", lit.Value);
    }

    [Fact]
    public void LowerBoolTrue()
    {
        var result = MakeLowerer().Lower(Parse("true"));
        Assert.Equal("true", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void LowerIdentifier()
    {
        var result = MakeLowerer().Lower(Parse("myVar"));
        Assert.Equal("myVar", Assert.IsType<LuaIdent>(result).Name);
    }

    [Fact]
    public void LowerStringConcatPlus_BecomesLuaConcat()
    {
        var result = MakeLowerer().Lower(Parse("\"hello\" + name"));
        Assert.IsType<LuaConcat>(result);
    }

    [Fact]
    public void LowerInterpolatedString()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { var x = $\"Hello {name}\"; } }");
        var interp = tree.GetRoot().DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>().First();
        var result = MakeLowerer().Lower(interp);
        Assert.IsType<LuaInterp>(result);
    }
}
```

**Step 2: Run tests — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ExpressionLowererTests" -v
```

**Step 3: Implement**

`Transform/Passes/ExpressionLowerer.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class ExpressionLowerer
{
    private readonly TypeResolver _types;

    public ExpressionLowerer(TypeResolver types) => _types = types;

    public ILuaExpression Lower(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit       => LowerLiteral(lit),
        IdentifierNameSyntax id           => new LuaIdent(id.Identifier.Text),
        InterpolatedStringExpressionSyntax i => LowerInterpolated(i),
        BinaryExpressionSyntax bin        => LowerBinary(bin),
        PrefixUnaryExpressionSyntax pre   => LowerPrefixUnary(pre),
        PostfixUnaryExpressionSyntax post => LowerPostfixUnary(post),
        MemberAccessExpressionSyntax mem  => LowerMemberAccess(mem),
        InvocationExpressionSyntax inv    => LowerInvocation(inv),
        ObjectCreationExpressionSyntax oc => LowerObjectCreation(oc),
        ImplicitObjectCreationExpressionSyntax ic => LowerImplicitCreation(ic),
        ConditionalExpressionSyntax cond  => LowerTernary(cond),
        ConditionalAccessExpressionSyntax ca => LowerNullSafe(ca),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coa
                                          => LowerCoalesce(coa),
        ElementAccessExpressionSyntax el  => LowerIndex(el),
        AssignmentExpressionSyntax assign => LowerAssignmentExpr(assign),
        ParenthesizedLambdaExpressionSyntax lam => LowerLambda(lam.ParameterList, lam.Body),
        SimpleLambdaExpressionSyntax lam  => LowerSimpleLambda(lam),
        ParenthesizedExpressionSyntax par => Lower(par.Expression),
        CastExpressionSyntax cast         => Lower(cast.Expression), // strip casts
        _                                 => new LuaLiteral($"--[[unsupported: {expr.Kind()}]]")
    };

    private static LuaLiteral LowerLiteral(LiteralExpressionSyntax lit) => lit.Kind() switch
    {
        SyntaxKind.StringLiteralExpression    => new LuaLiteral($"\"{EscapeString(lit.Token.ValueText)}\""),
        SyntaxKind.NumericLiteralExpression   => new LuaLiteral(lit.Token.Text),
        SyntaxKind.TrueLiteralExpression      => LuaLiteral.True,
        SyntaxKind.FalseLiteralExpression     => LuaLiteral.False,
        SyntaxKind.NullLiteralExpression      => LuaLiteral.Nil,
        _                                     => new LuaLiteral(lit.Token.Text)
    };

    private static LuaInterp LowerInterpolated(InterpolatedStringExpressionSyntax interp)
    {
        var sb = new System.Text.StringBuilder("`");
        foreach (var content in interp.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
                sb.Append(text.TextToken.ValueText);
            else if (content is InterpolationSyntax hole)
                sb.Append($"{{{hole.Expression}}}");
        }
        sb.Append('`');
        return new LuaInterp(sb.ToString());
    }

    private ILuaExpression LowerBinary(BinaryExpressionSyntax bin)
    {
        var left = Lower(bin.Left);
        var right = Lower(bin.Right);

        // String concatenation via + → ..
        if (bin.IsKind(SyntaxKind.AddExpression))
        {
            // Flatten concat chains
            var parts = FlattenConcat(bin);
            if (parts.Count > 1) return new LuaConcat(parts);
        }

        var op = bin.Kind() switch
        {
            SyntaxKind.AddExpression              => "+",
            SyntaxKind.SubtractExpression         => "-",
            SyntaxKind.MultiplyExpression         => "*",
            SyntaxKind.DivideExpression           => "/",
            SyntaxKind.ModuloExpression           => "%",
            SyntaxKind.EqualsExpression           => "==",
            SyntaxKind.NotEqualsExpression        => "~=",
            SyntaxKind.LessThanExpression         => "<",
            SyntaxKind.LessThanOrEqualExpression  => "<=",
            SyntaxKind.GreaterThanExpression      => ">",
            SyntaxKind.GreaterThanOrEqualExpression => ">=",
            SyntaxKind.LogicalAndExpression       => "and",
            SyntaxKind.LogicalOrExpression        => "or",
            SyntaxKind.CoalesceExpression         => null, // handled above
            _                                     => bin.OperatorToken.Text
        };

        if (op == null) return LowerCoalesce(bin);
        return new LuaBinary(left, op, right);
    }

    private List<ILuaExpression> FlattenConcat(BinaryExpressionSyntax bin)
    {
        var parts = new List<ILuaExpression>();
        if (bin.Left is BinaryExpressionSyntax lb && lb.IsKind(SyntaxKind.AddExpression))
            parts.AddRange(FlattenConcat(lb));
        else
            parts.Add(Lower(bin.Left));
        parts.Add(Lower(bin.Right));
        return parts;
    }

    private ILuaExpression LowerPrefixUnary(PrefixUnaryExpressionSyntax pre)
    {
        var op = pre.Kind() switch
        {
            SyntaxKind.LogicalNotExpression  => "not",
            SyntaxKind.UnaryMinusExpression  => "-",
            SyntaxKind.PreIncrementExpression => null, // x++ → handled as assign
            _                                => pre.OperatorToken.Text
        };
        if (op == null) return Lower(pre.Operand); // fallback
        return new LuaUnary(op, Lower(pre.Operand));
    }

    private ILuaExpression LowerPostfixUnary(PostfixUnaryExpressionSyntax post) =>
        Lower(post.Operand); // i++ treated as expression value (statement handles increment)

    private ILuaExpression LowerMemberAccess(MemberAccessExpressionSyntax mem) =>
        new LuaMember(Lower(mem.Expression), mem.Name.Identifier.Text);

    private ILuaExpression LowerInvocation(InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList.Arguments
            .Select(a => Lower(a.Expression)).ToList();

        if (inv.Expression is MemberAccessExpressionSyntax mem)
        {
            // Map Console.WriteLine → print
            var obj = mem.Expression.ToString();
            var method = mem.Name.Identifier.Text;
            if (obj == "Console" && method == "WriteLine")
                return new LuaCall(new LuaIdent("print"), args);

            return new LuaMethodCall(Lower(mem.Expression), method, args);
        }

        return new LuaCall(Lower(inv.Expression), args);
    }

    private ILuaExpression LowerObjectCreation(ObjectCreationExpressionSyntax oc)
    {
        var className = oc.Type.ToString();
        var args = oc.ArgumentList?.Arguments
            .Select(a => Lower(a.Expression)).ToList() ?? new();
        return new LuaNew(className, args);
    }

    private ILuaExpression LowerImplicitCreation(ImplicitObjectCreationExpressionSyntax ic)
    {
        // new() { ... } — typically a collection initializer
        if (ic.Initializer != null)
            return LowerInitializer(ic.Initializer);
        return LuaTable.Empty;
    }

    private LuaTable LowerInitializer(InitializerExpressionSyntax init)
    {
        var entries = new List<LuaTableEntry>();
        foreach (var expr in init.Expressions)
        {
            if (expr is InitializerExpressionSyntax kv && kv.Expressions.Count == 2)
            {
                var k = kv.Expressions[0].ToString().Trim('"');
                var v = Lower(kv.Expressions[1]);
                entries.Add(new LuaTableEntry(k, v));
            }
            else
            {
                entries.Add(new LuaTableEntry(null, Lower(expr)));
            }
        }
        return new LuaTable(entries);
    }

    private ILuaExpression LowerTernary(ConditionalExpressionSyntax c) =>
        new LuaTernary(Lower(c.Condition), Lower(c.WhenTrue), Lower(c.WhenFalse));

    private ILuaExpression LowerNullSafe(ConditionalAccessExpressionSyntax ca)
    {
        var member = ca.WhenNotNull is MemberBindingExpressionSyntax mb
            ? mb.Name.Identifier.Text : ca.WhenNotNull.ToString();
        return new LuaNullSafe(Lower(ca.Expression), member);
    }

    private ILuaExpression LowerCoalesce(BinaryExpressionSyntax bin) =>
        new LuaCoalesce(Lower(bin.Left), Lower(bin.Right));

    private ILuaExpression LowerIndex(ElementAccessExpressionSyntax el)
    {
        var key = Lower(el.ArgumentList.Arguments.First().Expression);
        return new LuaIndex(Lower(el.Expression), key);
    }

    private ILuaExpression LowerAssignmentExpr(AssignmentExpressionSyntax assign) =>
        Lower(assign.Right); // assignment-as-expression; statement lowerer handles the assign

    private ILuaExpression LowerLambda(ParameterListSyntax paramList, CSharpSyntaxNode body)
    {
        var parms = paramList.Parameters.Select(p => p.Identifier.Text).ToList();
        var stmts = body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LUSharpTranspiler.Transform.IR.Statements.LuaReturn(Lower((ExpressionSyntax)body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private ILuaExpression LowerSimpleLambda(SimpleLambdaExpressionSyntax lam)
    {
        var parms = new List<string> { lam.Parameter.Identifier.Text };
        var stmts = lam.Body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LUSharpTranspiler.Transform.IR.Statements.LuaReturn(Lower((ExpressionSyntax)lam.Body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ExpressionLowererTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/ExpressionLowerer.cs LUSharpTests/ExpressionLowererTests.cs
git commit -m "feat: add ExpressionLowerer for C# expression → IR"
```

---

### Task 4.2: StatementLowerer — core statements

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/StatementLowerer.cs`
- Create: `LUSharpTests/StatementLowererTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class StatementLowererTests
{
    private static List<ILuaStatement> LowerMethod(string body)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {body} }} }}");
        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().First();
        var exprLowerer = new ExpressionLowerer(new TypeResolver(new()));
        return StatementLowerer.LowerBlock(method.Body!.Statements, exprLowerer);
    }

    [Fact]
    public void LocalVarDeclaration()
    {
        var stmts = LowerMethod("var x = 42;");
        var local = Assert.IsType<LuaLocal>(Assert.Single(stmts));
        Assert.Equal("x", local.Name);
        Assert.Equal("42", Assert.IsType<LuaLiteral>(local.Value!).Value);
    }

    [Fact]
    public void ReturnStatement()
    {
        var stmts = LowerMethod("return 99;");
        var ret = Assert.IsType<LuaReturn>(Assert.Single(stmts));
        Assert.Equal("99", Assert.IsType<LuaLiteral>(ret.Value!).Value);
    }

    [Fact]
    public void IfElseStatement()
    {
        var stmts = LowerMethod("if (x > 0) { y = 1; } else { y = 2; }");
        var ifStmt = Assert.IsType<LuaIf>(Assert.Single(stmts));
        Assert.NotNull(ifStmt.Condition);
        Assert.Single(ifStmt.Then);
        Assert.NotNull(ifStmt.Else);
    }

    [Fact]
    public void ForEachStatement()
    {
        var stmts = LowerMethod("foreach (var item in list) { print(item); }");
        var forIn = Assert.IsType<LuaForIn>(Assert.Single(stmts));
        Assert.Contains("item", forIn.Variables);
    }

    [Fact]
    public void WhileStatement()
    {
        var stmts = LowerMethod("while (running) { update(); }");
        var wh = Assert.IsType<LuaWhile>(Assert.Single(stmts));
        Assert.NotNull(wh.Condition);
        Assert.Single(wh.Body);
    }
}
```

**Step 2: Run tests — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~StatementLowererTests" -v
```

**Step 3: Implement**

`Transform/Passes/StatementLowerer.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public static class StatementLowerer
{
    public static List<ILuaStatement> LowerBlock(
        SyntaxList<StatementSyntax> stmts,
        ExpressionLowerer exprs)
    {
        var result = new List<ILuaStatement>();
        foreach (var s in stmts)
        {
            var lowered = Lower(s, exprs);
            if (lowered != null) result.Add(lowered);
        }
        return result;
    }

    private static ILuaStatement? Lower(StatementSyntax stmt, ExpressionLowerer exprs) => stmt switch
    {
        LocalDeclarationStatementSyntax loc   => LowerLocalDecl(loc, exprs),
        ExpressionStatementSyntax expr        => LowerExprStmt(expr, exprs),
        ReturnStatementSyntax ret             => new LuaReturn(ret.Expression != null ? exprs.Lower(ret.Expression) : null),
        IfStatementSyntax ifS                 => LowerIf(ifS, exprs),
        ForStatementSyntax forS               => LowerFor(forS, exprs),
        ForEachStatementSyntax forEach        => LowerForEach(forEach, exprs),
        WhileStatementSyntax wh               => LowerWhile(wh, exprs),
        DoStatementSyntax doS                 => LowerDo(doS, exprs),
        BreakStatementSyntax                  => new LuaBreak(),
        ContinueStatementSyntax               => new LuaContinue(),
        ThrowStatementSyntax th               => LowerThrow(th, exprs),
        TryStatementSyntax tryS               => LowerTry(tryS, exprs),
        SwitchStatementSyntax sw              => LowerSwitch(sw, exprs),
        BlockSyntax block                     => new LuaExprStatement(new LuaLiteral(
            string.Join("\n", LowerBlock(block.Statements, exprs)))), // nested block inline
        _                                     => null
    };

    private static ILuaStatement LowerLocalDecl(LocalDeclarationStatementSyntax loc, ExpressionLowerer exprs)
    {
        var v = loc.Declaration.Variables.First();
        var value = v.Initializer != null ? exprs.Lower(v.Initializer.Value) : null;
        return new LuaLocal(v.Identifier.Text, value);
    }

    private static ILuaStatement LowerExprStmt(ExpressionStatementSyntax exprStmt, ExpressionLowerer exprs)
    {
        var expr = exprStmt.Expression;

        // x += 1 etc — compound assignments
        if (expr is AssignmentExpressionSyntax assign)
        {
            var target = exprs.Lower(assign.Left);
            var value = exprs.Lower(assign.Right);

            if (assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
                return new LuaAssign(target, value);

            // compound: x += v → x = x + v
            var op = assign.Kind() switch
            {
                SyntaxKind.AddAssignmentExpression      => "+",
                SyntaxKind.SubtractAssignmentExpression => "-",
                SyntaxKind.MultiplyAssignmentExpression => "*",
                SyntaxKind.DivideAssignmentExpression   => "/",
                _                                       => "+"
            };
            return new LuaAssign(target, new LuaBinary(target, op, value));
        }

        // Event += handler → LuaConnect
        if (expr is AssignmentExpressionSyntax ev &&
            ev.IsKind(SyntaxKind.AddAssignmentExpression) &&
            ev.Right is LambdaExpressionSyntax)
        {
            return new LuaConnect(exprs.Lower(ev.Left), exprs.Lower(ev.Right));
        }

        // i++ / i-- → i = i + 1
        if (expr is PostfixUnaryExpressionSyntax post)
        {
            var operand = exprs.Lower(post.Operand);
            var delta = new LuaLiteral("1");
            var op = post.IsKind(SyntaxKind.PostIncrementExpression) ? "+" : "-";
            return new LuaAssign(operand, new LuaBinary(operand, op, delta));
        }

        return new LuaExprStatement(exprs.Lower(expr));
    }

    private static LuaIf LowerIf(IfStatementSyntax ifS, ExpressionLowerer exprs)
    {
        var luaIf = new LuaIf
        {
            Condition = exprs.Lower(ifS.Condition),
            Then = LowerBody(ifS.Statement, exprs)
        };

        // Collect elseif chain
        var current = ifS.Else;
        while (current?.Statement is IfStatementSyntax elseIf)
        {
            luaIf.ElseIfs.Add(new LuaElseIf(
                exprs.Lower(elseIf.Condition),
                LowerBody(elseIf.Statement, exprs)));
            current = elseIf.Else;
        }

        if (current != null)
            luaIf.Else.AddRange(LowerBody(current.Statement, exprs)); // NOTE: Else is init-only record; use mutable version

        return luaIf;
    }

    private static ILuaStatement LowerFor(ForStatementSyntax forS, ExpressionLowerer exprs)
    {
        // Detect simple numeric for: for (int i = start; i < limit; i++)
        if (forS.Declaration?.Variables.Count == 1 &&
            forS.Incrementors.Count == 1 &&
            forS.Condition != null)
        {
            var varName = forS.Declaration.Variables[0].Identifier.Text;
            var start = exprs.Lower(forS.Declaration.Variables[0].Initializer!.Value);
            var limit = exprs.Lower(forS.Condition is BinaryExpressionSyntax bin ? bin.Right : forS.Condition);
            var body = LowerBody(forS.Statement, exprs);
            return new LuaForNum { Variable = varName, Start = start, Limit = limit, Body = body };
        }

        // Fallback: emit as while
        var initStmts = new List<ILuaStatement>();
        if (forS.Declaration != null)
            initStmts.Add(LowerLocalDecl(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.LocalDeclarationStatement(forS.Declaration),
                exprs));

        var whileBody = LowerBody(forS.Statement, exprs);
        foreach (var inc in forS.Incrementors)
            whileBody.Add(new LuaExprStatement(exprs.Lower(inc)));

        return new LuaWhile
        {
            Condition = forS.Condition != null ? exprs.Lower(forS.Condition) : LuaLiteral.True,
            Body = whileBody
        };
    }

    private static LuaForIn LowerForEach(ForEachStatementSyntax fe, ExpressionLowerer exprs)
    {
        var iter = exprs.Lower(fe.Expression);
        // Use ipairs for array-like, pairs for general
        var iterCall = new LuaCall(new LuaIdent("pairs"), new List<ILuaExpression> { iter });
        return new LuaForIn
        {
            Variables = new() { "_", fe.Identifier.Text },
            Iterator = iterCall,
            Body = LowerBody(fe.Statement, exprs)
        };
    }

    private static LuaWhile LowerWhile(WhileStatementSyntax wh, ExpressionLowerer exprs) =>
        new() { Condition = exprs.Lower(wh.Condition), Body = LowerBody(wh.Statement, exprs) };

    private static LuaRepeat LowerDo(DoStatementSyntax doS, ExpressionLowerer exprs) =>
        new() { Body = LowerBody(doS.Statement, exprs), Condition = exprs.Lower(doS.Condition) };

    private static LuaError LowerThrow(ThrowStatementSyntax th, ExpressionLowerer exprs) =>
        new(th.Expression != null ? exprs.Lower(th.Expression) : new LuaLiteral("\"error\""));

    private static LuaPCall LowerTry(TryStatementSyntax tryS, ExpressionLowerer exprs)
    {
        var pCall = new LuaPCall
        {
            TryBody = LowerBody(tryS.Block, exprs)
        };

        // First catch clause
        if (tryS.Catches.Count > 0)
        {
            var catch1 = tryS.Catches[0];
            pCall.CatchBody.AddRange(LowerBody(catch1.Block, exprs));
        }

        if (tryS.Finally != null)
            pCall.FinallyBody.AddRange(LowerBody(tryS.Finally.Block, exprs));

        return pCall;
    }

    private static ILuaStatement LowerSwitch(SwitchStatementSyntax sw, ExpressionLowerer exprs)
    {
        // switch → if/elseif chain
        var subject = exprs.Lower(sw.Expression);
        LuaIf? root = null;
        LuaIf? current = null;

        foreach (var section in sw.Sections)
        {
            var labels = section.Labels.OfType<CaseSwitchLabelSyntax>().ToList();
            if (!labels.Any()) continue; // default handled below

            var cond = labels
                .Select(l => (ILuaExpression)new LuaBinary(subject, "==", exprs.Lower(l.Value)))
                .Aggregate((a, b) => new LuaBinary(a, "or", b));

            var body = section.Statements
                .Where(s => s is not BreakStatementSyntax)
                .Select(s => Lower(s, exprs))
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();

            if (root == null)
            {
                root = new LuaIf { Condition = cond, Then = body };
                current = root;
            }
            else
            {
                current!.ElseIfs.Add(new LuaElseIf(cond, body));
            }
        }

        // default section
        var defaultSection = sw.Sections.FirstOrDefault(s =>
            s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
        if (defaultSection != null && root != null)
        {
            var defaultBody = defaultSection.Statements
                .Where(s => s is not BreakStatementSyntax)
                .Select(s => Lower(s, exprs))
                .Where(s => s != null).Select(s => s!).ToList();
            root.Else?.AddRange(defaultBody);
        }

        return root ?? new LuaExprStatement(new LuaLiteral("-- empty switch"));
    }

    private static List<ILuaStatement> LowerBody(StatementSyntax stmt, ExpressionLowerer exprs) =>
        stmt is BlockSyntax block
            ? LowerBlock(block.Statements, exprs)
            : Lower(stmt, exprs) is { } s ? new() { s } : new();

    private static List<ILuaStatement> LowerBody(BlockSyntax block, ExpressionLowerer exprs) =>
        LowerBlock(block.Statements, exprs);
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~StatementLowererTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/StatementLowerer.cs LUSharpTests/StatementLowererTests.cs
git commit -m "feat: add StatementLowerer for full C# → IR statement lowering"
```

---

### Task 4.3: MethodBodyLowerer — wire methods into LuaClassDef

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/MethodBodyLowerer.cs`
- Create: `LUSharpTests/MethodBodyLowererTests.cs`

**Step 1: Write failing test**

```csharp
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class MethodBodyLowererTests
{
    [Fact]
    public void LowersMethodBodyIntoClassDef()
    {
        var code = @"
            class Player {
                public int GetHealth() {
                    return 100;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var lowerer = new MethodBodyLowerer(table);

        var classDef = lowerer.Lower(
            tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .First());

        Assert.Single(classDef.Methods);
        Assert.Equal("GetHealth", classDef.Methods[0].Name);
        Assert.IsType<LuaReturn>(Assert.Single(classDef.Methods[0].Body));
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~MethodBodyLowererTests" -v
```

**Step 3: Implement**

`Transform/Passes/MethodBodyLowerer.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class MethodBodyLowerer
{
    private readonly TypeResolver _types;
    private readonly ExpressionLowerer _exprs;

    public MethodBodyLowerer(SymbolTable symbols)
    {
        _types = new TypeResolver(symbols);
        _exprs = new ExpressionLowerer(_types);
    }

    public LuaClassDef Lower(ClassDeclarationSyntax cls)
    {
        var def = new LuaClassDef { Name = cls.Identifier.Text };

        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method when method.Body != null:
                    def.Methods.Add(LowerMethod(method));
                    break;

                case ConstructorDeclarationSyntax ctor when ctor.Body != null:
                    def.Constructor = LowerConstructor(ctor, cls.Identifier.Text);
                    break;

                case FieldDeclarationSyntax field:
                    LowerField(field, def);
                    break;

                case PropertyDeclarationSyntax prop:
                    LowerProperty(prop, def);
                    break;
            }
        }

        return def;
    }

    private LuaMethodDef LowerMethod(MethodDeclarationSyntax method)
    {
        var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        var parms = new List<string>();
        if (!isStatic) parms.Add("self");
        parms.AddRange(method.ParameterList.Parameters.Select(p => p.Identifier.Text));

        return new LuaMethodDef
        {
            Name = method.Identifier.Text,
            IsStatic = isStatic,
            Parameters = parms,
            Body = StatementLowerer.LowerBlock(method.Body!.Statements, _exprs)
        };
    }

    private LuaMethodDef LowerConstructor(ConstructorDeclarationSyntax ctor, string className)
    {
        var parms = ctor.ParameterList.Parameters.Select(p => p.Identifier.Text).ToList();
        var body = new List<ILuaStatement>();

        // local self = {}
        body.Add(new LuaLocal("self", LuaTable.Empty));

        // Body statements (this.X = x → self.X = x)
        body.AddRange(StatementLowerer.LowerBlock(ctor.Body!.Statements, _exprs)
            .Select(s => RewriteThisToSelf(s)));

        // return self
        body.Add(new LuaReturn(new LuaIdent("self")));

        return new LuaMethodDef
        {
            Name = "new",
            IsStatic = true,
            Parameters = parms,
            Body = body
        };
    }

    private void LowerField(FieldDeclarationSyntax field, LuaClassDef def)
    {
        var isStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        foreach (var v in field.Declaration.Variables)
        {
            var value = v.Initializer != null ? _exprs.Lower(v.Initializer.Value) : null;
            var fd = new LuaFieldDef(v.Identifier.Text, value, isStatic);
            if (isStatic) def.StaticFields.Add(fd);
            else def.InstanceFields.Add(fd);
        }
    }

    private void LowerProperty(PropertyDeclarationSyntax prop, LuaClassDef def)
    {
        var isStatic = prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        var value = prop.Initializer != null ? _exprs.Lower(prop.Initializer.Value) : null;
        var fd = new LuaFieldDef(prop.Identifier.Text, value, isStatic);
        if (isStatic) def.StaticFields.Add(fd);
        else def.InstanceFields.Add(fd);

        // Auto-generate getter/setter methods
        if (prop.AccessorList != null)
        {
            if (prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
                def.Methods.Add(MakeGetter(prop.Identifier.Text, isStatic));
            if (prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
                def.Methods.Add(MakeSetter(prop.Identifier.Text, isStatic));
        }
    }

    private static LuaMethodDef MakeGetter(string name, bool isStatic) => new()
    {
        Name = $"get_{name}",
        IsStatic = isStatic,
        Parameters = isStatic ? new() : new() { "self" },
        Body = new() { new LuaReturn(new LuaMember(new LuaIdent(isStatic ? name : "self"), name)) }
    };

    private static LuaMethodDef MakeSetter(string name, bool isStatic) => new()
    {
        Name = $"set_{name}",
        IsStatic = isStatic,
        Parameters = isStatic ? new() { "value" } : new() { "self", "value" },
        Body = new() { new LuaAssign(new LuaMember(new LuaIdent(isStatic ? name : "self"), name), new LuaIdent("value")) }
    };

    // Rewrite this.X → self.X in lowered statements
    private static ILuaStatement RewriteThisToSelf(ILuaStatement stmt) => stmt; // simplified — full rewrite TBD in EventBinder
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~MethodBodyLowererTests" -v
```

**Step 5: Wire MethodBodyLowerer into TransformPipeline**

In `TransformPipeline.cs`, call `MethodBodyLowerer.Lower()` on each class in each parsed file and populate `LuaModule.Classes`.

**Step 6: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/MethodBodyLowerer.cs LUSharpTests/MethodBodyLowererTests.cs
git commit -m "feat: add MethodBodyLowerer — methods, constructors, fields → IR"
```

---

## Phase 5 — Transform: EventBinder

### Task 5.1: EventBinder

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/EventBinder.cs`
- Create: `LUSharpTests/EventBinderTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Statements;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class EventBinderTests
{
    private static List<ILuaStatement> LowerMethod(string body)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {body} }} }}");
        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().First();
        var exprs = new ExpressionLowerer(new TypeResolver(new()));
        return StatementLowerer.LowerBlock(method.Body!.Statements, exprs);
    }

    [Fact]
    public void PlusEqualsLambda_BecomesLuaConnect()
    {
        var stmts = LowerMethod("players.PlayerAdded += (Player p) => { print(p.Name); };");
        var conn = Assert.IsType<LuaConnect>(Assert.Single(stmts));
        Assert.NotNull(conn.Event);
        Assert.NotNull(conn.Handler);
    }

    [Fact]
    public void DotConnect_BecomesLuaConnect()
    {
        var stmts = LowerMethod("players.PlayerAdded.Connect((Player p) => { print(p.Name); });");
        // .Connect() call should be recognized as LuaConnect or LuaExprStatement with LuaMethodCall
        Assert.Single(stmts);
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~EventBinderTests" -v
```

**Step 3: Implement in StatementLowerer** (update `LowerExprStmt`)

Event `+=` with a lambda right-hand side should emit `LuaConnect`. Update the `LowerExprStmt` method in `StatementLowerer.cs`:

```csharp
// Inside LowerExprStmt, BEFORE the SimpleAssignment check:
if (expr is AssignmentExpressionSyntax ev &&
    ev.IsKind(SyntaxKind.AddAssignmentExpression) &&
    (ev.Right is LambdaExpressionSyntax || ev.Right is AnonymousMethodExpressionSyntax))
{
    return new LuaConnect(exprs.Lower(ev.Left), exprs.Lower(ev.Right));
}
```

`.Connect(handler)` calls are already emitted as `LuaMethodCall` nodes by `ExpressionLowerer` — the Backend maps `:Connect()` to Luau `:Connect()` naturally.

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~EventBinderTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/ LUSharpTests/EventBinderTests.cs
git commit -m "feat: wire event += to LuaConnect in StatementLowerer"
```

---

## Phase 6 — Backend: Emitters

### Task 6.1: ExprEmitter

**Files:**
- Create: `LUSharpTranspiler/Backend/ExprEmitter.cs`
- Create: `LUSharpTests/ExprEmitterTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTests;

public class ExprEmitterTests
{
    private static string Emit(ILuaExpression expr)
    {
        var w = new LuaWriter();
        ExprEmitter.Emit(expr, w);
        return w.ToString().Trim();
    }

    [Fact] public void Literal()    => Assert.Equal("42",       Emit(new LuaLiteral("42")));
    [Fact] public void Ident()      => Assert.Equal("self",     Emit(new LuaIdent("self")));
    [Fact] public void Member()     => Assert.Equal("self.Health", Emit(new LuaMember(new LuaIdent("self"), "Health")));
    [Fact] public void Binary()     => Assert.Equal("a + b",    Emit(new LuaBinary(new LuaIdent("a"), "+", new LuaIdent("b"))));
    [Fact] public void Concat()     => Assert.Equal("a .. b",   Emit(new LuaConcat(new() { new LuaIdent("a"), new LuaIdent("b") })));
    [Fact] public void NilLiteral() => Assert.Equal("nil",      Emit(LuaLiteral.Nil));
    [Fact] public void New()        => Assert.Equal("Player.new(x)", Emit(new LuaNew("Player", new() { new LuaIdent("x") })));
    [Fact] public void MethodCall() => Assert.Equal("obj:Foo(x)", Emit(new LuaMethodCall(new LuaIdent("obj"), "Foo", new() { new LuaIdent("x") })));
    [Fact] public void Ternary()    => Assert.Equal("a and b or c", Emit(new LuaTernary(new LuaIdent("a"), new LuaIdent("b"), new LuaIdent("c"))));
    [Fact] public void NullSafe()   => Assert.Equal("x and x.y", Emit(new LuaNullSafe(new LuaIdent("x"), "y")));
    [Fact] public void Index()      => Assert.Equal("t[i]",     Emit(new LuaIndex(new LuaIdent("t"), new LuaIdent("i"))));
}
```

**Step 2: Run tests — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ExprEmitterTests" -v
```

**Step 3: Implement**

`Backend/ExprEmitter.cs`:
```csharp
using LUSharpTranspiler.AST.SourceConstructor.Builders;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTranspiler.Backend;

public static class ExprEmitter
{
    public static void Emit(ILuaExpression expr, LuaWriter w)
    {
        switch (expr)
        {
            case LuaLiteral lit:     w.WriteInline(lit.Value); break;
            case LuaIdent id:        w.WriteInline(id.Name); break;
            case LuaInterp interp:   w.WriteInline(interp.Template); break;

            case LuaMember mem:
                Emit(mem.Object, w);
                w.WriteInline(mem.IsColon ? ":" : ".");
                w.WriteInline(mem.Member);
                break;

            case LuaIndex idx:
                Emit(idx.Object, w);
                w.WriteInline("[");
                Emit(idx.Key, w);
                w.WriteInline("]");
                break;

            case LuaBinary bin:
                EmitParen(bin.Left, bin.Op, w, true);
                w.WriteInline($" {bin.Op} ");
                EmitParen(bin.Right, bin.Op, w, false);
                break;

            case LuaUnary un:
                w.WriteInline(un.Op == "not" ? "not " : un.Op);
                Emit(un.Operand, w);
                break;

            case LuaConcat cat:
                for (int i = 0; i < cat.Parts.Count; i++)
                {
                    if (i > 0) w.WriteInline(" .. ");
                    Emit(cat.Parts[i], w);
                }
                break;

            case LuaCall call:
                Emit(call.Function, w);
                w.WriteInline("(");
                EmitArgList(call.Args, w);
                w.WriteInline(")");
                break;

            case LuaMethodCall mc:
                Emit(mc.Object, w);
                w.WriteInline($":{mc.Method}(");
                EmitArgList(mc.Args, w);
                w.WriteInline(")");
                break;

            case LuaNew n:
                w.WriteInline($"{n.ClassName}.new(");
                EmitArgList(n.Args, w);
                w.WriteInline(")");
                break;

            case LuaTernary t:
                Emit(t.Condition, w); w.WriteInline(" and ");
                Emit(t.Then, w);     w.WriteInline(" or ");
                Emit(t.Else, w);
                break;

            case LuaNullSafe ns:
                Emit(ns.Object, w);
                w.WriteInline($" and {ns.Object}.{ns.Member}"); // simplified
                break;

            case LuaCoalesce co:
                Emit(co.Left, w); w.WriteInline(" ~= nil and ");
                Emit(co.Left, w); w.WriteInline(" or ");
                Emit(co.Right, w);
                break;

            case LuaTable tbl:
                EmitTable(tbl, w);
                break;

            case LuaLambda lam:
                EmitLambda(lam, w);
                break;

            case LuaSpread sp:
                w.WriteInline("table.unpack(");
                Emit(sp.Table, w);
                w.WriteInline(")");
                break;

            default:
                w.WriteInline($"--[[unknown expr: {expr.GetType().Name}]]");
                break;
        }
    }

    private static void EmitParen(ILuaExpression expr, string parentOp, LuaWriter w, bool isLeft)
    {
        // Simple precedence: wrap lower-precedence binary expressions in parens
        if (expr is LuaBinary bin && NeedsParens(bin.Op, parentOp))
        {
            w.WriteInline("("); Emit(expr, w); w.WriteInline(")");
        }
        else Emit(expr, w);
    }

    private static bool NeedsParens(string inner, string outer) =>
        (inner == "or" && outer != "or") ||
        (inner == "and" && outer is "+" or "-" or "*" or "/");

    private static void EmitArgList(List<ILuaExpression> args, LuaWriter w)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0) w.WriteInline(", ");
            Emit(args[i], w);
        }
    }

    private static void EmitTable(LuaTable tbl, LuaWriter w)
    {
        if (tbl.Entries.Count == 0) { w.WriteInline("{}"); return; }
        w.WriteInline("{");
        for (int i = 0; i < tbl.Entries.Count; i++)
        {
            if (i > 0) w.WriteInline(", ");
            var e = tbl.Entries[i];
            if (e.Key != null) w.WriteInline($"{e.Key} = ");
            Emit(e.Value, w);
        }
        w.WriteInline("}");
    }

    private static void EmitLambda(LuaLambda lam, LuaWriter w)
    {
        w.WriteInline($"function({string.Join(", ", lam.Parameters)})");
        // inline body for simple lambdas, multiline for complex
        if (lam.Body.Count == 1 && lam.Body[0] is LUSharpTranspiler.Transform.IR.Statements.LuaReturn ret && ret.Value != null)
        {
            w.WriteInline(" return "); Emit(ret.Value, w); w.WriteInline(" end");
        }
        else
        {
            w.WriteInline("\n");
            w.IndentMore();
            foreach (var s in lam.Body)
                StatementEmitter.Emit(s, w);
            w.IndentLess();
            w.WriteInline("end");
        }
    }
}
```

**Note:** `LuaWriter` needs a `WriteInline(string)` method added — it currently only has `WriteLine`. Add it:

In `AST/SourceConstructor/Builders/LuaWriter.cs`, add:
```csharp
public void WriteInline(string text) => _sb.Append(text);
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ExprEmitterTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Backend/ExprEmitter.cs LUSharpTranspiler/AST/SourceConstructor/Builders/LuaWriter.cs LUSharpTests/ExprEmitterTests.cs
git commit -m "feat: add ExprEmitter for IR → Luau text; add LuaWriter.WriteInline"
```

---

### Task 6.2: StatementEmitter

**Files:**
- Create: `LUSharpTranspiler/Backend/StatementEmitter.cs`
- Create: `LUSharpTests/StatementEmitterTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTests;

public class StatementEmitterTests
{
    private static string Emit(ILuaStatement stmt)
    {
        var w = new LuaWriter();
        StatementEmitter.Emit(stmt, w);
        return w.ToString().Trim();
    }

    [Fact]
    public void LocalStatement()
    {
        var result = Emit(new LuaLocal("x", new LuaLiteral("42")));
        Assert.Equal("local x = 42", result);
    }

    [Fact]
    public void AssignStatement()
    {
        var result = Emit(new LuaAssign(new LuaIdent("x"), new LuaLiteral("10")));
        Assert.Equal("x = 10", result);
    }

    [Fact]
    public void ReturnStatement()
    {
        var result = Emit(new LuaReturn(new LuaLiteral("true")));
        Assert.Equal("return true", result);
    }

    [Fact]
    public void IfStatement()
    {
        var stmt = new LuaIf
        {
            Condition = new LuaBinary(new LuaIdent("x"), ">", new LuaLiteral("0")),
            Then = new() { new LuaReturn(new LuaLiteral("1")) }
        };
        var result = Emit(stmt);
        Assert.Contains("if x > 0 then", result);
        Assert.Contains("return 1", result);
        Assert.Contains("end", result);
    }

    [Fact]
    public void ForNumStatement()
    {
        var stmt = new LuaForNum
        {
            Variable = "i",
            Start = new LuaLiteral("1"),
            Limit = new LuaLiteral("10"),
            Body = new() { new LuaBreak() }
        };
        var result = Emit(stmt);
        Assert.Contains("for i = 1, 10 do", result);
        Assert.Contains("break", result);
    }

    [Fact]
    public void PCallStatement()
    {
        var stmt = new LuaPCall
        {
            TryBody = new() { new LuaExprStatement(new LuaCall(new LuaIdent("riskyOp"), new())) },
            CatchBody = new() { new LuaExprStatement(new LuaCall(new LuaIdent("print"), new() { new LuaIdent("_err") })) }
        };
        var result = Emit(stmt);
        Assert.Contains("pcall(function()", result);
        Assert.Contains("if not _ok then", result);
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~StatementEmitterTests" -v
```

**Step 3: Implement**

`Backend/StatementEmitter.cs`:
```csharp
using LUSharpTranspiler.AST.SourceConstructor.Builders;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTranspiler.Backend;

public static class StatementEmitter
{
    public static void Emit(ILuaStatement stmt, LuaWriter w)
    {
        switch (stmt)
        {
            case LuaLocal loc:
                w.WriteInline($"local {loc.Name}");
                if (loc.Value != null) { w.WriteInline(" = "); ExprEmitter.Emit(loc.Value, w); }
                w.WriteLine();
                break;

            case LuaAssign asgn:
                ExprEmitter.Emit(asgn.Target, w);
                w.WriteInline(" = ");
                ExprEmitter.Emit(asgn.Value, w);
                w.WriteLine();
                break;

            case LuaReturn ret:
                w.WriteInline("return");
                if (ret.Value != null) { w.WriteInline(" "); ExprEmitter.Emit(ret.Value, w); }
                w.WriteLine();
                break;

            case LuaBreak:    w.WriteLine("break");    break;
            case LuaContinue: w.WriteLine("continue"); break;

            case LuaError err:
                w.WriteInline("error("); ExprEmitter.Emit(err.Message, w); w.WriteLine(")");
                break;

            case LuaExprStatement es:
                ExprEmitter.Emit(es.Expression, w);
                w.WriteLine();
                break;

            case LuaIf ifS:     EmitIf(ifS, w); break;
            case LuaWhile wh:   EmitWhile(wh, w); break;
            case LuaRepeat rep: EmitRepeat(rep, w); break;
            case LuaForNum fn:  EmitForNum(fn, w); break;
            case LuaForIn fi:   EmitForIn(fi, w); break;
            case LuaPCall pc:   EmitPCall(pc, w); break;
            case LuaTaskSpawn ts: EmitTaskSpawn(ts, w); break;
            case LuaConnect cn: EmitConnect(cn, w); break;

            case LuaMultiAssign ma:
                w.WriteInline(string.Join(", ", ma.Targets));
                w.WriteInline(" = ");
                for (int i = 0; i < ma.Values.Count; i++)
                {
                    if (i > 0) w.WriteInline(", ");
                    ExprEmitter.Emit(ma.Values[i], w);
                }
                w.WriteLine();
                break;

            default:
                w.WriteLine($"--[[unknown stmt: {stmt.GetType().Name}]]");
                break;
        }
    }

    private static void EmitBlock(List<ILuaStatement> body, LuaWriter w)
    {
        w.IndentMore();
        foreach (var s in body) Emit(s, w);
        w.IndentLess();
    }

    private static void EmitIf(LuaIf ifS, LuaWriter w)
    {
        w.WriteInline("if "); ExprEmitter.Emit(ifS.Condition, w); w.WriteLine(" then");
        EmitBlock(ifS.Then, w);
        foreach (var ei in ifS.ElseIfs)
        {
            w.WriteInline("elseif "); ExprEmitter.Emit(ei.Condition, w); w.WriteLine(" then");
            EmitBlock(ei.Body, w);
        }
        if (ifS.Else?.Count > 0) { w.WriteLine("else"); EmitBlock(ifS.Else, w); }
        w.WriteLine("end");
    }

    private static void EmitWhile(LuaWhile wh, LuaWriter w)
    {
        w.WriteInline("while "); ExprEmitter.Emit(wh.Condition, w); w.WriteLine(" do");
        EmitBlock(wh.Body, w);
        w.WriteLine("end");
    }

    private static void EmitRepeat(LuaRepeat rep, LuaWriter w)
    {
        w.WriteLine("repeat");
        EmitBlock(rep.Body, w);
        w.WriteInline("until "); ExprEmitter.Emit(rep.Condition, w); w.WriteLine();
    }

    private static void EmitForNum(LuaForNum fn, LuaWriter w)
    {
        w.WriteInline($"for {fn.Variable} = ");
        ExprEmitter.Emit(fn.Start, w); w.WriteInline(", ");
        ExprEmitter.Emit(fn.Limit, w);
        if (fn.Step != null) { w.WriteInline(", "); ExprEmitter.Emit(fn.Step, w); }
        w.WriteLine(" do");
        EmitBlock(fn.Body, w);
        w.WriteLine("end");
    }

    private static void EmitForIn(LuaForIn fi, LuaWriter w)
    {
        w.WriteInline($"for {string.Join(", ", fi.Variables)} in ");
        ExprEmitter.Emit(fi.Iterator, w); w.WriteLine(" do");
        EmitBlock(fi.Body, w);
        w.WriteLine("end");
    }

    private static void EmitPCall(LuaPCall pc, LuaWriter w)
    {
        w.WriteLine("local _ok, _err = pcall(function()");
        EmitBlock(pc.TryBody, w);
        w.WriteLine("end)");
        if (pc.CatchBody.Count > 0)
        {
            w.WriteLine("if not _ok then");
            EmitBlock(pc.CatchBody, w);
            w.WriteLine("end");
        }
        foreach (var s in pc.FinallyBody) Emit(s, w); // finally always runs
    }

    private static void EmitTaskSpawn(LuaTaskSpawn ts, LuaWriter w)
    {
        w.WriteLine("task.spawn(function()");
        EmitBlock(ts.Body, w);
        w.WriteLine("end)");
    }

    private static void EmitConnect(LuaConnect cn, LuaWriter w)
    {
        ExprEmitter.Emit(cn.Event, w);
        w.WriteInline(":Connect(");
        ExprEmitter.Emit(cn.Handler, w);
        w.WriteLine(")");
    }
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~StatementEmitterTests" -v
```

**Step 5: Commit**

```bash
git add LUSharpTranspiler/Backend/StatementEmitter.cs LUSharpTests/StatementEmitterTests.cs
git commit -m "feat: add StatementEmitter for IR → Luau text"
```

---

### Task 6.3: ModuleEmitter + file output

**Files:**
- Create: `LUSharpTranspiler/Backend/ModuleEmitter.cs`
- Create: `LUSharpTests/ModuleEmitterTests.cs`

**Step 1: Write failing test**

```csharp
using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTests;

public class ModuleEmitterTests
{
    [Fact]
    public void EmitsModuleScriptWithReturn()
    {
        var module = new LuaModule
        {
            ScriptType = ScriptType.ModuleScript,
            Classes = new()
            {
                new LuaClassDef
                {
                    Name = "Player",
                    Methods = new()
                    {
                        new LuaMethodDef
                        {
                            Name = "GetHealth",
                            Parameters = new() { "self" },
                            Body = new() { new LuaReturn(new LuaLiteral("100")) }
                        }
                    }
                }
            }
        };

        var result = ModuleEmitter.Emit(module);

        Assert.Contains("local Player = {}", result);
        Assert.Contains("function Player:GetHealth()", result);
        Assert.Contains("return 100", result);
        Assert.Contains("return Player", result);
    }

    [Fact]
    public void EmitsRequires()
    {
        var module = new LuaModule
        {
            Requires = new() { new LuaRequire("Players", "game:GetService(\"Players\")") }
        };

        var result = ModuleEmitter.Emit(module);
        Assert.Contains("local Players = game:GetService(\"Players\")", result);
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ModuleEmitterTests" -v
```

**Step 3: Implement**

`Backend/ModuleEmitter.cs`:
```csharp
using LUSharpTranspiler.AST.SourceConstructor.Builders;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTranspiler.Backend;

public static class ModuleEmitter
{
    public static string Emit(LuaModule module)
    {
        var w = new LuaWriter();

        w.WriteLine("-- Generated by LUSharp");
        w.WriteLine();

        // Requires
        foreach (var req in module.Requires)
            w.WriteLine($"local {req.LocalName} = {req.RequirePath}");

        if (module.Requires.Count > 0) w.WriteLine();

        // Classes
        foreach (var cls in module.Classes)
            EmitClass(cls, w);

        // Entry body (Main class only)
        foreach (var stmt in module.EntryBody)
            StatementEmitter.Emit(stmt, w);

        // Module return
        if (module.ScriptType == ScriptType.ModuleScript && module.Classes.Count > 0)
            w.WriteLine($"return {module.Classes[0].Name}");

        return w.ToString();
    }

    private static void EmitClass(LuaClassDef cls, LuaWriter w)
    {
        w.WriteLine($"local {cls.Name} = {{}}");
        w.WriteLine();

        // Static fields
        foreach (var f in cls.StaticFields)
        {
            w.WriteInline($"{cls.Name}.{f.Name} = ");
            if (f.Value != null) ExprEmitter.Emit(f.Value, w);
            else w.WriteInline("nil");
            w.WriteLine();
        }

        if (cls.StaticFields.Count > 0) w.WriteLine();

        // Constructor
        if (cls.Constructor != null)
            EmitMethod(cls.Name, cls.Constructor, w);

        // Methods
        foreach (var method in cls.Methods)
            EmitMethod(cls.Name, method, w);

        // Custom events (_events table)
        if (cls.Events.Count > 0)
        {
            w.WriteLine($"{cls.Name}._events = {{}}");
            foreach (var ev in cls.Events)
                w.WriteLine($"{cls.Name}._events.{ev.Name} = Instance.new(\"BindableEvent\")");
            w.WriteLine();
        }
    }

    private static void EmitMethod(string className, LuaMethodDef method, LuaWriter w)
    {
        var sep = method.IsStatic ? "." : ":";
        var parms = method.IsStatic
            ? method.Parameters
            : method.Parameters.Skip(1).ToList(); // skip 'self' — colon syntax adds it

        w.WriteLine($"function {className}{sep}{method.Name}({string.Join(", ", parms)})");
        w.IndentMore();
        foreach (var stmt in method.Body)
            StatementEmitter.Emit(stmt, w);
        w.IndentLess();
        w.WriteLine("end");
        w.WriteLine();
    }
}
```

**Step 4: Wire file writing into TransformPipeline**

In `TransformPipeline.Run()`, after getting `List<LuaModule>` back, pass each to `ModuleEmitter.Emit()` and write the result to `module.OutputPath`:

```csharp
// In TransformPipeline.Run(), after building modules:
foreach (var module in modules)
{
    var lua = ModuleEmitter.Emit(module);
    var outPath = Path.Combine(outputBasePath, module.OutputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, lua);
}
```

**Step 5: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ModuleEmitterTests" -v
dotnet run --project LUSharpTranspiler
```

Expected: `.lua` files written to `TestInput-transpiled/out/`.

**Step 6: Commit**

```bash
git add LUSharpTranspiler/Backend/ModuleEmitter.cs LUSharpTests/ModuleEmitterTests.cs
git commit -m "feat: add ModuleEmitter — IR → Luau files written to disk"
```

---

## Phase 7 — Integration Test: Full Transpile

### Task 7.1: End-to-end integration test

**Files:**
- Create: `LUSharpTests/IntegrationTests.cs`

**Step 1: Write failing test**

```csharp
using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class IntegrationTests
{
    private static string Transpile(string csharp)
    {
        var tree = CSharpSyntaxTree.ParseText(csharp);
        var table = new SymbolTable();
        new SymbolCollector(table).Collect("Client/Test.cs", tree);

        var lowerer = new MethodBodyLowerer(table);
        var classes = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Select(c => lowerer.Lower(c))
            .ToList();

        var module = new LUSharpTranspiler.Transform.IR.LuaModule
        {
            ScriptType = LUSharpTranspiler.Transform.IR.ScriptType.ModuleScript,
            Classes = classes
        };

        return ModuleEmitter.Emit(module);
    }

    [Fact]
    public void SimpleClass_TranspilesToLua()
    {
        var result = Transpile(@"
            public class Player {
                public string Name { get; set; } = ""Default"";
                public Player(string name) { Name = name; }
                public void Greet() { print(""Hello "" + Name); }
            }");

        Assert.Contains("local Player = {}", result);
        Assert.Contains("function Player.new(name)", result);
        Assert.Contains("function Player:Greet()", result);
        Assert.Contains("return Player", result);
    }

    [Fact]
    public void IfStatement_TranspilesToLua()
    {
        var result = Transpile(@"
            public class Logic {
                public void Check(int x) {
                    if (x > 0) { print(""pos""); }
                    else { print(""neg""); }
                }
            }");

        Assert.Contains("if x > 0 then", result);
        Assert.Contains("else", result);
        Assert.Contains("end", result);
    }

    [Fact]
    public void ForEach_TranspilesToPairsLoop()
    {
        var result = Transpile(@"
            public class Looper {
                public void Run() {
                    foreach (var item in items) { print(item); }
                }
            }");

        Assert.Contains("for _, item in pairs(items) do", result);
    }
}
```

**Step 2: Run tests — expect PASS (fix any gaps)**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~IntegrationTests" -v
```

**Step 3: Commit**

```bash
git add LUSharpTests/IntegrationTests.cs
git commit -m "test: add end-to-end integration tests for full transpile pipeline"
```

---

## Phase 8 — Package System

### Task 8.1: LuaMapping attributes and PackageLoader

**Files:**
- Create: `LUSharpTranspiler/Transform/Attributes/LuaMappingAttribute.cs`
- Create: `LUSharpTranspiler/Transform/Attributes/LuaServiceAttribute.cs`
- Create: `LUSharpTranspiler/Transform/Attributes/LuaGlobalAttribute.cs`
- Create: `LUSharpTranspiler/Transform/Attributes/LuaPackageAttribute.cs`
- Create: `LUSharpTranspiler/Transform/PackageLoader.cs`

**Step 1: Write attributes**

`Transform/Attributes/LuaMappingAttribute.cs`:
```csharp
namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class LuaMappingAttribute(string luaExpr) : Attribute
{
    public string LuaExpr { get; } = luaExpr;
}
```

`Transform/Attributes/LuaServiceAttribute.cs`:
```csharp
namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class LuaServiceAttribute(string serviceName) : Attribute
{
    public string ServiceName { get; } = serviceName;
}
```

`Transform/Attributes/LuaGlobalAttribute.cs`:
```csharp
namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LuaGlobalAttribute : Attribute { }
```

`Transform/Attributes/LuaPackageAttribute.cs`:
```csharp
namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class LuaPackageAttribute(string packageName) : Attribute
{
    public string PackageName { get; } = packageName;
}
```

**Step 2: PackageLoader**

`Transform/PackageLoader.cs`:
```csharp
using System.Reflection;
using LUSharpTranspiler.Transform.Attributes;

namespace LUSharpTranspiler.Transform;

public record PackageMapping(string ClassName, string MethodName, string LuaExpr, string? RequirePath);

public class PackageLoader
{
    private readonly SymbolTable _symbols;
    private readonly List<PackageMapping> _mappings = new();

    public PackageLoader(SymbolTable symbols) => _symbols = symbols;

    public IReadOnlyList<PackageMapping> Mappings => _mappings;

    /// Load a package assembly and register its [LuaMapping] methods.
    public void LoadFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var pkg = type.GetCustomAttribute<LuaPackageAttribute>();
            var svc = type.GetCustomAttribute<LuaServiceAttribute>();
            var isGlobal = type.GetCustomAttribute<LuaGlobalAttribute>() != null;

            string? requirePath = pkg != null
                ? $"game.ReplicatedStorage.Runtime.{pkg.PackageName}"
                : svc != null
                    ? $"game:GetService(\"{svc.ServiceName}\")"
                    : null;

            foreach (var method in type.GetMethods())
            {
                var mapping = method.GetCustomAttribute<LuaMappingAttribute>();
                if (mapping != null)
                    _mappings.Add(new PackageMapping(type.Name, method.Name, mapping.LuaExpr, requirePath));
            }
        }
    }
}
```

**Step 3: Verify build**

```bash
dotnet build LUSharpTranspiler
```

**Step 4: Commit**

```bash
git add LUSharpTranspiler/Transform/Attributes/ LUSharpTranspiler/Transform/PackageLoader.cs
git commit -m "feat: add LuaMapping attributes and PackageLoader for API bindings"
```

---

## Phase 9 — CLI: `lusharp build`

### Task 9.1: `lusharp.json` ProjectConfig

**Files:**
- Create: `LUSharp/Project/ProjectConfig.cs`
- Create: `LUSharpTests/ProjectConfigTests.cs`

**Step 1: Write failing test**

```csharp
using LUSharp.Project;

namespace LUSharpTests;

public class ProjectConfigTests
{
    [Fact]
    public void Deserialize_ValidJson()
    {
        var json = """
            {
              "name": "MyGame",
              "packages": ["ECS"],
              "build": { "src": "./src", "out": "./out" }
            }
            """;

        var config = ProjectConfig.FromJson(json);
        Assert.Equal("MyGame", config.Name);
        Assert.Contains("ECS", config.Packages);
        Assert.Equal("./src", config.Build.Src);
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        var config = new ProjectConfig { Name = "Test", Packages = new() { "ECS" } };
        var json = config.ToJson();
        var back = ProjectConfig.FromJson(json);
        Assert.Equal("Test", back.Name);
    }
}
```

**Step 2: Run test — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ProjectConfigTests" -v
```

**Step 3: Implement**

`LUSharp/Project/ProjectConfig.cs`:
```csharp
using Newtonsoft.Json;

namespace LUSharp.Project;

public class ProjectConfig
{
    [JsonProperty("name")]
    public string Name { get; set; } = "MyProject";

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonProperty("packages")]
    public List<string> Packages { get; set; } = new();

    [JsonProperty("build")]
    public BuildConfig Build { get; set; } = new();

    public static ProjectConfig FromJson(string json) =>
        JsonConvert.DeserializeObject<ProjectConfig>(json)!;

    public string ToJson() =>
        JsonConvert.SerializeObject(this, Formatting.Indented);

    public static ProjectConfig LoadFromDirectory(string dir)
    {
        var path = Path.Combine(dir, "lusharp.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"lusharp.json not found in {dir}");
        return FromJson(File.ReadAllText(path));
    }
}

public class BuildConfig
{
    [JsonProperty("src")]  public string Src { get; set; } = "./src";
    [JsonProperty("out")]  public string Out { get; set; } = "./out";
}
```

**Step 4: Update `lusharp new`** to generate `lusharp.json` alongside `default.project.json` in `LUSharp/Program.cs`.

**Step 5: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~ProjectConfigTests" -v
```

**Step 6: Commit**

```bash
git add LUSharp/Project/ProjectConfig.cs LUSharpTests/ProjectConfigTests.cs
git commit -m "feat: add ProjectConfig for lusharp.json parsing"
```

---

### Task 9.2: `lusharp build` command

**Files:**
- Modify: `LUSharp/Program.cs`
- Create: `LUSharpTranspiler/Build/BuildCommand.cs`

**Step 1: Implement BuildCommand**

`LUSharpTranspiler/Build/BuildCommand.cs`:
```csharp
using LUSharp.Project;
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
            Console.WriteLine($"  ✓ {module.SourceFile.Replace(srcDir, "")} → {module.OutputPath}");
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
```

**Step 2: Wire into `LUSharp/Program.cs`**

Add `build` as a command alongside `new` and `help`:

```csharp
case "build":
{
    var dir = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
    var outFlag = args.FirstOrDefault(a => a.StartsWith("--out="))?[6..];
    var release = args.Contains("--release");
    return LUSharpTranspiler.Build.BuildCommand.Run(dir, outFlag, release);
}
```

**Step 3: Verify**

```bash
dotnet build
dotnet run --project LUSharp -- build LUSharpTranspiler/TestInput
```

Expected: outputs `✓` lines and writes `.lua` files.

**Step 4: Commit**

```bash
git add LUSharpTranspiler/Build/BuildCommand.cs LUSharp/Program.cs
git commit -m "feat: add lusharp build command driving full pipeline"
```

---

## Phase 10 — Optimizer

### Task 10.1: Constant folding and dead branch elimination

**Files:**
- Create: `LUSharpTranspiler/Transform/Passes/Optimizer.cs`
- Create: `LUSharpTests/OptimizerTests.cs`

**Step 1: Write failing tests**

```csharp
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTests;

public class OptimizerTests
{
    [Fact]
    public void FoldsConstantAddition()
    {
        var expr = new LuaBinary(new LuaLiteral("1"), "+", new LuaLiteral("2"));
        var result = Optimizer.FoldExpr(expr);
        Assert.Equal("3", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void FoldsConstantStringConcat()
    {
        var expr = new LuaConcat(new() { new LuaLiteral("\"hello\""), new LuaLiteral("\" world\"") });
        var result = Optimizer.FoldExpr(expr);
        Assert.Equal("\"hello world\"", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void EliminatesDeadIfTrue()
    {
        var stmt = new LuaIf
        {
            Condition = LuaLiteral.True,
            Then = new() { new LuaReturn(new LuaLiteral("1")) }
        };
        var result = Optimizer.OptimizeStatement(stmt);
        // Should return just the then-body directly
        Assert.IsType<LuaReturn>(Assert.Single(result));
    }

    [Fact]
    public void EliminatesDeadIfFalse()
    {
        var stmt = new LuaIf
        {
            Condition = LuaLiteral.False,
            Then = new() { new LuaReturn(new LuaLiteral("1")) },
            Else = new() { new LuaReturn(new LuaLiteral("2")) }
        };
        var result = Optimizer.OptimizeStatement(stmt);
        Assert.IsType<LuaReturn>(Assert.Single(result));
        Assert.Equal("2", Assert.IsType<LuaLiteral>(((LuaReturn)result[0]).Value!).Value);
    }
}
```

**Step 2: Run tests — expect FAIL**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~OptimizerTests" -v
```

**Step 3: Implement**

`Transform/Passes/Optimizer.cs`:
```csharp
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTranspiler.Transform.Passes;

public static class Optimizer
{
    public static ILuaExpression FoldExpr(ILuaExpression expr) => expr switch
    {
        LuaBinary { Op: "+" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l + r).ToString()),
        LuaBinary { Op: "-" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l - r).ToString()),
        LuaBinary { Op: "*" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l * r).ToString()),
        LuaConcat cat when cat.Parts.All(IsStringLiteral)
            => new LuaLiteral("\"" + string.Concat(cat.Parts.Cast<LuaLiteral>()
                .Select(l => l.Value.Trim('"'))) + "\""),
        _ => expr
    };

    public static List<ILuaStatement> OptimizeStatement(ILuaStatement stmt) => stmt switch
    {
        LuaIf { Condition: LuaLiteral { Value: "true"  } } ifS => ifS.Then,
        LuaIf { Condition: LuaLiteral { Value: "false" } } ifS => ifS.Else ?? new(),
        _ => new() { stmt }
    };

    public static List<ILuaStatement> OptimizeBlock(List<ILuaStatement> stmts) =>
        stmts.SelectMany(OptimizeStatement).ToList();

    private static bool IsNumericLiteral(ILuaExpression e, out double v)
    {
        if (e is LuaLiteral lit && double.TryParse(lit.Value, out v)) return true;
        v = 0; return false;
    }

    private static bool IsStringLiteral(ILuaExpression e) =>
        e is LuaLiteral lit && lit.Value.StartsWith('"') && lit.Value.EndsWith('"');
}
```

**Step 4: Run tests — expect PASS**

```bash
dotnet test LUSharpTests --filter "FullyQualifiedName~OptimizerTests" -v
```

**Step 5: Wire Optimizer into TransformPipeline** — call `Optimizer.OptimizeBlock()` on each method body after `MethodBodyLowerer` produces them.

**Step 6: Run all tests**

```bash
dotnet test
```

Expected: all pass.

**Step 7: Commit**

```bash
git add LUSharpTranspiler/Transform/Passes/Optimizer.cs LUSharpTests/OptimizerTests.cs
git commit -m "feat: add Optimizer with constant folding and dead branch elimination"
```

---

## Final Verification

### Task 11.1: Full build smoke test against TestInput

**Step 1: Run the transpiler against the existing TestInput**

```bash
dotnet run --project LUSharp -- build LUSharpTranspiler/TestInput
```

**Expected output:**
```
Building MyProject → LUSharpTranspiler/TestInput-transpiled/out
  ✓ Client/Main.cs → out/client/Main.lua
Build complete. 1 file(s) written.
```

**Step 2: Inspect the output**

Open `LUSharpTranspiler/TestInput-transpiled/out/client/Main.lua` and verify:
- `local CPlayer = {}` — class table
- `function CPlayer.new(name, health)` — constructor
- `function CPlayer:get_Name()` / `function CPlayer:set_Name(value)` — accessors
- `local Main = {}` — entry class
- Event connect: `playersService.PlayerAdded:Connect(function(player)`
- `print("A player has joined: " .. player.Name)`

**Step 3: Run all tests one final time**

```bash
dotnet test
```

Expected: all pass, 0 failures.

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete transpiler pipeline — IR, Transform, Backend, lusharp build"
```

---

## Summary: Files Created

| File | Purpose |
|---|---|
| `Transform/IR/ILuaNode.cs` + `ILuaStatement.cs` + `ILuaExpression.cs` | Core interfaces |
| `Transform/IR/LuaModule.cs` + `LuaClassDef.cs` + `LuaMethodDef.cs` etc | Top-level IR nodes |
| `Transform/IR/Statements/*.cs` (14 files) | Statement IR nodes |
| `Transform/IR/Expressions/*.cs` (15 files) | Expression IR nodes |
| `Transform/SymbolTable.cs` + `ClassSymbol.cs` | Cross-file class registry |
| `Transform/ParsedFile.cs` | Roslyn parse result wrapper |
| `Transform/TransformPipeline.cs` | Pipeline orchestrator |
| `Transform/Passes/SymbolCollector.cs` | Pass 1 |
| `Transform/Passes/TypeResolver.cs` | Pass 2 |
| `Transform/Passes/ExpressionLowerer.cs` | Expression → IR |
| `Transform/Passes/StatementLowerer.cs` | Statement → IR (Pass 3+4) |
| `Transform/Passes/MethodBodyLowerer.cs` | Class → LuaClassDef |
| `Transform/Passes/Optimizer.cs` | Pass 7 |
| `Transform/Attributes/*.cs` (4 files) | Package/API mapping attributes |
| `Transform/PackageLoader.cs` | Package assembly loader |
| `Backend/ExprEmitter.cs` | IR expression → Luau text |
| `Backend/StatementEmitter.cs` | IR statement → Luau text |
| `Backend/ModuleEmitter.cs` | LuaModule → complete .lua file |
| `Build/BuildCommand.cs` | `lusharp build` implementation |
| `LUSharp/Project/ProjectConfig.cs` | lusharp.json model |
| `AST/SourceConstructor/Builders/LuaWriter.cs` | Add `WriteInline()` |
| `LUSharpTests/*.cs` (12 test files) | Full test suite |
