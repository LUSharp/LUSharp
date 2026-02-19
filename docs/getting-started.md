# Getting Started with LUSharp

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Rojo](https://rojo.space) (for syncing Luau output to Roblox Studio)

## Installation

Download the latest release binary for your platform from [GitHub Releases](https://github.com/LUSharp/LUSharp/releases):

- **Windows**: `lusharp-win-x64.zip`
- **Linux**: `lusharp-linux-x64.tar.gz`
- **macOS**: `lusharp-osx-x64.tar.gz`

Extract and add to your PATH.

## Create a Project

```bash
lusharp new MyGame
cd MyGame
```

This generates:

```
MyGame/
  src/
    client/ClientMain.cs      # Client entry point (LocalScript)
    server/ServerMain.cs      # Server entry point (Script)
    shared/SharedModule.cs    # Shared module (ModuleScript)
  lib/LUSharpAPI.dll          # Roblox API bindings for intellisense
  out/                        # Transpiled Luau output
  MyGame.csproj               # .NET project file
  MyGame.sln                  # Solution file
  lusharp.json                # LUSharp config
  default.project.json        # Rojo config
  .gitignore
```

## Verify Intellisense

Open the project in VS Code or Visual Studio:

```bash
dotnet build
```

This compiles against `LUSharpAPI.dll` and gives you full intellisense for Roblox types.

## Write Code

Every script file needs a class with a `GameEntry()` method. Here's the generated client template:

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

### Script Types

| Base Class | Roblox Equivalent | Location |
|------------|-------------------|----------|
| `RobloxScript` | Script / LocalScript | `src/server/` or `src/client/` |
| `ModuleScript` | ModuleScript | `src/shared/` |

## Build

Transpile your C# to Luau:

```bash
lusharp build
```

Output goes to `out/client/`, `out/server/`, and `out/shared/`.

## Sync to Roblox

Use Rojo to sync the output into Roblox Studio:

```bash
rojo serve
```

Then connect from the Rojo plugin in Roblox Studio.

## Project Config

`lusharp.json` controls the build:

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

| Field | Description |
|-------|-------------|
| `name` | Project name |
| `version` | Project version |
| `packages` | Community packages to include (future) |
| `build.src` | Source directory |
| `build.out` | Output directory |
