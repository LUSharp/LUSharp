# LUSharp - [Discord](https://discord.gg/c85RP2dzHY)

**Write Roblox games in C# — transpiled to Luau.**

LUSharp is a C# to Luau transpiler for Roblox, similar to [roblox-ts](https://roblox-ts.com) but for C# developers. Write your game logic in modern C# with full IntelliSense, then transpile it to Luau that runs natively in Roblox.

## Roblox Studio Plugin

LUSharp includes a Roblox Studio plugin with a built-in C# editor, real-time IntelliSense, syntax highlighting, and a project view. Write C# directly inside Studio and compile to Luau with one click.

### Plugin Features

- **C# Editor** — Syntax-highlighted editor with auto-indent, word delete, and tab completion
- **IntelliSense** — Real-time completions, hover info, and diagnostics with C# error codes (CS0176, CS0120, CS1061)
- **`--!strict` Output** — Generated Luau uses `--!strict` with full type annotations (`type self`, `export type`, typed parameters and returns)
- **Enums & Structs** — Full support for C# enums (string-valued) and structs with IntelliSense, hover icons, and completions
- **Object Initializers** — `new() { Field = value }` syntax with field completions
- **Static/Instance Validation** — Enforces correct `.` vs `:` call syntax with compiler errors
- **Cross-Script Resolution** — Automatic dependency resolution between scripts via shared runtime registry
- **Generic `GetService<T>`** — `game.GetService<Players>()` transpiles to `game:GetService("Players")`
- **0-to-1 Index Conversion** — Array indices automatically adjusted for Luau's 1-based indexing
- **Project View** — Tree view of all LUSharp scripts with build status, rename, delete, and create
- **Error List** — Dockable error list window with severity filtering and click-to-navigate
- **Build System** — Single-script or build-all compilation with error and warning reporting
- **Roblox API Awareness** — Completions and validation for Roblox services, instances, and enums

### Plugin Install

Download `LUSharp-plugin.rbxmx` from the [latest release](../../releases/latest) and place it in your Roblox Studio plugins folder:

```
%localappdata%\Roblox\Plugins\LUSharp-plugin.rbxmx
```

Restart Roblox Studio. The LUSharp toolbar will appear with Editor, Project, Build, and New Script buttons.

## CLI Installation

### Quick Install

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/LUSharp/LUSharp/master/install.ps1 | iex
```

**Linux / macOS:**
```bash
curl -fsSL https://raw.githubusercontent.com/LUSharp/LUSharp/master/install.sh | bash
```

### Manual Install

Download the latest release for your platform from [Releases](../../releases):

| Platform | Download |
|----------|----------|
| Windows  | `lusharp-win-x64.zip` |
| Linux    | `lusharp-linux-x64.tar.gz` |
| macOS    | `lusharp-osx-x64.tar.gz` |

Extract the archive and add the directory to your `PATH`.

## Quick Start

```bash
# Create a new project
lusharp new MyGame

# Enter the project directory
cd MyGame

# Build + transpile (lusharp runs automatically via MSBuild)
dotnet build

# Sync to Roblox Studio with Rojo
rojo serve
```

## Example

### C# Input

```csharp
using System;
using System.Collections.Generic;
using Roblox.Classes;

namespace Game.Server
{
    public class GameScript
    {
        public enum State
        {
            Idle,
            Running,
            Paused
        }

        public struct PlayerData
        {
            public string Name;
            public int Score;
        }

        public static void Main()
        {
            PlayerData data = new()
            {
                Name = "Alice",
                Score = 100
            };

            Console.WriteLine($"Player: {data.Name}, Score: {data.Score}");

            var items = new List<int>() { 10, 20, 30 };
            foreach (var item in items)
            {
                print(item);
            }

            var players = game.GetService<Players>();
            players.PlayerAdded.Connect((Player p) =>
            {
                print($"{p.DisplayName} joined!");
            });
        }
    }
}
```

### Generated Luau

```lua
--!strict
-- Compiled by LUSharp (do not edit)

local Players = game:GetService("Players")

local State = ({
    ['Idle'] = "Idle";
    ['Running'] = "Running";
    ['Paused'] = "Paused";
})

export type State = keyof<typeof(State)>

type PlayerData_self = {
    Name: string?;
    Score: number?;
}

local PlayerData = {}
PlayerData.__index = PlayerData

export type PlayerData = typeof(setmetatable({} :: PlayerData_self, PlayerData))

function PlayerData.new(): PlayerData
    local self = setmetatable({} :: PlayerData_self, PlayerData)
    return self
end

local GameScript = {}
GameScript.__index = GameScript

export type GameScript = typeof(setmetatable({}, GameScript))

function GameScript.Main()
    local data = PlayerData.new()
    data.Name = "Alice"
    data.Score = 100
    print(`Player: {data.Name}, Score: {data.Score}`)
    local items = { 10, 20, 30 }
    for _, item in items do
        print(item)
    end
    local players = Players
    players.PlayerAdded:Connect(function(p) print(`{p.DisplayName} joined!`) end)
end

return GameScript
```

## Project Structure

Running `lusharp new MyGame` generates:

```
MyGame/
  src/
    client/
      ClientMain.cs         -- Client-side entry point (LocalScript)
    server/
      ServerMain.cs         -- Server-side entry point (Script)
    shared/
      SharedModule.cs       -- Shared module (ModuleScript)
  lib/
    LUSharpAPI.dll          -- Roblox API bindings for intellisense
  out/                      -- Transpiled Luau output (gitignored)
  MyGame.csproj             -- .NET project (references LUSharpAPI)
  MyGame.sln                -- Solution file
  lusharp.json              -- LUSharp project config
  default.project.json      -- Rojo config
  .gitignore
```

The `out/` directory maps to Roblox via Rojo:

| Output Folder   | Roblox Location                           |
|-----------------|-------------------------------------------|
| `out/server/`   | `ServerScriptService`                     |
| `out/client/`   | `StarterPlayer/StarterPlayerScripts`      |
| `out/shared/`   | `ReplicatedStorage/Shared`                |
| `out/runtime/`  | `ReplicatedStorage/Runtime`               |

## Commands

| Command | Description |
|---------|-------------|
| `lusharp new <name>` | Create a new LUSharp project with full .NET scaffolding |
| `lusharp build` | Transpile C# source to Luau output |
| `lusharp help` | Show available commands |
| `lusharp --version` | Print the LUSharp version |

## How It Works

LUSharp uses a three-stage pipeline:

1. **Frontend** — Parses C# source files using [Roslyn](https://github.com/dotnet/roslyn) and validates the entry point structure
2. **Transform** — Converts the Roslyn syntax tree into a Lua IR (intermediate representation) through a series of passes: symbol collection, type resolution, import resolution, method body lowering, control flow lowering, and optimization
3. **Backend** — Renders the Lua IR into Luau source code

### Cross-Script Runtime

When scripts reference classes from other scripts, LUSharp generates a shared `_LUSharpRuntime` ModuleScript that handles dependency resolution. Scripts register their classes into a shared registry and entry points are deferred until all dependencies are loaded.

### C# to Luau Mappings

| C# | Luau |
|----|------|
| Class with fields | `local T = {}` table with `.new()` constructor, `type self` block, `export type` |
| Struct | Same as class — table with `.new()`, typed fields |
| Enum | `local E = ({ ['A'] = "A"; })` with `export type E = keyof<typeof(E)>` |
| Static members | Class-level table entries (dot syntax) |
| Instance members | Dot syntax with explicit `self: ClassName` parameter |
| Properties | `get_Prop()` / `set_Prop()` methods |
| `List<T>` | `{1, 2, 3}` numeric table |
| `Dictionary<K,V>` | `{key = val}` table |
| `foreach (var x in list)` | `for _, x in list do` |
| `game.GetService<T>()` | `game:GetService("T")` |
| `array[0]` | `array[1]` (automatic 0→1 index conversion) |
| `Console.WriteLine()` | `print()` |
| String interpolation `$"..."` | Backtick string `` `...` `` |
| `string` / `int` / `float` / `bool` | `string` / `number` / `number` / `boolean` |

## Roadmap

- [x] Class, property, and constructor transpilation
- [x] Collection initializers (`List<T>`, `Dictionary<K,V>`)
- [x] String interpolation conversion
- [x] `Console.WriteLine` to `print` mapping
- [x] Project scaffolding with `lusharp new`
- [x] Rojo integration
- [x] Roblox API bindings (LUSharpAPI)
- [x] Build command (`lusharp build`)
- [x] MSBuild integration (`dotnet build` triggers transpilation)
- [x] Install scripts (Windows + Linux/macOS)
- [x] Roblox Studio plugin with C# editor
- [x] Real-time IntelliSense and diagnostics
- [x] Static/instance call validation (CS0176, CS0120)
- [x] Cross-script dependency resolution
- [x] Enum support (string-valued, `keyof<typeof()>` export)
- [x] Struct support (typed fields, constructors, methods)
- [x] Object initializer syntax (`new() { Field = value }`)
- [x] `--!strict` Luau output with full type annotations
- [x] Generic `GetService<T>()` transpilation
- [x] 0-based to 1-based index conversion
- [x] `foreach` to `for _, x in` conversion
- [x] Error List window with severity filtering
- [x] Warning squiggles in editor
- [ ] Full method body transpilation
- [ ] `async`/`await` to coroutine conversion
- [ ] Interface support
- [ ] Watch mode (`lusharp build --watch`)
- [ ] Package system for community Roblox API extensions

## Contributing

Contributions are welcome! Feel free to open an issue or pull request.

1. Fork the repo
2. Create your feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push to the branch and open a pull request

### Building from Source

```bash
git clone https://github.com/LUSharp/LUSharp.git
cd LUSharp
dotnet build
dotnet test
```

## License

LUSharp is licensed under the [MIT License](LICENSE).
