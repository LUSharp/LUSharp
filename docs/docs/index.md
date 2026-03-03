# LUSharp

**Write Roblox games in C# — transpiled to Luau.**

LUSharp is a C# to Luau transpiler for Roblox, similar to [roblox-ts](https://roblox-ts.com) but for C# developers. Write your game logic in modern C# with full IntelliSense and type checking, then transpile it to Luau that runs natively in Roblox.

---

<div class="grid cards" markdown>

-   :material-language-csharp:{ .lg .middle } **Write in C#**

    ---

    Use the language you already know — classes, properties, generics, LINQ. Full IntelliSense from your IDE of choice.

-   :material-swap-horizontal:{ .lg .middle } **Transpile to Luau**

    ---

    LUSharp converts your C# into clean, idiomatic Luau. No runtime overhead — just native Roblox scripts.

-   :material-cube-outline:{ .lg .middle } **Full Roblox API**

    ---

    Access the complete Roblox API — Instance, BasePart, Vector3, CFrame, services, events — all with C# types.

-   :material-sync:{ .lg .middle } **Rojo Integration**

    ---

    Built-in Rojo project generation. Transpile and sync to Roblox Studio in one step.

</div>

---

## Quick Example

=== "C#"

    ```csharp
    using System;
    using Roblox.Classes;

    namespace Game.Server
    {
        public class GameScript
        {
            public enum State { Idle, Running, Paused }

            public struct PlayerData
            {
                public string Name;
                public int Score;
            }

            public static void Main()
            {
                PlayerData data = new() { Name = "Alice", Score = 100 };
                Console.WriteLine($"Player: {data.Name}");

                var players = game.GetService<Players>();
                players.PlayerAdded.Connect((Player p) =>
                {
                    print($"{p.DisplayName} joined!");
                });
            }
        }
    }
    ```

=== "Generated Luau"

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
        print(`Player: {data.Name}`)
        local players = Players
        players.PlayerAdded:Connect(function(p) print(`{p.DisplayName} joined!`) end)
    end

    return GameScript
    ```

---

## Get Started

```bash
# Download lusharp from the latest release, then:
lusharp new MyGame
cd MyGame
dotnet build          # verify IntelliSense
lusharp build         # transpile to Luau
rojo serve            # sync to Roblox Studio
```

[:octicons-arrow-right-24: Installation](getting-started/installation.md){ .md-button .md-button--primary }
[:octicons-arrow-right-24: Quick Start](getting-started/quick-start.md){ .md-button }

---

## Download

Get the latest release from [GitHub Releases](https://github.com/LUSharp/LUSharp/releases).

| Platform | Download |
|----------|----------|
| Windows  | `lusharp-win-x64.zip` |
| Linux    | `lusharp-linux-x64.tar.gz` |
| macOS    | `lusharp-osx-x64.tar.gz` |
