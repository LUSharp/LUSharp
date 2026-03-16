# Roslyn-to-Luau Transpilation Design

**Date:** 2026-03-03
**Status:** Approved

## Goal

Transpile Microsoft's Roslyn C# compiler source code to Luau, creating a native C# parser (and eventually semantic model) that runs inside Roblox Studio. This is the ultimate proof that LUSharp can transpile real-world C# codebases, and it provides the plugin with a full-fidelity C# frontend.

## Architecture

### LUSharpRoslynModule (Standalone C# Project)

Self-contained project with its own C#-to-Luau transpiler. No dependencies on the existing LUSharp plugin or CLI transpiler.

```
LUSharpRoslynModule/
├── LUSharpRoslynModule.csproj    ← References Microsoft.CodeAnalysis.CSharp NuGet
├── Program.cs                     ← CLI entry: reads C# source files, transpiles → Luau
├── Transpiler/
│   ├── RoslynToLuau.cs           ← Orchestrator: Roslyn parse → walk → emit
│   ├── LuauEmitter.cs            ← Builds Luau source strings from Roslyn syntax nodes
│   └── TypeMapper.cs             ← C# type → Luau type mapping
├── RoslynSource/                  ← Decompiled Roslyn source files (transpilation input)
│   ├── SyntaxKind.cs
│   ├── SyntaxToken.cs
│   └── ...                        ← Added incrementally per layer
├── Reference/                     ← C# test harnesses producing reference output
│   └── TokenizerTest.cs
└── out/                           ← Generated Luau output
    └── *.lua
```

**Pipeline:**

```
Decompiled Roslyn .cs file
    ↓ CSharpSyntaxTree.ParseText()
Roslyn SyntaxTree (full fidelity)
    ↓ RoslynToLuau.cs walks the tree
    ↓ (SemanticModel for type resolution when needed)
LuauEmitter.cs emits Luau source
    ↓
Luau module file in out/
```

### TestPlugin (Roblox Studio Plugin)

Isolated validation plugin — loads transpiled Roslyn Luau modules and runs them.

```
TestPlugin/
├── src/
│   ├── init.server.lua           ← Plugin entry, loads modules, runs tests
│   ├── runtime/                   ← .NET BCL equivalents in Luau
│   │   ├── init.lua              ← Exports all runtime types
│   │   ├── System.lua            ← Core types (Char, String, Math, Convert)
│   │   ├── Collections.lua       ← List, Dictionary, HashSet, ImmutableArray
│   │   ├── Text.lua              ← StringBuilder, Encoding
│   │   └── Diagnostics.lua       ← Debug utilities
│   └── modules/                   ← Transpiled Luau modules (copied from out/)
│       ├── SyntaxKind.lua
│       └── ...
└── TestPlugin.rbxmx              ← Plugin artifact
```

### Isolation Policy

No changes to the existing LUSharp plugin or CLI transpiler. LUSharpRoslynModule is fully standalone. Integration into the main plugin only happens after the transpiled Roslyn modules are proven stable.

## Roslyn Source Strategy

- **Source:** NuGet package `Microsoft.CodeAnalysis.CSharp` + decompilation (ILSpy or similar)
- **Storage:** Decompiled .cs files copied into `RoslynSource/` directory
- **Sync:** Manual — cherry-pick files per layer, pin to a specific Roslyn version

## Type Mapping

| C# (Roslyn source) | Luau output |
|---|---|
| `int`, `uint`, `long` | `number` |
| `bool` | `boolean` |
| `string` | `string` |
| `char` | `number` (byte value) |
| `enum` | Table + `export type` |
| `struct` | Table with `.new()` |
| `ReadOnlySpan<char>` | `string` (runtime shim) |
| `string[]` | `{string}` |
| `Dictionary<K,V>` | `{[K]: V}` |
| `List<T>` | `{T}` |
| `Nullable<T>` / `T?` | `T?` |
| `ImmutableArray<T>` | `{T}` (runtime with read-only semantics) |

## Runtime Library

Transpiled code `require()`s a runtime library providing .NET BCL equivalents. The runtime grows incrementally — only add types as each layer needs them.

**Emitted require pattern:**
```lua
local Runtime = require(script.Parent.Parent.runtime)
local System = Runtime.System
local Collections = Runtime.Collections
```

**Runtime growth per layer:**

| Layer | Runtime additions |
|---|---|
| 1: SyntaxKind | `bit32` ops for `[Flags]` enums |
| 2: CharacterInfo | `System.Char`, `System.String` |
| 3: Lexer | `Collections.List`, `Text.StringBuilder`, `Span` shim |
| 4: Parser | `Collections.ImmutableArray`, `ObjectPool` |
| 5: Semantic model | Major BCL surface (LINQ, nullable analysis) |

## Bottom-Up Progression

### Layer 1: SyntaxKind Enum + Token Structs

**Roslyn files:** `SyntaxKind.cs`, `SyntaxToken.cs`, related enums/structs
**C# features required:** Enums (int-valued, `[Flags]`), structs, constants
**Validation:** Enum values match between C# and Luau exactly

### Layer 2: Character Utilities

**Roslyn files:** `CharacterInfo.cs`, `SlidingTextWindow.cs`
**C# features required:** Static methods, char operations, string slicing
**Validation:** Boolean results of character classification match

### Layer 3: Lexer

**Roslyn files:** `Lexer.cs`, `LexerCache.cs`
**C# features required:** Switch statements, instance state, collections, exceptions
**Validation:** Token streams (kind, text, position) match for identical C# input

### Layer 4: Parser

**Roslyn files:** `SyntaxParser.cs`, `LanguageParser*.cs`
**C# features required:** Generics, interfaces, inheritance, immutable types
**Validation:** Syntax tree structure matches

### Layer 5: Semantic Model

**Roslyn files:** `Binder*.cs`, `Symbol*.cs`
**C# features required:** Complex generics, LINQ, nullable analysis
**Validation:** Symbol resolution and type checking match

## Validation Strategy

### C# Reference (LUSharpRoslynModule)

```csharp
// dotnet run --project LUSharpRoslynModule -- tokenize "int x = 5;"
// Output:
// IntKeyword|int|0
// IdentifierToken|x|4
// EqualsToken|=|6
// NumericLiteralToken|5|8
// SemicolonToken|;|9
```

### Luau Test (TestPlugin)

```lua
-- Runs transpiled Roslyn lexer, prints same structured format
local tokens = Lexer.Tokenize("int x = 5;")
for _, t in tokens do
    print(t.Kind .. "|" .. t.Text .. "|" .. t.Start)
end
```

### Comparison

Manual diff initially — run both, compare output. Automated diffing added later as test suite grows.

## Key Decisions

1. **Standalone project** — No coupling to existing plugin or CLI transpiler
2. **Own transpiler** — LUSharpRoslynModule uses Roslyn to parse Roslyn, emits Luau
3. **Runtime library** — BCL equivalents in Luau, grown incrementally per layer
4. **NuGet + decompile** — Source from decompiling Roslyn NuGet packages
5. **Bottom-up** — SyntaxKind → CharacterInfo → Lexer → Parser → Semantic model
6. **TestPlugin isolation** — Separate from main LUSharp plugin until proven stable
