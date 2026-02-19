# Writing Scripts

Every LUSharp script is a C# class that extends either `RobloxScript` or `ModuleScript`. This page covers script types, entry points, and the globals available to your code.

## Script Types

### RobloxScript

The base class for client and server scripts. Use this for code that runs immediately when the game starts.

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

- Place in `src/server/` for server scripts or `src/client/` for client scripts
- Must override `GameEntry()` — this is the entry point
- `GameEntry()` is called automatically when the script runs

### ModuleScript

For shared, reusable modules that are imported by other scripts via `require()`.

=== "C#"

    ```csharp
    using LUSharpAPI.Runtime.Internal;

    namespace MyGame.Shared
    {
        internal class MathUtils : ModuleScript
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }

            public static int Clamp(int value, int min, int max)
            {
                if (value < min) return min;
                if (value > max) return max;
                return value;
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local MathUtils = {}

    function MathUtils.Add(a: number, b: number): number
        return a + b
    end

    function MathUtils.Clamp(value: number, min: number, max: number): number
        if value < min then return min end
        if value > max then return max end
        return value
    end

    return MathUtils
    ```

- Place in `src/shared/`
- No `GameEntry()` needed — modules are imported, not executed directly
- Other scripts import them with `using` statements, transpiled to `require()` calls

## The GameEntry Convention

Every `RobloxScript` must have a `GameEntry()` method. LUSharp enforces this at transpile time.

```csharp
public override void GameEntry()
{
    // Your code here — this runs when the script starts
}
```

!!! warning
    If your class extends `RobloxScript` but doesn't have a `GameEntry()` method, the transpiler will report an error and skip the file.

## Available Globals

When extending `RobloxScript`, you inherit access to Roblox globals as C# members:

| Global | Type | Description |
|--------|------|-------------|
| `game` | `DataModel` | The root of the Roblox instance hierarchy |
| `workspace` | `Workspace` | Shortcut to `game.Workspace` |
| `print(...)` | method | Output to the Roblox console |
| `warn(...)` | method | Output a warning to the Roblox console |
| `wait(n)` | method | Yield for `n` seconds |
| `typeof(obj)` | method | Returns the Roblox type name of an object |

=== "C#"

    ```csharp
    public override void GameEntry()
    {
        print("Game loaded");
        warn("This is a warning");

        var ws = workspace;       // Workspace instance
        var dm = game;            // DataModel instance

        wait(2);                  // Wait 2 seconds
        print("2 seconds later");
    }
    ```

=== "Generated Luau"

    ```lua
    function Main.GameEntry()
        print("Game loaded")
        warn("This is a warning")

        local ws = workspace
        local dm = game

        wait(2)
        print("2 seconds later")
    end
    ```

## Classes and Fields

LUSharp transpiles C# classes into Lua table-based objects.

### Instance Fields

=== "C#"

    ```csharp
    internal class Player : RobloxScript
    {
        private int health = 100;
        private string name;
        private bool isAlive = true;

        public override void GameEntry()
        {
            name = "Steve";
            print($"{name} has {health} HP");
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local Player = {}
    Player.__index = Player

    function Player.new()
        local self = setmetatable({}, Player)
        self._health = 100
        self._name = nil
        self._isAlive = true
        return self
    end

    function Player.GameEntry()
        local self = Player.new()
        self._name = "Steve"
        print(`{self._name} has {self._health} HP`)
    end

    Player.GameEntry()

    return Player
    ```

### Static Members

```csharp
internal class Config : ModuleScript
{
    public static int MaxPlayers = 10;
    public static string GameVersion = "1.0.0";
}
```

Static members become table-level entries, not instance fields.

### Properties

=== "C#"

    ```csharp
    public int Health { get; set; } = 100;
    ```

=== "Generated Luau"

    ```lua
    function Player:get_Health()
        return self._health
    end

    function Player:set_Health(value)
        self._health = value
    end
    ```

## Collections

### Lists

=== "C#"

    ```csharp
    var items = new List<string> { "Sword", "Shield", "Potion" };
    ```

=== "Generated Luau"

    ```lua
    local items = {"Sword", "Shield", "Potion"}
    ```

### Dictionaries

=== "C#"

    ```csharp
    var scores = new Dictionary<string, int>
    {
        { "Alice", 100 },
        { "Bob", 85 }
    };
    ```

=== "Generated Luau"

    ```lua
    local scores = {Alice = 100, Bob = 85}
    ```

## String Interpolation

C# string interpolation converts directly to Luau template strings:

=== "C#"

    ```csharp
    string name = "World";
    print($"Hello, {name}!");
    print($"2 + 2 = {2 + 2}");
    ```

=== "Generated Luau"

    ```lua
    local name = "World"
    print(`Hello, {name}!`)
    print(`2 + 2 = {2 + 2}`)
    ```

## Next Steps

- [Using the Roblox API](roblox-api.md) — services, instances, and events
- [Type Mappings](type-mappings.md) — complete C# to Luau type reference
