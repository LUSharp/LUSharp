# Architecture

## Overview

LUSharp is a C# to Luau transpiler for Roblox. It consists of five projects:

| Project | Purpose |
|---------|---------|
| **LUSharp** | CLI tool (`lusharp new`, `lusharp build`, `lusharp help`) |
| **LUSharpTranspiler** | Core transpiler engine (C# to Luau) |
| **LUSharpAPI** | C# bindings for the Roblox API (IntelliSense only) |
| **LUSharpApiGenerator** | Generates LUSharpAPI stubs from the Roblox API dump |
| **LUSharpTests** | Unit tests |

## Dependency Chain

```
LUSharpAPI              (standalone — Roblox API bindings)
LUSharpApiGenerator     (standalone — generates LUSharpAPI stubs)
LUSharp                 (CLI, references LUSharpTranspiler)
  └── LUSharpTranspiler (core transpiler engine)
LUSharpTests            (references LUSharp + LUSharpTranspiler)
```

## Transpilation Pipeline

```
C# source files (src/client/, src/server/, src/shared/)
        │
        ▼
   ┌─────────┐    Roslyn CSharpSyntaxTree.ParseText()
   │ Frontend │    Scans files, validates entry structure
   └────┬────┘    CodeWalker.cs — CSharpSyntaxWalker
        │
        ▼
  ┌───────────┐   IR tree of ILuaStatement / ILuaExpression nodes
  │ Transform │   7 passes: SymbolCollector → TypeResolver →
  └─────┬─────┘   ImportResolver → MethodBodyLowerer →
        │          ControlFlowLowerer → EventBinder → Optimizer
        ▼
   ┌─────────┐    ILuaRenderable.Render(LuaWriter)
   │ Backend │    Fluent builder pattern → indented Luau output
   └────┬────┘
        │
        ▼
   Luau source files (out/client/, out/server/, out/shared/)
```

### Frontend

The frontend is responsible for parsing C# source files and validating their structure.

- **Transpiler.cs** — Entry point: scans directories, validates `GameEntry()` structure, drives the pipeline
- **CodeWalker.cs** — Roslyn `CSharpSyntaxWalker` that visits syntax nodes and routes them to builders

### Transform Layer

The transform layer converts Roslyn syntax into a Lua IR through sequential passes:

| # | Pass | Purpose |
|---|------|---------|
| 1 | **SymbolCollector** | Gathers all type, method, and field symbols from the parsed C# |
| 2 | **TypeResolver** | Maps C# types to Lua equivalents (`int` → `number`, `List<T>` → table, etc.) |
| 3 | **ImportResolver** | Resolves cross-file `using` references into `require()` calls |
| 4 | **MethodBodyLowerer** | Converts method bodies to Lua IR statements and expressions |
| 5 | **ControlFlowLowerer** | Converts `if`/`for`/`while`/`switch` to Lua control flow |
| 6 | **EventBinder** | Wires Roblox event connections (`+=` → `:Connect()`) |
| 7 | **Optimizer** | Constant folding and dead branch elimination |

### Backend

The backend renders the Lua IR into Luau source text.

- **ClassBuilder.cs** — Core C# to Lua class conversion
- **AST Builders** — Fluent interface for constructing Lua AST nodes
- **LuaWriter.cs** — Indented text writer that renders `ILuaRenderable` nodes to formatted Luau

## IR Design

The intermediate representation is a tree of `ILuaStatement` and `ILuaExpression` nodes rooted in a `LuaModule`:

```
LuaModule
├── RequireStatements[]
├── ClassDeclarations[]
│   ├── Fields[]
│   ├── Constructor
│   ├── Methods[]
│   │   ├── Parameters[]
│   │   └── Body: ILuaStatement[]
│   ├── Properties[]
│   └── StaticMembers[]
└── EntryPoint (GameEntry call)
```

All nodes implement `ILuaRenderable` with a `Render(LuaWriter writer)` method for recursive output.

## C# to Luau Mappings

| C# Concept | Luau Output |
|-------------|-------------|
| Class with instance fields | `local T = {}` table with `.new()` constructor |
| Static members | Class-level table entries |
| Properties | `get_Prop()` / `set_Prop()` methods |
| `List<T>` | `{1, 2, 3}` numeric table |
| `Dictionary<K,V>` | `{key = val}` table |
| `Console.WriteLine()` | `print()` |
| String interpolation `$"..."` | Backtick string `` `...` `` |
| `string` / `int` / `float` / `bool` | `string` / `number` / `number` / `boolean` |
| `void` | `nil` |
| `object` | `table` |

## Rojo Integration

`lusharp new` generates a `default.project.json` that maps output directories into Roblox:

| Output Folder | Roblox Location |
|---------------|-----------------|
| `out/server/` | `ServerScriptService` |
| `out/client/` | `StarterPlayer/StarterPlayerScripts` |
| `out/shared/` | `ReplicatedStorage/Shared` |
| `out/runtime/` | `ReplicatedStorage/Runtime` |

## LUSharpAPI

Provides C# stub types for the Roblox API so users get IntelliSense in their IDE. These types are never executed — they exist only for compile-time type checking.

### Structure

| Directory | Contents |
|-----------|----------|
| `Runtime/Internal/` | Base script types (`RobloxScript`, `LocalScript`, `ModuleScript`) |
| `Runtime/STL/Generated/Classes/` | Auto-generated Roblox instance stubs (Part, Model, etc.) |
| `Runtime/STL/Generated/Enums/` | Auto-generated Roblox enum stubs |
| `Runtime/STL/Types/` | Value types (Vector3, CFrame, Color3, etc.) |
| `Runtime/STL/Services/` | Service types (Players, Workspace, etc.) |

### API Generator

`LUSharpApiGenerator` reads the Roblox API JSON dump and generates C# stub classes with correct inheritance, properties, methods, and events. Run it to regenerate stubs when the Roblox API changes.

## Key Files

| File | Purpose |
|------|---------|
| `LUSharpTranspiler/Frontend/Transpiler.cs` | Pipeline entry point |
| `LUSharpTranspiler/Frontend/CodeWalker.cs` | Roslyn syntax walker |
| `LUSharpTranspiler/Transpiler/Builder/ClassBuilder.cs` | Core C# to Lua conversion |
| `LUSharpTranspiler/Transform/TransformPipeline.cs` | IR transform pass orchestration |
| `LUSharp/Program.cs` | CLI entry point |
| `LUSharp/Project/ProjectScaffolder.cs` | `lusharp new` scaffolding |
