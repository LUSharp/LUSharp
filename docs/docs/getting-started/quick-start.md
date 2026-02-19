# Quick Start

This guide walks you through creating a LUSharp project, writing a script, transpiling it, and running it in Roblox Studio.

## 1. Create a Project

```bash
lusharp new MyGame
cd MyGame
```

This generates a complete project structure with template scripts, a .NET project file, and a Rojo configuration.

## 2. Verify IntelliSense

Open the project in your IDE and build:

```bash
dotnet build
```

This compiles against `LUSharpAPI.dll` and gives you full IntelliSense for all Roblox types — `Vector3`, `CFrame`, `Instance`, services, events, and more.

## 3. Write a Script

Open `src/client/ClientMain.cs`:

=== "C#"

    ```csharp
    using LUSharpAPI.Runtime.Internal;
    using LUSharpAPI.Runtime.STL.Types;

    namespace MyGame.Client
    {
        internal class ClientMain : RobloxScript
        {
            public override void GameEntry()
            {
                var position = new Vector3(10, 5, 10);
                print($"Player spawned at {position}");

                workspace.ChildAdded += (child) =>
                {
                    print($"New object: {child.Name}");
                };
            }
        }
    }
    ```

=== "Generated Luau"

    ```lua
    local ClientMain = {}

    function ClientMain.GameEntry()
        local position = Vector3.new(10, 5, 10)
        print(`Player spawned at {position}`)

        workspace.ChildAdded:Connect(function(child)
            print(`New object: {child.Name}`)
        end)
    end

    ClientMain.GameEntry()

    return ClientMain
    ```

!!! tip
    Every script file must contain a class that inherits from `RobloxScript` (or `ModuleScript`) with a `GameEntry()` method. This is the entry point that LUSharp calls when your script runs.

## 4. Build

Transpile your C# to Luau:

```bash
lusharp build
```

The transpiled output goes to:

| Source | Output |
|--------|--------|
| `src/client/` | `out/client/` |
| `src/server/` | `out/server/` |
| `src/shared/` | `out/shared/` |

## 5. Sync to Roblox Studio

Start Rojo to sync the output:

```bash
rojo serve
```

Then open Roblox Studio and connect from the Rojo plugin. Your Luau scripts will appear in the correct locations:

| Output | Roblox Location |
|--------|-----------------|
| `out/server/` | `ServerScriptService` |
| `out/client/` | `StarterPlayer/StarterPlayerScripts` |
| `out/shared/` | `ReplicatedStorage/Shared` |

## 6. Watch Mode (Optional)

For rapid iteration, use watch mode to automatically rebuild when files change:

```bash
lusharp build --watch
```

Combined with `rojo serve`, this gives you a live development loop: edit C# in your IDE, save, and see changes reflected in Roblox Studio within seconds.

## Next Steps

- [Project Structure](project-structure.md) — understand every generated file
- [Writing Scripts](../guide/writing-scripts.md) — script types, globals, and patterns
- [Using the Roblox API](../guide/roblox-api.md) — services, instances, and events
