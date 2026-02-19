# API Reference

This section documents the Roblox API types available through LUSharpAPI. These C# types provide full IntelliSense in your IDE and are transpiled to their native Luau equivalents.

!!! tip "Full Roblox API"
    LUSharpAPI covers the most commonly used Roblox types. For the complete Roblox API (541+ classes, 500+ enums), see the official [Roblox Creator Documentation](https://create.roblox.com/docs/reference/engine).

## Class Hierarchy

```
Globals
├── RobloxScript          # Base for client/server scripts
│   └── (your scripts)
└── ModuleScript          # Base for shared modules
    └── (your modules)

Instance
├── PVInstance
│   ├── BasePart          # Part, MeshPart, etc.
│   │   └── Part
│   ├── Model
│   └── WorldRoot
│       └── Workspace
├── Player
├── Humanoid
├── Folder
├── Camera
└── ... (541+ generated classes)
```

## Script Base Classes

| Type | Description |
|------|-------------|
| [RobloxScript](roblox-script.md) | Base class for client and server scripts — entry point via `GameEntry()` |
| [ModuleScript](module-script.md) | Base class for shared modules — imported via `require()` |

## Instance Types

| Type | Description | Roblox Docs |
|------|-------------|-------------|
| [Instance](instance.md) | Base class for all Roblox objects | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/Instance) |
| [BasePart](base-part.md) | Base class for physical parts — physics, collisions, appearance | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/BasePart) |
| [Model](model.md) | Container for grouped parts — characters, vehicles, structures | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/Model) |
| [Player](player.md) | A connected player — identity, character, camera settings | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/Player) |

## Value Types

| Type | Description | Roblox Docs |
|------|-------------|-------------|
| [Vector3](vector3.md) | 3D vector — positions, directions, sizes | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/Vector3) |
| [CFrame](cframe.md) | Coordinate frame — position + rotation | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/CFrame) |
| [Color3](color3.md) | RGB color (0–1 or 0–255) | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/Color3) |
| [BrickColor](brickcolor.md) | Legacy named color palette | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/BrickColor) |

## Event System

| Type | Description | Roblox Docs |
|------|-------------|-------------|
| [RBXScriptSignal](events.md) | Event signals — `Connect()`, `Once()`, `Wait()` | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/RBXScriptSignal) |
| [RBXScriptConnection](events.md#rbxscriptconnection) | Event listener handle — `Disconnect()` | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/datatypes/RBXScriptConnection) |

## Raycasting & Spatial Queries

| Type | Description | Roblox Docs |
|------|-------------|-------------|
| [Ray / RaycastParams / RaycastResult](raycasting.md) | Raycasting and spatial queries | [:octicons-link-external-16:](https://create.roblox.com/docs/workspace/raycasting) |

## Services

| Type | Description | Roblox Docs |
|------|-------------|-------------|
| [Players](services.md#players) | Player management and join/leave events | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/Players) |
| [Workspace](services.md#workspace) | The 3D world — raycasting, physics queries | [:octicons-link-external-16:](https://create.roblox.com/docs/reference/engine/classes/Workspace) |

## Globals

When extending `RobloxScript` or `ModuleScript`, you inherit these globals:

| Global | Type | Description |
|--------|------|-------------|
| `game` | `DataModel` | Root of the Roblox instance hierarchy |
| `workspace` | `Workspace` | The 3D workspace |
| `script` | `LuaSourceContainer` | Reference to the running script |
| `Enum` | `Enums` | Access to all Roblox enums |
| `shared` | `List<object>` | Shared table between scripts |
| `print(...)` | method | Print to output |
| `warn(...)` | method | Print a warning |
| `error(msg, level)` | method | Throw an error |
| `assert(cond, msg)` | method | Assert a condition |
| `time()` | method | Time since game start |
| `tick()` | method | Unix timestamp |
| `elapsedTime()` | method | Time since Roblox started |
| `version()` | method | Roblox engine version string |

## Additional Roblox Types

LUSharpAPI includes 541+ auto-generated class stubs and 500+ enums. For types not documented here, refer to the official Roblox documentation:

- [Roblox Classes](https://create.roblox.com/docs/reference/engine/classes) — Instance, Humanoid, Camera, GUI classes, etc.
- [Roblox Data Types](https://create.roblox.com/docs/reference/engine/datatypes) — UDim2, Region3, NumberSequence, etc.
- [Roblox Enums](https://create.roblox.com/docs/reference/engine/enums) — Material, KeyCode, SurfaceType, etc.
