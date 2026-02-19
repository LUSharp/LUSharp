# Building & Deploying

This guide covers the LUSharp build process, configuration options, and the workflow for getting your code into Roblox Studio.

## Build Command

```bash
lusharp build
```

This runs the full transpilation pipeline:

1. **Parse** — Reads all `.cs` files from `src/` using Roslyn
2. **Validate** — Checks for `GameEntry()` entry points and syntax errors
3. **Transform** — Converts C# syntax to Lua IR through 7 passes
4. **Emit** — Renders IR to Luau source files in `out/`

### Watch Mode

Rebuild automatically when source files change:

```bash
lusharp build --watch
```

This monitors `src/` for changes and re-transpiles only affected files. Combined with `rojo serve`, you get a live development loop.

## Configuration

### `lusharp.json`

```json
{
  "name": "MyGame",
  "version": "1.0.0",
  "packages": [],
  "build": {
    "src": "./src",
    "out": "./out"
  }
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `name` | (required) | Project name |
| `version` | `"1.0.0"` | Project version |
| `packages` | `[]` | Community packages to include |
| `build.src` | `"./src"` | Source directory |
| `build.out` | `"./out"` | Output directory |

## Build Pipeline

The transpilation pipeline processes files through these stages:

```
C# Source → Frontend → Transform → Backend → Luau Output
```

### Frontend

- Scans `src/client/`, `src/server/`, `src/shared/`
- Parses each `.cs` file with Roslyn
- Validates entry point structure (`GameEntry()` for `RobloxScript`)
- Reports syntax errors with file, line, and column

### Transform (7 passes)

| Pass | Purpose |
|------|---------|
| **SymbolCollector** | Gathers all type, method, and field symbols |
| **TypeResolver** | Maps C# types to Lua equivalents |
| **ImportResolver** | Resolves cross-file references to `require()` calls |
| **MethodBodyLowerer** | Converts method bodies to Lua IR |
| **ControlFlowLowerer** | Converts `if`/`for`/`while`/`switch` to Lua control flow |
| **EventBinder** | Wires Roblox event connections (`.Touched +=` → `:Connect()`) |
| **Optimizer** | Constant folding and dead branch elimination |

### Backend

- Renders IR nodes to Luau text via `LuaWriter`
- Writes output files preserving the directory structure

## Rojo Workflow

### Initial Setup

```bash
lusharp new MyGame     # generates default.project.json
cd MyGame
```

### Development Loop

```bash
# Terminal 1: Watch for changes and rebuild
lusharp build --watch

# Terminal 2: Serve to Roblox Studio
rojo serve
```

In Roblox Studio, connect via the Rojo plugin. Changes sync automatically.

### Output Mapping

| Source | Output | Roblox Location |
|--------|--------|-----------------|
| `src/server/*.cs` | `out/server/*.luau` | `ServerScriptService` |
| `src/client/*.cs` | `out/client/*.luau` | `StarterPlayer/StarterPlayerScripts` |
| `src/shared/*.cs` | `out/shared/*.luau` | `ReplicatedStorage/Shared` |

## Error Handling

LUSharp reports errors with file path, line number, and description:

```
Error: src/client/ClientMain.cs:15 — Missing GameEntry() method in class ClientMain
Error: src/server/ServerMain.cs:8 — Unknown type 'Foo' (did you mean 'Part'?)
```

!!! tip
    Run `dotnet build` first to catch C# compile errors. LUSharp's error reporting focuses on transpilation-specific issues.

## Deployment Checklist

1. Ensure `dotnet build` passes (catches type errors)
2. Run `lusharp build` (transpiles to Luau)
3. Start `rojo serve` and connect from Studio
4. Test in Studio — check the Output window for runtime errors
5. Publish your Roblox place when ready
