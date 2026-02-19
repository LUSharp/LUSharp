# LUSharp

**Write Roblox games in C# — transpiled to Luau.**

LUSharp is a C# to Luau transpiler for Roblox, similar to [roblox-ts](https://roblox-ts.com) but for C# developers. Write your game logic in modern C# with full intellisense, then transpile it to Luau that runs natively in Roblox.

## Installation

Download the latest release for your platform from [Releases](../../releases):

| Platform | Download |
|----------|----------|
| Windows  | `lusharp-win-x64.zip` |
| Linux    | `lusharp-linux-x64.tar.gz` |
| macOS    | `lusharp-osx-x64.tar.gz` |

Extract the archive and add the directory to your `PATH`.

**Windows (PowerShell):**
```powershell
# Extract to a permanent location, then add to PATH
$dest = "$env:LOCALAPPDATA\LUSharp"
Expand-Archive lusharp-win-x64.zip -DestinationPath $dest
[Environment]::SetEnvironmentVariable("PATH", "$env:PATH;$dest", "User")
```

**Linux / macOS:**
```bash
sudo tar -xzf lusharp-linux-x64.tar.gz -C /usr/local/bin
```

## Quick Start

```bash
# Create a new project
lusharp new MyGame

# Enter the project directory
cd MyGame

# Verify intellisense works
dotnet build

# Transpile C# to Luau
lusharp build

# Sync to Roblox Studio with Rojo
rojo serve
```

## Example

### C# Input (`src/client/ClientMain.cs`)

```csharp
using LUSharpAPI.Runtime.Internal;

namespace MyGame.Client
{
    internal class ClientMain : RobloxScript
    {
        public override void GameEntry()
        {
            print("Hello from C# in Luau!");
        }
    }
}
```

### Generated Luau (`out/client/ClientMain.lua`)

```lua
local ClientMain = {}

function ClientMain.GameEntry()
    print("Hello from C# in Luau!")
end

ClientMain.GameEntry()

return ClientMain
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

## How It Works

LUSharp uses a three-stage pipeline:

1. **Frontend** — Parses C# source files using [Roslyn](https://github.com/dotnet/roslyn) and validates the entry point structure
2. **Transform** — Converts the Roslyn syntax tree into a Lua IR (intermediate representation) through a series of passes: symbol collection, type resolution, import resolution, method body lowering, control flow lowering, and optimization
3. **Backend** — Renders the Lua IR into Luau source code

### C# to Luau Mappings

| C# | Luau |
|----|------|
| Class with fields | `local T = {}` table with `.new()` constructor |
| Static members | Class-level table entries |
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
- [ ] Full method body transpilation
- [ ] Cross-file `require()` references
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
