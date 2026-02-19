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
    using LUSharpAPI.Runtime.Internal;
    using LUSharpAPI.Runtime.STL.Types;
    using LUSharpAPI.Runtime.STL.Services;

    namespace MyGame.Server
    {
        internal class ServerMain : RobloxScript
        {
            public override void GameEntry()
            {
                Players.PlayerAdded += (player) =>
                {
                    print($"Welcome, {player.Name}!");
                };

                var spawn = new Vector3(0, 10, 0);
                print($"Spawn point: {spawn}");
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local Players = game:GetService("Players")

    local ServerMain = {}

    function ServerMain.GameEntry()
        Players.PlayerAdded:Connect(function(player)
            print(`Welcome, {player.Name}!`)
        end)

        local spawn = Vector3.new(0, 10, 0)
        print(`Spawn point: {spawn}`)
    end

    ServerMain.GameEntry()

    return ServerMain
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
