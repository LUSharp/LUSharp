---
layout: home
title: LUSharp
---

# LUSharp

**Write Roblox games in C# — transpiled to Luau.**

LUSharp is a C# to Luau transpiler for Roblox, similar to [roblox-ts](https://roblox-ts.com) but for C# developers. Write your game logic in modern C# with full intellisense, then transpile it to Luau that runs natively in Roblox.

## Quick Start

```bash
# Download lusharp from the latest release, then:
lusharp new MyGame
cd MyGame
dotnet build          # verify intellisense
lusharp build         # transpile to Luau
rojo serve            # sync to Roblox Studio
```

## Documentation

- [Getting Started](getting-started) — Installation, project setup, and build workflow
- [Architecture](architecture) — Pipeline design, project structure, and type mappings
- [Contributing](contributing) — Building from source and submitting PRs

## Example

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

Transpiles to:

```lua
local ClientMain = {}

function ClientMain.GameEntry()
    print("Hello from C# in Luau!")
end

ClientMain.GameEntry()

return ClientMain
```

## Download

Get the latest release from [GitHub Releases](https://github.com/LUSharp/LUSharp/releases).

| Platform | Download |
|----------|----------|
| Windows  | `lusharp-win-x64.zip` |
| Linux    | `lusharp-linux-x64.tar.gz` |
| macOS    | `lusharp-osx-x64.tar.gz` |
