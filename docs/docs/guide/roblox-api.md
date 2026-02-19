# Using the Roblox API

LUSharp provides C# bindings for the Roblox API through `LUSharpAPI`. You write natural C# code with full IntelliSense, and the transpiler converts it to the equivalent Roblox Luau API calls.

## Services

Access Roblox services using their C# types. The transpiler converts these to `game:GetService()` calls.

=== "C#"

    ```csharp
    using LUSharpAPI.Runtime.STL.Services;

    public override void GameEntry()
    {
        var players = game.GetService<Players>();
        var lighting = game.GetService<Lighting>();
    }
    ```

=== "Generated Luau"

    ```lua
    local Players = game:GetService("Players")
    local Lighting = game:GetService("Lighting")
    ```

### Common Services

| Service | C# Type | Description |
|---------|---------|-------------|
| Players | `Players` | Player management and events |
| Workspace | `Workspace` | The 3D world (also available as `workspace` global) |
| Lighting | `Lighting` | Lighting and atmosphere |
| ReplicatedStorage | `ReplicatedStorage` | Shared storage between client and server |
| ServerStorage | `ServerStorage` | Server-only storage |
| ServerScriptService | `ServerScriptService` | Server-only scripts |
| UserInputService | `UserInputService` | Client input handling |
| TweenService | `TweenService` | Animation tweening |

## Creating Instances

Create Roblox instances using the `new` keyword:

=== "C#"

    ```csharp
    using LUSharpAPI.Runtime.STL.Generated.Classes;
    using LUSharpAPI.Runtime.STL.Types;

    var part = new Part();
    part.Name = "MyPart";
    part.Size = new Vector3(4, 1, 4);
    part.Position = new Vector3(0, 10, 0);
    part.Anchored = true;
    part.BrickColor = BrickColor.New("Bright red");
    part.Parent = workspace;
    ```

=== "Generated Luau"

    ```lua
    local part = Instance.new("Part")
    part.Name = "MyPart"
    part.Size = Vector3.new(4, 1, 4)
    part.Position = Vector3.new(0, 10, 0)
    part.Anchored = true
    part.BrickColor = BrickColor.new("Bright red")
    part.Parent = workspace
    ```

## Finding Instances

Navigate the instance hierarchy using familiar methods:

=== "C#"

    ```csharp
    // Find a child by name
    var door = workspace.FindFirstChild("Door");

    // Find with type filter
    var part = workspace.FindFirstChildOfClass<Part>("Floor");

    // Wait for a child to exist
    var spawner = workspace.WaitForChild("Spawner");
    ```

=== "Generated Luau"

    ```lua
    local door = workspace:FindFirstChild("Door")
    local part = workspace:FindFirstChildOfClass("Part", "Floor")
    local spawner = workspace:WaitForChild("Spawner")
    ```

## Events

Connect to Roblox events using C# event syntax (`+=`). The transpiler converts this to `:Connect()`.

=== "C#"

    ```csharp
    // Player events
    Players.PlayerAdded += (player) =>
    {
        print($"{player.Name} joined!");

        player.CharacterAdded += (character) =>
        {
            print($"{player.Name} spawned");
        };
    };

    Players.PlayerRemoving += (player) =>
    {
        print($"{player.Name} left");
    };
    ```

=== "Generated Luau"

    ```lua
    Players.PlayerAdded:Connect(function(player)
        print(`{player.Name} joined!`)

        player.CharacterAdded:Connect(function(character)
            print(`{player.Name} spawned`)
        end)
    end)

    Players.PlayerRemoving:Connect(function(player)
        print(`{player.Name} left`)
    end)
    ```

### Touch Events

=== "C#"

    ```csharp
    var part = workspace.FindFirstChild("LavaPart") as BasePart;

    part.Touched += (otherPart) =>
    {
        var humanoid = otherPart.Parent?.FindFirstChildOfClass<Humanoid>();
        if (humanoid != null)
        {
            humanoid.Health = 0;
        }
    };
    ```

=== "Generated Luau"

    ```lua
    local part = workspace:FindFirstChild("LavaPart")

    part.Touched:Connect(function(otherPart)
        local humanoid = otherPart.Parent
            and otherPart.Parent:FindFirstChildOfClass("Humanoid")
        if humanoid then
            humanoid.Health = 0
        end
    end)
    ```

## Importing Modules

Use `using` statements to import shared modules. The transpiler converts these to `require()` calls.

=== "C# (SharedModule)"

    ```csharp
    // src/shared/MathUtils.cs
    using LUSharpAPI.Runtime.Internal;

    namespace MyGame.Shared
    {
        internal class MathUtils : ModuleScript
        {
            public static int Clamp(int val, int min, int max)
            {
                if (val < min) return min;
                if (val > max) return max;
                return val;
            }
        }
    }
    ```

=== "C# (Importing it)"

    ```csharp
    // src/server/ServerMain.cs
    using MyGame.Shared;

    public override void GameEntry()
    {
        int health = MathUtils.Clamp(150, 0, 100);
        print($"Health: {health}"); // 100
    }
    ```

=== "Generated Luau"

    ```lua
    local MathUtils = require(game.ReplicatedStorage.Shared.MathUtils)

    function ServerMain.GameEntry()
        local health = MathUtils.Clamp(150, 0, 100)
        print(`Health: {health}`)
    end
    ```

## Next Steps

- [Type Mappings](type-mappings.md) — complete C# to Luau type reference
- [API Reference](../api/index.md) — detailed documentation for every Roblox type
