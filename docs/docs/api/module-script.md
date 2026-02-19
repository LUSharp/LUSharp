# ModuleScript

```
Globals > ModuleScript
```

The base class for shared, reusable modules in LUSharp. Extend this class to create code that can be imported by client scripts, server scripts, or other modules.

## Overview

`ModuleScript` classes are placed in `src/shared/` and transpile to Luau `ModuleScript` instances in `ReplicatedStorage`. Other scripts import them using C# `using` statements, which transpile to `require()` calls.

Unlike `RobloxScript`, `ModuleScript` does **not** require a `GameEntry()` method. Modules expose their functionality through static methods and properties.

=== "C# (Module)"

    ```csharp
    using LUSharpAPI.Runtime.Internal;

    namespace MyGame.Shared
    {
        internal class MathUtils : ModuleScript
        {
            public static int Clamp(int value, int min, int max)
            {
                if (value < min) return min;
                if (value > max) return max;
                return value;
            }

            public static double Lerp(double a, double b, double t)
            {
                return a + (b - a) * t;
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local MathUtils = {}

    function MathUtils.Clamp(value: number, min: number, max: number): number
        if value < min then return min end
        if value > max then return max end
        return value
    end

    function MathUtils.Lerp(a: number, b: number, t: number): number
        return a + (b - a) * t
    end

    return MathUtils
    ```

## Importing a Module

=== "C# (Importing)"

    ```csharp
    using LUSharpAPI.Runtime.Internal;
    using MyGame.Shared;

    namespace MyGame.Server
    {
        internal class ServerMain : RobloxScript
        {
            public override void GameEntry()
            {
                int clamped = MathUtils.Clamp(150, 0, 100);
                print($"Clamped: {clamped}"); // 100

                double lerped = MathUtils.Lerp(0, 100, 0.5);
                print($"Lerped: {lerped}"); // 50
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local MathUtils = require(game.ReplicatedStorage.Shared.MathUtils)

    local ServerMain = {}

    function ServerMain.GameEntry()
        local clamped = MathUtils.Clamp(150, 0, 100)
        print(`Clamped: {clamped}`)

        local lerped = MathUtils.Lerp(0, 100, 0.5)
        print(`Lerped: {lerped}`)
    end

    ServerMain.GameEntry()

    return ServerMain
    ```

## Inherited Globals

Like `RobloxScript`, `ModuleScript` extends `Globals` and has access to all Roblox globals (`game`, `workspace`, `print`, `warn`, etc.). See [RobloxScript > Inherited Globals](roblox-script.md#inherited-globals) for the full list.

## Key Differences from RobloxScript

| | RobloxScript | ModuleScript |
|---|---|---|
| **Entry point** | Requires `GameEntry()` | No entry point — imported by others |
| **Execution** | Runs automatically | Runs when first `require()`d |
| **Location** | `src/client/` or `src/server/` | `src/shared/` |
| **Roblox type** | Script / LocalScript | ModuleScript |
| **Roblox location** | ServerScriptService / StarterPlayerScripts | ReplicatedStorage/Shared |

## Patterns

### Data Module

```csharp
using LUSharpAPI.Runtime.Internal;

namespace MyGame.Shared
{
    internal class GameConfig : ModuleScript
    {
        public static int MaxPlayers = 10;
        public static double RespawnTime = 5.0;
        public static string GameVersion = "1.0.0";
    }
}
```

### Utility Module

```csharp
using LUSharpAPI.Runtime.Internal;
using LUSharpAPI.Runtime.STL.Types;

namespace MyGame.Shared
{
    internal class VectorUtils : ModuleScript
    {
        public static double Distance(Vector3 a, Vector3 b)
        {
            return (a - b).Magnitude;
        }

        public static Vector3 Midpoint(Vector3 a, Vector3 b)
        {
            return a.Lerp(b, 0.5);
        }
    }
}
```
