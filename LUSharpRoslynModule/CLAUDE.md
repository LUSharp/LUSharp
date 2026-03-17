# LUSharpRoslynModule — Roslyn-to-Luau Transpiler

## Mission

Enable any C# developer to write code for Roblox Studio using familiar C# patterns. The transpiler should handle real-world C# codebases (proven by transpiling Newtonsoft.Json — 241 files, 11/11 runtime tests, output matches HttpService:JSONEncode).

## Architecture

```
C# source files
    ↓ Roslyn CSharpSyntaxTree.ParseText()
RoslynToLuau.PreScan()     ← type-to-module map, overload disambiguation, Tarjan SCC
    ↓
RoslynToLuau.Transpile()   ← per-file: parse → semantic model → LuauEmitter
    ↓
LuauEmitter                ← expression/statement/declaration emission with --!strict
    ↓
MethodMapper               ← C# BCL → Luau runtime rewrites
    ↓
LUSharpRuntime.lua         ← runtime helpers (collections, strings, type checks, etc.)
    ↓
Luau modules with require(script.Parent.X) preamble + deferred lazy proxies
```

### Key Files

| File | Purpose | Lines |
|------|---------|-------|
| `Transpiler/RoslynToLuau.cs` | Orchestrator: PreScan, Tarjan SCC, overload maps, compilation | ~900 |
| `Transpiler/LuauEmitter.cs` | Main emitter: all C# → Luau conversion logic | ~7500 |
| `Transpiler/MethodMapper.cs` | BCL method rewrites (String, List, Dict, Math, etc.) | ~400 |
| `out/LUSharpRuntime.lua` | Luau runtime: helpers that can't be inlined | ~1000 |
| `Program.cs` | CLI: transpile, transpile-project, reference tests | ~450 |

### Self-Hosting Transpiler (RoslynSource/)

A simplified C# → Luau transpiler written in self-hosting-safe C# (no LINQ, no generics, no string interpolation). 13 source files that transpile themselves with **13/13 perfect match** between C# and Luau execution.

| File | Purpose |
|------|---------|
| `SimpleTokenizer.cs` | Lexer |
| `SimpleParser.cs` | Recursive descent parser |
| `SimpleEmitter.cs` | C# AST → Luau emitter |
| `SimpleTranspiler.cs` | Multi-file orchestrator with require() resolution |
| `SyntaxKind.cs` | 571-member token/node kind enum |
| `SyntaxFacts.cs` | Keyword/operator classification |
| `SyntaxNode.cs` | Base AST node types |
| `DeclarationNodes.cs` | Class/struct/enum/method declaration nodes |
| `ExpressionNodes.cs` | Expression AST nodes |
| `StatementNodes.cs` | Statement AST nodes |
| `SyntaxWalker.cs` | Visitor pattern base |
| `SlidingTextWindow.cs` | Text buffer with lookahead |
| `SyntaxToken.cs` | Token + TokenInfo structs |

Self-hosting constraints: no LINQ, no `var`, no `is`/`as` patterns, no `?.`/`??`, no switch expressions, no string interpolation. All arrays are fixed-size with explicit limits.

## Build & Test

```bash
# Build
dotnet build LUSharpRoslynModule/LUSharpRoslynModule.csproj

# Reference tests (must pass before any commit)
dotnet run --project LUSharpRoslynModule -- reference self-emit    # 12/12
dotnet run --project LUSharpRoslynModule -- reference transpiler   # 13/13

# Transpile a project
dotnet run --project LUSharpRoslynModule -- transpile-project <dir>

# Self-hosting bundle test (Luau CLI)
python tools/gen_selfhost_bundle.py && ./luau.exe selfhost_bundle.lua  # 13/13 PERFECT MATCH

# Newtonsoft.Json validation
dotnet run --project LUSharpRoslynModule -- transpile-project "C:/Users/table/AppData/Local/Temp/newtonsoft/Src/Newtonsoft.Json"
# Then deploy to Z:\transcripts\scripts\newtonsoft\ and test in Studio (11/11)
```

## Current State (Layer 97)

### What Works
- **Classes**: instance/static fields, constructors (including base chaining), methods, properties (auto + expression-bodied), nested types
- **Structs**: same as classes (table-based)
- **Enums**: numeric (freeze'd table) and string-valued (keyof<typeof>)
- **Inheritance**: setmetatable chain, virtual dispatch via metatable __index, `__className` for isinstance checks
- **Overloads**: GlobalOverloadMap (4-tuple key) + FullSignatureOverloadMap for disambiguation
- **Generics**: type parameters erased, typeof(T) → nil for generic params
- **Collections**: List → table, Dictionary → table, Array → table, with runtime helpers
- **String operations**: mapped via MethodMapper (IndexOf, Replace, Split, Trim, Contains, etc.)
- **Math**: full System.Math mapping to Luau math library
- **Control flow**: if/else, for, foreach, while, do-while, switch (as if/elseif), try/catch/finally (pcall)
- **Expressions**: ternary, null-coalescing (?? → or), null-conditional (?. → if-nil check), is/as type checks
- **LINQ-style**: select, where, any, all, first, count via runtime helpers
- **Integer division**: C# int/int → math.floor(a/b)
- **Pre/post increment/decrement**: emitted as separate statements
- **0→1 index conversion**: automatic for array/list access
- **String interpolation**: $"..." → backtick strings
- **Exception handling**: try/catch → pcall, throw → error(), Message field preserved
- **Type checks**: `is Type` → `__rt.isinstance(obj, "Type")` with metatable chain walk
- **Cross-module**: require() preamble with deferred lazy proxies for cycle breaking
- **Static constructors**: deferred to Phase 7.6 (after all methods defined)
- **Deferred statics**: task.defer for cycle-breaking static field initialization

### What's Missing / Incomplete
- **async/await**: not yet implemented (→ coroutine/task.spawn)
- **Interfaces**: type-erased, no runtime dispatch
- **Events/delegates**: partial (RBXScriptSignal mapped, custom events not fully supported)
- **Unsafe/pointers**: not applicable to Luau
- **ref/out parameters**: partial (out via multiple returns)
- **Span<T>/Memory<T>**: not applicable
- **Dynamic/ExpandoObject**: not applicable
- **Full LINQ**: only basic operators (Select, Where, Any, All, First, Count). Missing: GroupBy, Join, OrderBy, Aggregate, SelectMany, Distinct, Union, Intersect, Except, Zip, Take, Skip, ToDictionary, ToLookup
- **System.Text.RegularExpressions**: no Luau equivalent (string.match is limited)
- **System.IO**: not applicable in Roblox (except HttpService for network)
- **System.Threading**: partial (Task.Delay → task.wait, basic async patterns)
- **Attribute-based features**: serialization attributes partially handled
- **Nullable reference types**: annotations erased, null checks preserved
- **Pattern matching**: basic `is Type` works, complex patterns (property/positional) not yet
- **Records**: not yet
- **with expressions**: not yet
- **Tuples**: partial (ValueTuple deconstructed)
- **Extension methods**: not yet
- **Default interface members**: not yet

## Goal: Complete C# for Roblox

Priority runtime features to implement (in order):

### Tier 1 — Essential for Real-World Code
1. **Full LINQ** — GroupBy, OrderBy, Join, SelectMany, Distinct, Take, Skip, Aggregate, ToDictionary
2. **System.Text.StringBuilder** — currently maps to string concat, needs proper mutable builder for performance
3. **Enum utilities** — Enum.Parse, Enum.TryParse, Enum.GetValues, Enum.GetName
4. **Convert class** — Convert.ToInt32, Convert.ToString, Convert.ToDouble, etc.
5. **System.Text.Encoding** — UTF8 encode/decode for HttpService payloads
6. **TimeSpan/DateTime** — basic arithmetic, formatting, parsing

### Tier 2 — Quality of Life
7. **async/await** — map to coroutine/task patterns
8. **Extension methods** — emit as static calls with first-param dispatch
9. **Pattern matching** — property patterns, switch expressions with patterns
10. **Records** — auto-generate equality, ToString, deconstruct
11. **Nullable<T>** — proper HasValue/Value/GetValueOrDefault
12. **IDisposable/using** — cleanup patterns

### Tier 3 — Advanced
13. **Full Roblox API stubs** — complete LUSharpAPI coverage
14. **Source generators** — compile-time code generation for Roblox-specific patterns
15. **Hot reload** — watch mode with incremental transpilation
16. **Package system** — community Roblox API extensions

## Conventions

### Commit Messages
Format: `feat(roslyn-module): description (LXX)` where XX is the layer number. Each layer represents a batch of related fixes.

### Testing Protocol
1. `dotnet build` — must compile
2. `reference self-emit` — 12/12
3. `reference transpiler` — 13/13
4. Transpile Newtonsoft.Json — 241/241
5. Deploy and test in Studio — 11/11
6. Self-host bundle — 13/13 PERFECT MATCH

### Emitter Patterns
- Use semantic model (`_model`) when available for type-aware decisions
- `EscapeIdentifier()` for all user-facing identifiers (Luau reserved words)
- `EmitExpression()` returns string; `EmitStatement()` appends via `AppendLine()`
- Properties: auto → direct field access; expression-bodied/abstract → `get_/set_` methods
- Overloads: disambiguated as `MethodName_FirstParamType` or `MethodName_FirstParamType__N`
- Base calls: `ParentClass.Method(self, args)` (static dispatch, not virtual)
- Instance calls: colon syntax `obj:Method(args)` via `IsInstanceCallTarget` detection

### Runtime Helpers (__rt)
Add to `out/LUSharpRuntime.lua`. Keep helpers minimal — prefer inline emission when possible. Only use runtime helpers for operations that need loops, closures, or multi-step logic that would be unreadable inline.

### MethodMapper Patterns
Register with: `Register("TypeName", "MethodName", (receiver, args, model) => "luau_expression")`
- `receiver` is the emitted object expression
- `args` is the emitted argument array
- `model` is the semantic model (nullable)
- Return the complete Luau expression string

### Self-Hosting Constraints
When modifying `RoslynSource/*.cs` files:
- No LINQ, no `var`, no `is`/`as` patterns, no `?.`/`??`
- No switch expressions (`x switch { ... }`) — use if-return chains
- No string interpolation ($"...") — use `+` concatenation
- Fixed-size arrays (currently 512 for statements, 1024 for enum members)
- No method overloads (combine into single method or rename)
- Use intermediate variables for chained `ElementAccess.Property` (SimpleEmitter drops the property)
- `switch` fall-through with >16 cases may be truncated — use if-chains instead
