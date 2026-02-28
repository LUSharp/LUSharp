# LUSharp - [Discord](https://discord.gg/c85RP2dzHY)

**Write Roblox games in C# — transpiled to Luau.**

LUSharp is a C# to Luau transpiler for Roblox, similar to [roblox-ts](https://roblox-ts.com) but for C# developers. Write your game logic in modern C# with full IntelliSense, then transpile it to Luau that runs natively in Roblox.

## Roblox Studio Plugin

LUSharp includes a Roblox Studio plugin with a built-in C# editor, real-time IntelliSense, syntax highlighting, and a project view. Write C# directly inside Studio and compile to Luau with one click.

### Plugin Features

- **C# Editor** — Syntax-highlighted editor with auto-indent, word delete, and tab completion
- **IntelliSense** — Real-time completions, hover info, and diagnostics with C# error codes (CS0176, CS0120, CS1061)
- **Static/Instance Validation** — Enforces correct `.` vs `:` call syntax with compiler errors
- **Cross-Script Resolution** — Automatic dependency resolution between scripts via shared runtime registry
- **Project View** — Tree view of all LUSharp scripts with build status, rename, delete, and create
- **Build System** — Single-script or build-all compilation with error reporting
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
namespace Game.Server
{
    public class Helper
    {
        public static void DoStuff() { print("static call"); }
        public void DoInstanceStuff() { print("instance call"); }
    }

    public class Main
    {
        public static void Main()
        {
            Helper.DoStuff();           // emits: Helper.DoStuff()
            Helper helper = new();
            helper.DoInstanceStuff();   // emits: helper:DoInstanceStuff()
        }
    }
}
```

### Generated Luau

```lua
local __r = require(script.Parent:FindFirstChild("_LUSharpRuntime"))

local Helper = {}
function Helper.DoStuff()
    print("static call")
end
function Helper.new()
    local self = setmetatable({}, { __index = Helper })
    return self
end
function Helper:DoInstanceStuff()
    print("instance call")
end

local Main = {}
function Main.Main()
    Helper.DoStuff()
    local helper = Helper.new()
    helper:DoInstanceStuff()
end

__r.register("Main", Main)
Main.Main()
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
| Class with fields | `local T = {}` table with `.new()` constructor |
| Static members | Class-level table entries (dot syntax) |
| Instance members | Colon syntax with implicit `self` |
| Properties | `get_Prop()` / `set_Prop()` methods |
| `List<T>` | `{1, 2, 3}` numeric table |
| `Dictionary<K,V>` | `{key = val}` table |
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
- [ ] Full method body transpilation
- [ ] `async`/`await` to coroutine conversion
- [ ] Enum and interface support
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
git clone https://github.com/yourusername/LUSharp.git
cd LUSharp
dotnet build
dotnet test
```

## License

LUSharp is licensed under the [MIT License](LICENSE).
