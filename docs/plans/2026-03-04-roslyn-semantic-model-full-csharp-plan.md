# Roslyn SemanticModel + Full C# Language Coverage Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Evolve `LuauEmitter.cs` to use Roslyn `SemanticModel` for type resolution and cover all C# syntax, targeting Newtonsoft.Json transpilation as the gate test.

**Architecture:** Build a `CSharpCompilation` from all source files + BCL references in `RoslynToLuau.PreScan`, pass `SemanticModel` to `LuauEmitter` per-file. Replace all heuristic type resolution with `model.GetTypeInfo()` / `model.GetSymbolInfo()` queries. Add `MethodMapper.cs` as a centralized method rewrite registry. Fill every TODO gap in LuauEmitter.

**Tech Stack:** Microsoft.CodeAnalysis.CSharp (Roslyn), .NET 8, Luau output

**Test commands:**
- `dotnet build LUSharpRoslynModule` — build
- `dotnet run --project LUSharpRoslynModule -- transpile-all` — transpile all RoslynSource files via LuauEmitter
- `dotnet run --project LUSharpRoslynModule -- reference self-emit` — self-hosted pipeline (12/12, frozen)
- `dotnet run --project LUSharpRoslynModule -- reference transpiler` — self-hosted cross-module (13/13, frozen)

**Key files:**
- `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs` (374 lines) — orchestrator
- `LUSharpRoslynModule/Transpiler/LuauEmitter.cs` (2947 lines) — core emitter
- `LUSharpRoslynModule/Transpiler/TypeMapper.cs` (39 lines) — primitive type map
- `LUSharpRoslynModule/Program.cs` — CLI entry point

---

## Phase 1: SemanticModel Foundation

### Task 1: Build CSharpCompilation in RoslynToLuau.PreScan

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs`

**Step 1: Add Compilation field and BCL reference resolution**

Add to `RoslynToLuau` class:

```csharp
private CSharpCompilation? _compilation;

private static List<MetadataReference> GetBclReferences()
{
    var refs = new List<MetadataReference>();
    var trustedDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
    var coreLibs = new[]
    {
        "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll",
        "System.Threading.Tasks.dll", "System.Console.dll", "System.Text.RegularExpressions.dll",
        "System.Memory.dll", "System.Collections.Concurrent.dll", "System.Collections.Immutable.dll",
        "System.ComponentModel.dll", "System.ComponentModel.Primitives.dll",
        "System.ObjectModel.dll", "System.Globalization.dll", "System.IO.dll",
        "System.Text.Encoding.dll", "System.Threading.dll", "System.Numerics.Vectors.dll",
        "System.Runtime.Numerics.dll", "System.Runtime.Extensions.dll",
        "System.Private.CoreLib.dll", "netstandard.dll",
    };
    foreach (var lib in coreLibs)
    {
        var path = Path.Combine(trustedDir, lib);
        if (File.Exists(path))
            refs.Add(MetadataReference.CreateFromFile(path));
    }
    return refs;
}
```

**Step 2: Modify PreScan to build Compilation**

In `PreScan`, after collecting all syntax trees, add:

```csharp
_compilation = CSharpCompilation.Create(
    "LUSharpTranspilation",
    syntaxTrees: allTrees,
    references: GetBclReferences(),
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithNullableContextOptions(NullableContextOptions.Enable)
);
```

**Step 3: Pass SemanticModel to LuauEmitter in Transpile()**

In `Transpile`, after parsing:

```csharp
var model = _compilation!.GetSemanticModel(tree);
var emitter = new LuauEmitter(model);
```

**Step 4: Build and verify transpile-all still works**

Run: `dotnet build LUSharpRoslynModule && dotnet run --project LUSharpRoslynModule -- transpile-all`
Expected: All files transpile (same output as before — SemanticModel not yet used).

**Step 5: Verify self-emit tests unaffected**

Run: `dotnet run --project LUSharpRoslynModule -- reference self-emit`
Expected: 12/12 (SimpleEmitter pipeline is independent).

**Step 6: Commit**

```
feat(roslyn): add CSharpCompilation + SemanticModel to RoslynToLuau
```

---

### Task 2: Add SemanticModel to LuauEmitter constructor

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add SemanticModel field and constructor parameter**

```csharp
private readonly SemanticModel? _model;

// New constructor
public LuauEmitter(SemanticModel model)
{
    _model = model;
}

// Keep existing parameterless constructor for backward compat
public LuauEmitter() { _model = null; }
```

**Step 2: Add helper methods for SemanticModel queries**

```csharp
private ITypeSymbol? GetExpressionType(ExpressionSyntax expr)
{
    if (_model == null) return null;
    return _model.GetTypeInfo(expr).Type;
}

private ISymbol? GetSymbol(ExpressionSyntax expr)
{
    if (_model == null) return null;
    return _model.GetSymbolInfo(expr).Symbol;
}

private bool IsStringType(ExpressionSyntax expr)
{
    var type = GetExpressionType(expr);
    return type?.SpecialType == SpecialType.System_String;
}

private bool IsEnumType(ExpressionSyntax expr)
{
    var type = GetExpressionType(expr);
    return type?.TypeKind == TypeKind.Enum;
}
```

**Step 3: Build and verify**

Run: `dotnet build LUSharpRoslynModule && dotnet run --project LUSharpRoslynModule -- transpile-all`
Expected: Same output (helpers not yet called).

**Step 4: Commit**

```
feat(roslyn): add SemanticModel field + type query helpers to LuauEmitter
```

---

### Task 3: Replace string concat heuristic with SemanticModel

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

This is the highest-impact heuristic replacement. Currently uses `IsStringConcatenation()` / `IsStringConcatenationChain()` with a hardcoded method name whitelist.

**Step 1: Modify EmitBinary for AddExpression**

In `EmitBinary`, for `SyntaxKind.AddExpression`, replace the `IsStringConcatenation` / `_forceStringConcat` logic with:

```csharp
case SyntaxKind.AddExpression:
    if (_model != null)
    {
        // SemanticModel: check if the + operator resolves to string concat
        var typeInfo = _model.GetTypeInfo(binary);
        if (typeInfo.Type?.SpecialType == SpecialType.System_String)
            return $"{left} .. {right}";
        return $"{left} + {right}";
    }
    // Fallback: old heuristic (when no SemanticModel)
    // ... keep existing logic ...
```

**Step 2: Similarly fix AddAssignmentExpression (+=)**

In `EmitAssignmentStatement`, for `SyntaxKind.AddAssignmentExpression`:

```csharp
if (_model != null && IsStringType(assignment.Left))
{
    AppendLine($"{left} = {left} .. {right}");
    return;
}
```

**Step 3: Build and run transpile-all, diff output**

Run: `dotnet run --project LUSharpRoslynModule -- transpile-all`
Expected: `+` operators on string expressions now correctly emit `..`. Diff `out/` before/after to verify improvements.

**Step 4: Commit**

```
feat(roslyn): replace string concat heuristic with SemanticModel type check
```

---

### Task 4: Replace element access heuristic with SemanticModel

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

Currently `IsLikelyStringAccess()` uses a hardcoded list of variable names (`_text`, `text`, `_source`...). Replace with:

**Step 1: Modify EmitElementAccess**

```csharp
private string EmitElementAccess(ElementAccessExpressionSyntax elem)
{
    var obj = EmitExpression(elem.Expression);
    var args = elem.ArgumentList.Arguments;

    if (args.Count == 1)
    {
        var index = EmitExpression(args[0].Expression);

        // SemanticModel: check receiver type
        if (_model != null)
        {
            var receiverType = GetExpressionType(elem.Expression);
            if (receiverType?.SpecialType == SpecialType.System_String)
                return $"string.byte({obj}, {index} + 1)";
            if (receiverType?.SpecialType == SpecialType.System_Char)
                return $"string.byte({obj}, {index} + 1)";
        }
        else if (IsLikelyStringAccess(elem.Expression))
        {
            return $"string.byte({obj}, {index} + 1)";
        }

        // Array/list: 0-to-1 index adjustment
        return $"{obj}[{index} + 1]";
    }
    // Multi-dimensional
    var allArgs = string.Join(", ", args.Select(a => EmitExpression(a.Expression)));
    return $"{obj}[{allArgs}]";
}
```

**Step 2: Build, transpile-all, verify**

**Step 3: Commit**

```
feat(roslyn): replace element access string heuristic with SemanticModel
```

---

### Task 5: Replace field type tracking with SemanticModel

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

Currently `_instanceFieldTypes`, `_currentMethodParamTypes`, `_currentMethodLocals`, `_currentMethodParams` track types manually. With SemanticModel, these become queries.

**Step 1: Add method-level type query**

```csharp
private ITypeSymbol? GetReceiverType(ExpressionSyntax expr)
{
    if (_model == null) return null;
    // For member access like obj.Method(), get the type of obj
    if (expr is MemberAccessExpressionSyntax ma)
        return GetExpressionType(ma.Expression);
    return GetExpressionType(expr);
}
```

**Step 2: Modify EmitMemberInvocation to use GetReceiverType**

Where the emitter currently checks `_instanceFieldTypes[fieldName]` to determine how to dispatch `fieldObj.Method()`, replace with:

```csharp
if (_model != null)
{
    var receiverType = GetExpressionType(memberAccess.Expression);
    if (receiverType != null)
    {
        var typeName = receiverType.Name;
        // Use typeName for method dispatch instead of _instanceFieldTypes lookup
    }
}
```

This replaces the `EmitFieldMethodCall` path that guesses field types.

**Step 3: Modify EmitMemberAccess .Length/.Count**

```csharp
if (memberName == "Length" || memberName == "Count")
{
    if (_model != null)
    {
        var ownerType = GetExpressionType(memberAccess.Expression);
        if (ownerType?.SpecialType == SpecialType.System_String
            || ownerType is IArrayTypeSymbol
            || ownerType?.Name is "List" or "Array" or "Queue" or "Stack")
        {
            return $"#{obj}";
        }
    }
    // Fallback heuristic...
}
```

**Step 4: Build, transpile-all, diff, verify**

**Step 5: Commit**

```
feat(roslyn): replace field/param type tracking with SemanticModel queries
```

---

## Phase 2: Fill Expression Gaps

### Task 6: Add interpolated string support

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

Currently falls to `--[[TODO: InterpolatedStringExpression]]`.

**Step 1: Add EmitInterpolatedString method**

```csharp
private string EmitInterpolatedString(InterpolatedStringExpressionSyntax interp)
{
    var sb = new StringBuilder("`");
    foreach (var content in interp.Contents)
    {
        if (content is InterpolatedStringTextSyntax text)
        {
            // Literal text — escape backticks and braces for Luau
            var raw = text.TextToken.Text;
            raw = raw.Replace("\\n", "\n").Replace("\\t", "\t")
                     .Replace("\\r", "\r").Replace("\\\"", "\"");
            sb.Append(raw);
        }
        else if (content is InterpolationSyntax hole)
        {
            sb.Append('{');
            sb.Append(EmitExpression(hole.Expression));
            sb.Append('}');
            // Note: hole.FormatClause (e.g. :F2) is dropped — Luau backtick
            // strings don't support format specifiers
        }
    }
    sb.Append('`');
    return sb.ToString();
}
```

**Step 2: Add to EmitExpression switch**

```csharp
InterpolatedStringExpressionSyntax interp => EmitInterpolatedString(interp),
```

**Step 3: Build, test with a file containing `$"Hello {name}"`**

**Step 4: Commit**

```
feat(roslyn): add interpolated string emission ($"..." → backtick)
```

---

### Task 7: Add is/as expressions and pattern matching

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add is-expression to EmitBinary (or as separate handler)**

In `EmitExpression`, add:

```csharp
IsPatternExpressionSyntax isPattern => EmitIsPattern(isPattern),
BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } bin => EmitIsExpression(bin),
BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } bin => EmitExpression(bin.Left),
```

```csharp
private string EmitIsExpression(BinaryExpressionSyntax binary)
{
    var left = EmitExpression(binary.Left);
    var typeName = binary.Right.ToString();
    if (typeName is "string") return $"(type({left}) == \"string\")";
    if (typeName is "int" or "float" or "double" or "long" or "decimal")
        return $"(type({left}) == \"number\")";
    if (typeName is "bool") return $"(type({left}) == \"boolean\")";
    return $"({left} ~= nil)";
}

private string EmitIsPattern(IsPatternExpressionSyntax isPattern)
{
    var left = EmitExpression(isPattern.Expression);
    return EmitPattern(left, isPattern.Pattern);
}

private string EmitPattern(string subject, PatternSyntax pattern)
{
    return pattern switch
    {
        ConstantPatternSyntax cp => $"{subject} == {EmitExpression(cp.Expression)}",
        DeclarationPatternSyntax dp => EmitDeclarationPattern(subject, dp),
        UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } np =>
            $"not ({EmitPattern(subject, np.Pattern)})",
        BinaryPatternSyntax bp when bp.IsKind(SyntaxKind.OrPattern) =>
            $"({EmitPattern(subject, bp.Left)} or {EmitPattern(subject, bp.Right)})",
        BinaryPatternSyntax bp when bp.IsKind(SyntaxKind.AndPattern) =>
            $"({EmitPattern(subject, bp.Left)} and {EmitPattern(subject, bp.Right)})",
        RelationalPatternSyntax rp => $"{subject} {rp.OperatorToken.Text} {EmitExpression(rp.Expression)}",
        DiscardPatternSyntax => "true",
        TypePatternSyntax tp => EmitTypeCheck(subject, tp.Type.ToString()),
        VarPatternSyntax vp => EmitVarPattern(subject, vp),
        _ => $"--[[TODO: pattern {pattern.Kind()}]] true",
    };
}
```

**Step 2: Add DeclarationPattern handler**

```csharp
private string EmitDeclarationPattern(string subject, DeclarationPatternSyntax dp)
{
    var typeName = dp.Type.ToString();
    if (dp.Designation is SingleVariableDesignationSyntax svd)
    {
        var varName = svd.Identifier.Text;
        _currentMethodLocals.Add(varName);
        AppendLine($"local {varName} = {subject}");
        return EmitTypeCheck(varName, typeName);
    }
    return EmitTypeCheck(subject, typeName);
}

private string EmitTypeCheck(string subject, string typeName)
{
    if (typeName is "string") return $"type({subject}) == \"string\"";
    if (typeName is "int" or "float" or "double" or "long" or "decimal")
        return $"type({subject}) == \"number\"";
    if (typeName is "bool") return $"type({subject}) == \"boolean\"";
    return $"{subject} ~= nil";
}
```

**Step 3: Update EmitSwitchPattern to use EmitPattern**

Replace the existing `EmitSwitchPattern` body with:

```csharp
private string EmitSwitchPattern(string subject, PatternSyntax pattern)
{
    return EmitPattern(subject, pattern);
}
```

**Step 4: Build, test with `if (x is string s)` and `switch` with type patterns**

**Step 5: Commit**

```
feat(roslyn): add is/as expressions and full pattern matching
```

---

### Task 8: Add await, typeof, throw expressions

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add AwaitExpression**

```csharp
AwaitExpressionSyntax awaitExpr => EmitAwait(awaitExpr),
```

```csharp
private string EmitAwait(AwaitExpressionSyntax awaitExpr)
{
    var inner = EmitExpression(awaitExpr.Expression);
    // await Task.Delay(ms) → task.wait(ms / 1000)
    if (inner.StartsWith("Task.Delay(") && inner.EndsWith(")"))
    {
        var arg = inner["Task.Delay(".Length..^1];
        return $"task.wait({arg} / 1000)";
    }
    if (inner is "Task.Yield()") return "task.wait()";
    // General: just call the expression (Roblox coroutines yield naturally)
    return inner;
}
```

**Step 2: Add TypeOfExpression**

```csharp
TypeOfExpressionSyntax typeOf => $"\"{typeOf.Type}\"",
```

**Step 3: Fix ThrowExpression**

```csharp
private string EmitThrowExpression(ThrowExpressionSyntax throwExpr)
{
    var inner = EmitExpression(throwExpr.Expression);
    return $"error({inner})";
}
```

**Step 4: Build, test, commit**

```
feat(roslyn): add await, typeof, throw expression emission
```

---

### Task 9: Add tuple expressions and deconstruction

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add TupleExpression**

```csharp
TupleExpressionSyntax tuple => EmitTuple(tuple),
DeclarationExpressionSyntax decl => EmitDeclarationExpression(decl),
```

```csharp
private string EmitTuple(TupleExpressionSyntax tuple)
{
    // (a, b, c) → multi-value (comma-separated)
    var args = string.Join(", ", tuple.Arguments.Select(a => EmitExpression(a.Expression)));
    return args;
}
```

**Step 2: Handle tuple deconstruction in EmitLocalDeclaration**

When `LocalDeclarationStatementSyntax` has a tuple pattern on the left:

```csharp
// var (a, b) = GetPair();
if (declaration.Declaration.Variables.Count == 1)
{
    var variable = declaration.Declaration.Variables[0];
    if (variable.Initializer?.Value is TupleExpressionSyntax ||
        declaration.Declaration.Type is TupleTypeSyntax)
    {
        // Handle deconstruction
    }
}
```

For `DeclarationExpressionSyntax` with `ParenthesizedVariableDesignationSyntax`:

```csharp
private string EmitDeclarationExpression(DeclarationExpressionSyntax decl)
{
    if (decl.Designation is ParenthesizedVariableDesignationSyntax pvd)
    {
        var names = pvd.Variables.Select(v => v.ToString());
        return string.Join(", ", names);
    }
    if (decl.Designation is SingleVariableDesignationSyntax svd)
        return svd.Identifier.Text;
    return $"--[[TODO: decl {decl.Designation.Kind()}]] nil";
}
```

**Step 3: Build, test, commit**

```
feat(roslyn): add tuple expressions and deconstruction
```

---

## Phase 3: Fill Statement Gaps

### Task 10: Add using, lock, yield statements

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add to EmitStatement**

```csharp
case UsingStatementSyntax usingStmt:
    EmitUsingStatement(usingStmt);
    break;
case LocalDeclarationStatementSyntax local when local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword):
    EmitUsingDeclaration(local);
    break;
case LockStatementSyntax lockStmt:
    AppendLine($"-- lock ({EmitExpression(lockStmt.Expression)})");
    EmitStatementBody(lockStmt.Statement);
    break;
case YieldStatementSyntax yieldStmt:
    EmitYieldStatement(yieldStmt);
    break;
```

```csharp
private void EmitUsingStatement(UsingStatementSyntax usingStmt)
{
    if (usingStmt.Declaration != null)
    {
        foreach (var v in usingStmt.Declaration.Variables)
        {
            var init = v.Initializer != null ? EmitExpression(v.Initializer.Value) : "nil";
            AppendLine($"local {v.Identifier.Text} = {init}");
        }
    }
    else if (usingStmt.Expression != null)
    {
        AppendLine($"local __using = {EmitExpression(usingStmt.Expression)}");
    }
    AppendLine("do");
    _indent++;
    EmitStatementBody(usingStmt.Statement);
    _indent--;
    AppendLine("end");
}

private void EmitYieldStatement(YieldStatementSyntax yieldStmt)
{
    if (yieldStmt.IsKind(SyntaxKind.YieldReturnStatement))
    {
        var expr = EmitExpression(yieldStmt.Expression!);
        AppendLine($"coroutine.yield({expr})");
    }
    else // YieldBreakStatement
    {
        AppendLine("return");
    }
}
```

**Step 2: Build, test, commit**

```
feat(roslyn): add using, lock, yield statement emission
```

---

### Task 11: Add object initializer support

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

Currently object initializers `new Type() { A = 1, B = 2 }` are silently dropped. The emitter emits `Type.new()` but ignores the initializer assignments.

**Step 1: Modify EmitObjectCreation to handle InitializerExpression**

```csharp
private string EmitObjectCreation(ObjectCreationExpressionSyntax creation)
{
    // ... existing new Type(args) logic ...
    var result = $"{typeName}.new({args})";

    if (creation.Initializer != null)
    {
        // Object initializer: generate temp var, assign fields, return
        var tempVar = $"__init_{_tempVarCounter++}";
        // This is complex in expression context. Use IIFE pattern:
        // (function() local t = Type.new(args); t.A = 1; t.B = 2; return t end)()
        var sb = new StringBuilder();
        sb.Append($"(function() local {tempVar} = {result}; ");
        foreach (var expr in creation.Initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assign)
            {
                var name = assign.Left.ToString();
                var value = EmitExpression(assign.Right);
                sb.Append($"{tempVar}.{name} = {value}; ");
            }
        }
        sb.Append($"return {tempVar} end)()");
        return sb.ToString();
    }

    return result;
}
```

Add `private int _tempVarCounter = 0;` field.

**Step 2: Build, test with `new Player() { Name = "Alex", Health = 100 }`**

**Step 3: Commit**

```
feat(roslyn): add object initializer emission
```

---

### Task 12: Add static class field/property support

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

Currently `EmitStaticClass` only handles methods; fields/properties emit `-- TODO`.

**Step 1: Add field/property handling to EmitStaticClass**

In the member loop, before the TODO fallback:

```csharp
case FieldDeclarationSyntax field:
    foreach (var variable in field.Declaration.Variables)
    {
        var name = variable.Identifier.Text;
        var init = variable.Initializer != null
            ? EmitExpression(variable.Initializer.Value)
            : GetDefaultValue(field.Declaration.Type.ToString());
        if (field.Modifiers.Any(SyntaxKind.ConstKeyword))
            AppendLine($"{className}.{name} = {init}");
        else
            AppendLine($"{className}.{name} = {init}");
    }
    break;
case PropertyDeclarationSyntax prop:
    EmitProperty(className, prop);
    break;
```

**Step 2: Build, test, commit**

```
feat(roslyn): add static class field and property emission
```

---

## Phase 4: MethodMapper + Comprehensive Method Rewrites

### Task 13: Create MethodMapper.cs

**Files:**
- Create: `LUSharpRoslynModule/Transpiler/MethodMapper.cs`

**Step 1: Create the centralized mapping registry**

```csharp
using Microsoft.CodeAnalysis;

namespace LUSharpRoslynModule.Transpiler;

public delegate string MethodRewriter(string receiver, string[] args, ITypeSymbol? receiverType);

public static class MethodMapper
{
    private static readonly Dictionary<(string TypeName, string MethodName), MethodRewriter> _map = new();

    public static void Register(string typeName, string methodName, MethodRewriter rewriter)
    {
        _map[(typeName, methodName)] = rewriter;
    }

    public static string? TryRewrite(IMethodSymbol method, string receiver, string[] args, ITypeSymbol? receiverType)
    {
        var typeName = method.ContainingType?.Name ?? "";
        // Try exact type match first, then interfaces
        if (_map.TryGetValue((typeName, method.Name), out var rewriter))
            return rewriter(receiver, args, receiverType);
        // Try base types and interfaces
        var current = method.ContainingType?.BaseType;
        while (current != null)
        {
            if (_map.TryGetValue((current.Name, method.Name), out rewriter))
                return rewriter(receiver, args, receiverType);
            current = current.BaseType;
        }
        return null;
    }

    static MethodMapper()
    {
        RegisterAll();
    }

    private static void RegisterAll()
    {
        // === Console ===
        Register("Console", "WriteLine", (r, a, _) => a.Length > 0 ? $"print({a[0]})" : "print()");
        Register("Console", "Write", (r, a, _) => a.Length > 0 ? $"print({a[0]})" : "print()");

        // === String instance ===
        Register("String", "Substring", (r, a, _) => a.Length == 2
            ? $"string.sub({r}, {a[0]} + 1, {a[0]} + {a[1]})"
            : $"string.sub({r}, {a[0]} + 1)");
        Register("String", "Contains", (r, a, _) => $"(string.find({r}, {a[0]}, 1, true) ~= nil)");
        Register("String", "StartsWith", (r, a, _) => $"(string.sub({r}, 1, #{a[0]}) == {a[0]})");
        Register("String", "EndsWith", (r, a, _) => $"(string.sub({r}, -#{a[0]}) == {a[0]})");
        Register("String", "ToLower", (r, a, _) => $"string.lower({r})");
        Register("String", "ToUpper", (r, a, _) => $"string.upper({r})");
        Register("String", "Trim", (r, a, _) => "string.match(" + r + ", \"^%s*(.-)%s*$\")");
        Register("String", "TrimStart", (r, a, _) => "string.match(" + r + ", \"^%s*(.*)\")");
        Register("String", "TrimEnd", (r, a, _) => "string.match(" + r + ", \"(.-)%s*$\")");
        Register("String", "Replace", (r, a, _) => $"string.gsub({r}, {a[0]}, {a[1]})");
        Register("String", "Split", (r, a, _) => $"string.split({r}, {a[0]})");
        Register("String", "IndexOf", (r, a, _) => $"((string.find({r}, {a[0]}, 1, true) or 0) - 1)");
        Register("String", "ToString", (r, a, _) => $"tostring({r})");
        Register("String", "Insert", (r, a, _) => $"string.sub({r}, 1, {a[0]}) .. {a[1]} .. string.sub({r}, {a[0]} + 1)");
        Register("String", "Remove", (r, a, _) => a.Length == 2
            ? $"string.sub({r}, 1, {a[0]}) .. string.sub({r}, {a[0]} + {a[1]} + 1)"
            : $"string.sub({r}, 1, {a[0]})");
        Register("String", "PadLeft", (r, a, _) => a.Length == 2
            ? $"string.rep({a[1]}, {a[0]} - #{r}) .. {r}"
            : $"string.rep(\" \", {a[0]} - #{r}) .. {r}");
        Register("String", "PadRight", (r, a, _) => a.Length == 2
            ? $"{r} .. string.rep({a[1]}, {a[0]} - #{r})"
            : $"{r} .. string.rep(\" \", {a[0]} - #{r})");

        // === String static ===
        Register("String", "IsNullOrEmpty", (r, a, _) => $"({a[0]} == nil or {a[0]} == \"\")");
        Register("String", "IsNullOrWhiteSpace", (r, a, _) => $"({a[0]} == nil or string.match({a[0]}, \"^%s*$\") ~= nil)");
        Register("String", "Join", (r, a, _) => $"table.concat({a[1]}, {a[0]})");
        Register("String", "Format", (r, a, _) => $"string.format({string.Join(", ", a)})");

        // === List<T> ===
        Register("List", "Add", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("List", "Insert", (r, a, _) => $"table.insert({r}, {a[0]} + 1, {a[1]})");
        Register("List", "RemoveAt", (r, a, _) => $"table.remove({r}, {a[0]} + 1)");
        Register("List", "Clear", (r, a, _) => $"table.clear({r})");
        Register("List", "Contains", (r, a, _) => $"(table.find({r}, {a[0]}) ~= nil)");
        Register("List", "IndexOf", (r, a, _) => $"((table.find({r}, {a[0]}) or 0) - 1)");
        Register("List", "Sort", (r, a, _) => a.Length > 0 ? $"table.sort({r}, {a[0]})" : $"table.sort({r})");
        Register("List", "Reverse", (r, a, _) => $"table.sort({r}, function(a, b) return false end)"); // stub
        Register("List", "ToArray", (r, a, _) => $"table.clone({r})");
        Register("List", "AddRange", (r, a, _) => $"table.move({a[0]}, 1, #{a[0]}, #{r} + 1, {r})");
        Register("List", "Remove", (r, a, _) =>
            $"(function() local __i = table.find({r}, {a[0]}); if __i then table.remove({r}, __i) end end)()");

        // === Dictionary<K,V> ===
        Register("Dictionary", "ContainsKey", (r, a, _) => $"({r}[{a[0]}] ~= nil)");
        Register("Dictionary", "Remove", (r, a, _) => $"(function() {r}[{a[0]}] = nil end)()");
        Register("Dictionary", "Clear", (r, a, _) => $"table.clear({r})");

        // === HashSet<T> ===
        Register("HashSet", "Add", (r, a, _) => $"(function() {r}[{a[0]}] = true end)()");
        Register("HashSet", "Remove", (r, a, _) => $"(function() {r}[{a[0]}] = nil end)()");
        Register("HashSet", "Contains", (r, a, _) => $"({r}[{a[0]}] == true)");

        // === Queue<T> / Stack<T> ===
        Register("Queue", "Enqueue", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("Queue", "Dequeue", (r, a, _) => $"table.remove({r}, 1)");
        Register("Queue", "Peek", (r, a, _) => $"{r}[1]");
        Register("Stack", "Push", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("Stack", "Pop", (r, a, _) => $"table.remove({r})");
        Register("Stack", "Peek", (r, a, _) => $"{r}[#{r}]");

        // === Math ===
        Register("Math", "Max", (r, a, _) => $"math.max({a[0]}, {a[1]})");
        Register("Math", "Min", (r, a, _) => $"math.min({a[0]}, {a[1]})");
        Register("Math", "Abs", (r, a, _) => $"math.abs({a[0]})");
        Register("Math", "Floor", (r, a, _) => $"math.floor({a[0]})");
        Register("Math", "Ceiling", (r, a, _) => $"math.ceil({a[0]})");
        Register("Math", "Round", (r, a, _) => $"math.round({a[0]})");
        Register("Math", "Sqrt", (r, a, _) => $"math.sqrt({a[0]})");
        Register("Math", "Pow", (r, a, _) => $"({a[0]} ^ {a[1]})");
        Register("Math", "Log", (r, a, _) => a.Length == 2 ? $"math.log({a[0]}, {a[1]})" : $"math.log({a[0]})");
        Register("Math", "Sin", (r, a, _) => $"math.sin({a[0]})");
        Register("Math", "Cos", (r, a, _) => $"math.cos({a[0]})");
        Register("Math", "Tan", (r, a, _) => $"math.tan({a[0]})");
        Register("Math", "Atan2", (r, a, _) => $"math.atan2({a[0]}, {a[1]})");
        Register("Math", "Clamp", (r, a, _) => $"math.clamp({a[0]}, {a[1]}, {a[2]})");
        Register("Math", "Sign", (r, a, _) => $"math.sign({a[0]})");
        Register("Math", "Exp", (r, a, _) => $"math.exp({a[0]})");
        Register("Math", "Truncate", (r, a, _) => $"(if {a[0]} >= 0 then math.floor({a[0]}) else math.ceil({a[0]}))");

        // === Convert ===
        Register("Convert", "ToInt32", (r, a, _) => $"math.floor(tonumber({a[0]}))");
        Register("Convert", "ToString", (r, a, _) => $"tostring({a[0]})");
        Register("Convert", "ToDouble", (r, a, _) => $"tonumber({a[0]})");
        Register("Convert", "ToBoolean", (r, a, _) => $"(not not {a[0]})");

        // === Int32 static ===
        Register("Int32", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Double", "Parse", (r, a, _) => $"tonumber({a[0]})");

        // === Object ===
        Register("Object", "ToString", (r, a, _) => $"tostring({r})");
        Register("Object", "Equals", (r, a, _) => $"({r} == {a[0]})");
        Register("Object", "GetHashCode", (r, a, _) => $"tostring({r})");
        Register("Object", "GetType", (r, a, _) => $"typeof({r})");

        // === Task ===
        Register("Task", "Run", (r, a, _) => $"task.spawn({a[0]})");
        Register("Task", "Delay", (r, a, _) => $"task.wait({a[0]} / 1000)");
        Register("Task", "FromResult", (r, a, _) => a[0]);

        // === Array ===
        Register("Array", "Copy", (r, a, _) => a.Length >= 5
            ? $"table.move({a[0]}, {a[1]} + 1, {a[1]} + {a[4]}, {a[3]} + 1, {a[2]})"
            : $"table.move({a[0]}, 1, {a[2]}, 1, {a[1]})");
        Register("Array", "Sort", (r, a, _) => $"table.sort({a[0]})");
        Register("Array", "Reverse", (r, a, _) => $"__rt.reverse({a[0]})");
    }
}
```

**Step 2: Integrate MethodMapper into EmitMemberInvocation**

At the top of `EmitMemberInvocation`, before the manual `if/else` chains:

```csharp
if (_model != null)
{
    var symbol = _model.GetSymbolInfo(originalInvocation).Symbol as IMethodSymbol;
    if (symbol != null)
    {
        var argStrings = args.Select(a => EmitExpression(a.Expression)).ToArray();
        var receiverType = GetExpressionType(memberAccess.Expression);
        var result = MethodMapper.TryRewrite(symbol, obj, argStrings, receiverType);
        if (result != null) return result;
    }
}
// ... fall through to existing manual dispatch ...
```

Note: `originalInvocation` needs to be passed through or captured from the call site.

**Step 3: Build, transpile-all, diff to verify method rewrites work**

**Step 4: Commit**

```
feat(roslyn): create MethodMapper with centralized method rewrite registry
```

---

## Phase 5: Extended Type Mapping

### Task 14: Extend TypeMapper with SemanticModel-aware mapping

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/TypeMapper.cs`
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Add ITypeSymbol-based mapping to TypeMapper**

```csharp
public static string MapTypeSymbol(ITypeSymbol type)
{
    // Primitives via SpecialType
    return type.SpecialType switch
    {
        SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
        or SpecialType.System_Byte or SpecialType.System_Single or SpecialType.System_Double
        or SpecialType.System_Decimal or SpecialType.System_Char
        or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_UInt16
        or SpecialType.System_SByte => "number",

        SpecialType.System_Boolean => "boolean",
        SpecialType.System_String => "string",
        SpecialType.System_Object => "any",
        SpecialType.System_Void => "()",
        _ => MapComplexTypeSymbol(type),
    };
}

private static string MapComplexTypeSymbol(ITypeSymbol type)
{
    // Nullable<T> → T?
    if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        && type is INamedTypeSymbol { TypeArguments.Length: 1 } nullable)
        return MapTypeSymbol(nullable.TypeArguments[0]) + "?";

    // Arrays → { T }
    if (type is IArrayTypeSymbol arr)
        return "{ " + MapTypeSymbol(arr.ElementType) + " }";

    if (type is INamedTypeSymbol named)
    {
        var name = named.Name;
        // Task<T> → T, Task → ()
        if (name == "Task" && named.TypeArguments.Length == 1)
            return MapTypeSymbol(named.TypeArguments[0]);
        if (name == "Task" && named.TypeArguments.Length == 0)
            return "()";
        // Collections
        if (name is "List" or "IList" or "IEnumerable" or "IReadOnlyList" or "ICollection"
            or "Queue" or "Stack" && named.TypeArguments.Length == 1)
            return "{ " + MapTypeSymbol(named.TypeArguments[0]) + " }";
        if (name is "Dictionary" or "IDictionary" && named.TypeArguments.Length == 2)
            return "{ [" + MapTypeSymbol(named.TypeArguments[0]) + "]: "
                   + MapTypeSymbol(named.TypeArguments[1]) + " }";
        if (name is "HashSet" && named.TypeArguments.Length == 1)
            return "{ [" + MapTypeSymbol(named.TypeArguments[0]) + "]: boolean }";
        // Func<..., R> → (...) -> R
        if (name == "Func" && named.TypeArguments.Length >= 1)
        {
            var paramTypes = named.TypeArguments.Take(named.TypeArguments.Length - 1)
                .Select(MapTypeSymbol);
            var retType = MapTypeSymbol(named.TypeArguments.Last());
            return $"({string.Join(", ", paramTypes)}) -> {retType}";
        }
        // Action<...> → (...) -> ()
        if (name == "Action")
        {
            if (named.TypeArguments.Length == 0) return "() -> ()";
            var paramTypes = named.TypeArguments.Select(MapTypeSymbol);
            return $"({string.Join(", ", paramTypes)}) -> ()";
        }
        // Predicate<T> → (T) -> boolean
        if (name == "Predicate" && named.TypeArguments.Length == 1)
            return $"({MapTypeSymbol(named.TypeArguments[0])}) -> boolean";
        // Enums → number
        if (type.TypeKind == TypeKind.Enum)
            return "number";
    }

    // User-defined types → pass through name
    return type.Name;
}
```

**Step 2: Use MapTypeSymbol throughout LuauEmitter where SemanticModel is available**

Replace calls to `MapComplexType(TypeSyntax)` with:

```csharp
private string MapTypeNode(TypeSyntax typeSyntax)
{
    if (_model != null)
    {
        var typeSymbol = _model.GetTypeInfo(typeSyntax).Type;
        if (typeSymbol != null)
            return TypeMapper.MapTypeSymbol(typeSymbol);
    }
    return MapComplexType(typeSyntax); // existing fallback
}
```

**Step 3: Build, transpile-all, verify type annotations improve**

**Step 4: Commit**

```
feat(roslyn): add ITypeSymbol-based type mapping with full generic support
```

---

## Phase 6: Remaining Syntax Coverage

### Task 15: Add interface, event, indexer, operator overload declarations

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`
- Modify: `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs`

**Step 1: Interface → type alias**

```csharp
public void EmitInterface(InterfaceDeclarationSyntax iface)
{
    var name = iface.Identifier.Text;
    EmittedTopLevelTypes.Add(name);
    // Emit as an export type with method signatures
    AppendLine($"export type {name} = {{");
    _indent++;
    foreach (var member in iface.Members)
    {
        if (member is MethodDeclarationSyntax method)
        {
            var retType = MapTypeNode(method.ReturnType);
            var parameters = method.ParameterList.Parameters
                .Select(p => $"{p.Identifier.Text}: {MapTypeNode(p.Type!)}")
                .ToList();
            parameters.Insert(0, $"self: {name}");
            AppendLine($"{method.Identifier.Text}: ({string.Join(", ", parameters)}) -> {retType},");
        }
    }
    _indent--;
    AppendLine("}");
    AppendLine("");
}
```

**Step 2: Events**

In `EmitInstanceClass`, for `EventFieldDeclarationSyntax`:
```csharp
// event EventHandler OnDamage; → self.OnDamage = __rt.Event.new()
```

**Step 3: Indexers → __index/__newindex metamethods**

**Step 4: Operator overloads → metamethods**

```csharp
// public static T operator +(T a, T b) → __add metamethod
```

**Step 5: Build, test, commit**

```
feat(roslyn): add interface, event, indexer, operator overload emission
```

---

### Task 16: Add predefined type members (.MaxValue, .Empty, etc.)

**Files:**
- Modify: `LUSharpRoslynModule/Transpiler/LuauEmitter.cs`

**Step 1: Expand PredefinedType member access**

```csharp
// In EmitMemberAccess, PredefinedTypeSyntax branch:
("int", "MaxValue") => "2147483647",
("int", "MinValue") => "-2147483648",
("double", "MaxValue") => "math.huge",
("double", "MinValue") => "-math.huge",
("double", "NaN") => "(0/0)",
("double", "PositiveInfinity") => "math.huge",
("double", "NegativeInfinity") => "-math.huge",
("double", "Epsilon") => "2.2204460492503131e-16",
("char", "MaxValue") => "65535",
("string", "Empty") => "\"\"",  // already done
```

**Step 2: Expand PredefinedType method calls**

```csharp
// In EmitMemberInvocation, PredefinedTypeSyntax branch:
("int", "Parse") => $"tonumber({args})",
("int", "TryParse") => // out param pattern
("double", "Parse") => $"tonumber({args})",
("string", "IsNullOrWhiteSpace") => ...,
("string", "Join") => ...,
("string", "Format") => ...,
("string", "Concat") => ...,
("char", "IsDigit") => $"(string.match(string.char({args}), \"%d\") ~= nil)",
("char", "IsLetter") => $"(string.match(string.char({args}), \"%a\") ~= nil)",
("char", "IsWhiteSpace") => $"(string.match(string.char({args}), \"%s\") ~= nil)",
```

**Step 3: Build, test, commit**

```
feat(roslyn): add predefined type members and static methods
```

---

## Phase 7: Runtime Module

### Task 17: Generate LUSharpRuntime.lua

**Files:**
- Create: `LUSharpRoslynModule/Transpiler/RuntimeEmitter.cs`
- Update: `LUSharpRoslynModule/out/LUSharpRuntime.lua` (generated output)

The runtime module provides helpers that can't be inlined. The existing `LUSharpRuntime.lua` from the SimpleEmitter work has most of these. RuntimeEmitter ensures it stays in sync.

**Step 1: Create RuntimeEmitter that writes the Lua file**

This is a code generator that produces `LUSharpRuntime.lua` containing:
- LINQ operations (where, select, first, any, all, count, sum, min, max, orderBy, groupBy, distinct, zip, selectMany, aggregate, take, skip, toList, toDictionary)
- Dictionary helpers (keys, values, dictCount)
- Array helpers (reverse, binarySearch, fill, resize)
- HashSet operations (unionWith, intersectWith, exceptWith)
- Event system (Event.new with Connect/Fire/Wait)
- Reflection stubs (getAttribute, getProperties — table-based)
- Async helpers (whenAll, whenAny)

**Step 2: Add `_needsRuntime` tracking to LuauEmitter**

When any runtime helper is referenced, set `_needsRuntime = true`. In `RoslynToLuau.Transpile`, after emission, if `_needsRuntime`, insert `local __rt = require(script.Parent.LUSharpRuntime)`.

**Step 3: Build, commit**

```
feat(roslyn): add RuntimeEmitter and LUSharpRuntime.lua generation
```

---

## Phase 8: Newtonsoft.Json Gate Test

### Task 18: Add transpile-project CLI command

**Files:**
- Modify: `LUSharpRoslynModule/Program.cs`
- Modify: `LUSharpRoslynModule/Transpiler/RoslynToLuau.cs`

**Step 1: Add `transpile-project <path>` command**

```csharp
case "transpile-project":
    if (args.Length < 2) { Console.Error.WriteLine("Usage: transpile-project <dir>"); return 1; }
    return TranspileProject(args[1]);
```

This command:
1. Scans directory recursively for all `.cs` files
2. Resolves NuGet references from any `.csproj` found
3. Builds `CSharpCompilation` with all sources + references
4. Transpiles each file to `out/` directory
5. Reports: total files, succeeded, failed, TODO count

**Step 2: Add external reference resolution**

```csharp
public void AddExternalReferences(IEnumerable<string> dllPaths)
{
    // Add to compilation metadata references
}
```

**Step 3: Clone Newtonsoft.Json, attempt transpilation**

```bash
git clone https://github.com/JamesNK/Newtonsoft.Json /tmp/newtonsoft
dotnet run --project LUSharpRoslynModule -- transpile-project /tmp/newtonsoft/Src/Newtonsoft.Json
```

**Step 4: Iterate on failures until zero TODOs**

This is the main iteration loop — each `--[[TODO: SyntaxKind]]` in the output reveals a gap to fill. Fix each one in LuauEmitter and re-run.

**Step 5: Commit**

```
feat(roslyn): add transpile-project command, pass Newtonsoft.Json gate
```

---

## Phase 9: Cleanup and Verification

### Task 19: Final verification pass

**Step 1: Run all test suites**

```bash
dotnet run --project LUSharpRoslynModule -- reference self-emit      # 12/12
dotnet run --project LUSharpRoslynModule -- reference transpiler      # 13/13
dotnet run --project LUSharpRoslynModule -- transpile-all             # All RoslynSource files
dotnet run --project LUSharpRoslynModule -- transpile-project <newtonsoft>  # Gate test
```

**Step 2: Grep for remaining TODOs in output**

```bash
grep -r "TODO" LUSharpRoslynModule/out/ | wc -l
```

Target: zero.

**Step 3: Commit and tag**

```
feat(roslyn): complete Roslyn SemanticModel integration — full C# coverage
```

---

## Summary

| Phase | Tasks | What it delivers |
|---|---|---|
| 1: Foundation | 1-5 | SemanticModel wired in, heuristics replaced |
| 2: Expressions | 6-9 | Interpolation, is/as, await, tuples |
| 3: Statements | 10-12 | using, lock, yield, object initializers, static members |
| 4: MethodMapper | 13 | Centralized method rewrites (100+ methods) |
| 5: TypeMapper | 14 | ITypeSymbol-based mapping with generics |
| 6: Declarations | 15-16 | Interfaces, events, indexers, operators, predefined members |
| 7: Runtime | 17 | LUSharpRuntime.lua with LINQ, events, reflection stubs |
| 8: Gate Test | 18 | transpile-project command, Newtonsoft.Json passes |
| 9: Cleanup | 19 | All tests green, zero TODOs |
