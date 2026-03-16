# Design: Roslyn SemanticModel + Full C# Language Coverage

**Date:** 2026-03-04
**Status:** Approved
**Goal:** Evolve LuauEmitter.cs to use Roslyn's real parser + SemanticModel for full .NET 10 C# syntax support, targeting Newtonsoft.Json transpilation as the gate test.

---

## Context

LUSharpRoslynModule has two emitter paths:
- **LuauEmitter.cs** (~2947 lines) — walks real Roslyn `*Syntax` nodes, orchestrated by `RoslynToLuau.cs`. No SemanticModel; all type resolution is heuristic.
- **SimpleEmitter.cs** (~3300+ Luau lines) — self-hosted emitter using custom `SimpleParser`. Has more method/type mappings but uses int-based kind dispatch.

Both are incomplete. LuauEmitter handles more Roslyn node types but lacks method mappings. SimpleEmitter has rich mappings but a fragile hand-rolled parser.

### Decision

**Approach A: Evolve LuauEmitter.cs.** Add `SemanticModel` for type resolution, port method mappings from SimpleEmitter, and extend to cover all C# syntax. SimpleEmitter stays frozen as the self-emit test path.

---

## Architecture

### Core Change

```
Current:  CSharpSyntaxTree.ParseText() → SyntaxTree → LuauEmitter (heuristic types)

Proposed: CSharpCompilation.Create(allTrees, refs) → SemanticModel per tree
                                                          ↓
          RoslynToLuau orchestrates → LuauEmitter(tree, semanticModel) → Luau
```

`RoslynToLuau.PreScan` builds a `CSharpCompilation` from all source files + BCL metadata references. Each file's `SemanticModel` is passed to `LuauEmitter`.

### File Organization

| File | Role |
|---|---|
| `Transpiler/RoslynToLuau.cs` | Orchestrator — builds `CSharpCompilation`, passes `SemanticModel` to emitter |
| `Transpiler/LuauEmitter.cs` | Core emitter — walks Roslyn AST with `SemanticModel` for type queries |
| `Transpiler/TypeMapper.cs` | Extended type mapping — C# types → Luau types, including generics |
| `Transpiler/MethodMapper.cs` | **New** — centralized C# method → Luau function mapping table |
| `Transpiler/RuntimeEmitter.cs` | **New** — generates `LUSharpRuntime.lua` |
| `RoslynSource/*` | Frozen — self-hosted pipeline for self-emit test only |

---

## SemanticModel Integration

### Compilation Setup

```csharp
CSharpCompilation.Create("LUSharpTranspilation",
    syntaxTrees: allTrees,
    references: [ mscorlib, System.Runtime, System.Collections,
                  System.Linq, System.Threading.Tasks, ...BCL refs ],
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
)
```

For external libraries (Newtonsoft.Json), add their NuGet package references to the compilation's metadata references.

### LuauEmitter Signature

```csharp
public class LuauEmitter
{
    private readonly SemanticModel _model;
    public LuauEmitter(SemanticModel model) { _model = model; }
}
```

### Heuristic Replacements

| Current Heuristic | SemanticModel Replacement |
|---|---|
| `_forceStringConcat` + `IsStringConcatenation()` | `_model.GetTypeInfo(expr).Type.SpecialType == SpecialType.System_String` |
| `_instanceFieldTypes` dictionary | `_model.GetSymbolInfo(expr).Symbol` → `IFieldSymbol.Type` |
| `_currentMethodParamTypes` dictionary | `_model.GetDeclaredSymbol(param)` → `IParameterSymbol.Type` |
| `IsLikelyEnumOrExternalType()` PascalCase | `_model.GetTypeInfo(expr).Type.TypeKind == TypeKind.Enum` |
| `IsLikelyStringAccess()` name whitelist | `_model.GetTypeInfo(receiver).Type.SpecialType == SpecialType.System_String` |
| `_overloadMap` first-param suffix | `_model.GetSymbolInfo(invocation).Symbol` → `IMethodSymbol` exact |
| `ReferencedModules` name tracking | `_model.GetSymbolInfo(expr).Symbol.ContainingType` → real cross-file refs |

---

## Full C# Language Coverage

### Tier 1 — Required for Newtonsoft.Json

| Feature | Implementation |
|---|---|
| Interfaces | Type alias or skip (no runtime representation) |
| Generics (type params) | Preserve in annotations, erase at runtime |
| `is`/`as` expressions | `type()` checks / `IsA` for instances |
| Interpolated strings | Backtick strings with expression re-emission |
| `async`/`await` | Strip async, await → direct call or `task.wait` |
| Object initializers | Field assignments after `.new()` |
| LINQ methods | Runtime module (`__rt.where`, `__rt.select`, etc.) |
| Multiple constructors | Overload disambiguation |
| Events | Runtime event helper |
| Attributes | Metadata tables for reflection-like access |
| `typeof(T)` | `"TypeName"` string literal |
| Pattern matching (full) | Type patterns, when guards, and/or/not |
| `using` statements | `do...end` block |
| Static class fields/properties | `ClassName.Field = value` |
| Indexers | `__index`/`__newindex` metamethods |
| `ref`/`out` params | Out → multi-return, ref → table wrapper |

### Tier 2 — Full Language

| Feature | Implementation |
|---|---|
| Records | Class with equality + `with` as clone + override |
| Tuples | Multi-return / table |
| Range/Index | `table.move` slice, `^1` → `#arr` |
| `goto`/labels | Restructure to loops or emit error |
| `checked`/`unchecked` | Drop (Luau doubles) |
| `fixed`/`stackalloc`/`unsafe` | Emit error |
| Operator overloads | `__add`/`__sub`/`__eq` metamethods |
| Extension methods | Module functions, rewrite call sites |
| Partial classes | Merge in PreScan |
| `yield return` | Coroutine iterator |
| `dynamic` | `any` passthrough |

### Tier 3 — .NET 10 / C# 14

| Feature | Implementation |
|---|---|
| Extension members | Module functions |
| `field` keyword | `self._fieldName` |
| Primary constructors | Desugar to constructor + fields |
| Collection expressions | Table literals |

---

## Method Mapping (MethodMapper.cs)

Centralized registry replacing scattered `if/else` chains. With `SemanticModel`, resolves `IMethodSymbol` and looks up `(ContainingType, MethodName)`:

```csharp
MethodMapper.Register("List`1", "Add", (receiver, args) =>
    $"table.insert({receiver}, {args[0]})");
MethodMapper.Register("String", "Contains", (receiver, args) =>
    $"(string.find({receiver}, {args[0]}, 1, true) ~= nil)");
```

All mappings from SimpleEmitter (collections, string ops, Math, async, DateTime, etc.) get ported here.

---

## Runtime Module (LUSharpRuntime.lua)

Helpers that can't be inlined:
- LINQ operations (where, select, first, orderBy, groupBy, etc.)
- Dictionary helpers (keys, values, dictCount)
- Event system
- Reflection stubs (attribute metadata, type introspection)
- Async helpers (whenAll, whenAny, CancellationToken)

Emitter tracks `_needsRuntime` and auto-inserts `require()`.

---

## Newtonsoft.Json Strategy

Key patterns used by Newtonsoft:
- **Reflection** → runtime stubs using Luau table introspection
- **Attributes** → metadata tables on type definitions
- **`dynamic`** → `any` passthrough
- **LINQ** → eager runtime helpers
- **Generics** → type erasure with runtime type tags

---

## Testing Strategy

1. **Unit tests** — per-feature emission validation
2. **Integration tests** — multi-file C# project → Luau output
3. **Newtonsoft.Json gate** — full transpilation with zero `--[[TODO]]` markers
4. **Self-emit test** — SimpleEmitter pipeline stays as regression (frozen)

### Success Criteria

- Transpile all Newtonsoft.Json `.cs` files to Luau
- Zero unhandled `SyntaxKind` in output
- Core `JsonConvert.SerializeObject` / `DeserializeObject` work in Roblox Studio
