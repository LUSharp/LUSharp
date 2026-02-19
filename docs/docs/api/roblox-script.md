# RobloxScript

```
Globals > RobloxScript
```

The base class for all client and server scripts in LUSharp. Extend this class to create scripts that run in `ServerScriptService` (server) or `StarterPlayerScripts` (client).

## Overview

`RobloxScript` is the entry point for your game logic. Every script must override the `GameEntry()` method, which is called automatically when the script runs in Roblox.

=== "C#"

    ```csharp
    using LUSharpAPI.Runtime.Internal;

    namespace MyGame.Server
    {
        internal class ServerMain : RobloxScript
        {
            public override void GameEntry()
            {
                print("Server started!");
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local ServerMain = {}

    function ServerMain.GameEntry()
        print("Server started!")
    end

    ServerMain.GameEntry()

    return ServerMain
    ```

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GameEntry()` | `void` | **Abstract.** The entry point for the script. Must be overridden in every `RobloxScript` subclass. Called automatically when the script runs. |

## Inherited Globals

`RobloxScript` extends `Globals`, giving you access to Roblox global variables and functions as class members.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `game` | `DataModel` | The root of the Roblox instance hierarchy |
| `workspace` | `Workspace` | Shortcut to `game.Workspace` — the 3D world |
| `script` | `LuaSourceContainer` | Reference to the currently running script instance |
| `Enum` | `Enums` | Access to all Roblox enums |
| `shared` | `List<object>` | A shared table accessible from all scripts |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `print(params string[] message)` | `void` | Prints to the Roblox output window |
| `warn(params string[] message)` | `void` | Prints a warning to the output window |
| `error(string message, double level)` | `void` | Throws a Lua error |
| `assert(bool condition, string errorMessage)` | `(bool, string)` | Asserts a condition, errors if false |
| `time()` | `double` | Seconds since the game started |
| `tick()` | `double` | Current Unix timestamp |
| `elapsedTime()` | `double` | Seconds since the Roblox application started |
| `version()` | `string` | The current Roblox engine version |

## Usage

### Server Script

Place in `src/server/`:

```csharp
using LUSharpAPI.Runtime.Internal;
using LUSharpAPI.Runtime.STL.Services;

namespace MyGame.Server
{
    internal class ServerMain : RobloxScript
    {
        public override void GameEntry()
        {
            var players = game.GetService<Players>();
            players.PlayerAdded += (player) =>
            {
                print($"Welcome, {player.Name}!");
            };
        }
    }
}
```

### Client Script

Place in `src/client/`:

```csharp
using LUSharpAPI.Runtime.Internal;

namespace MyGame.Client
{
    internal class ClientMain : RobloxScript
    {
        public override void GameEntry()
        {
            print("Client loaded!");
            var camera = workspace.FindFirstChildOfClass("Camera");
        }
    }
}
```

!!! warning "Entry Point Required"
    The transpiler will report an error if a class extends `RobloxScript` but does not override `GameEntry()`.
