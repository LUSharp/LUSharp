# API Reference

This section documents the Roblox API types available through LUSharpAPI. These C# types provide full IntelliSense in your IDE and are transpiled to their native Luau equivalents.

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
│   └── Model
├── Player
├── Humanoid
├── Folder
├── Camera
└── ...

WorldRoot (extends PVInstance)
└── Workspace             # The 3D world
```

## Script Base Classes

| Type | Description | Guide |
|------|-------------|-------|
| [RobloxScript](roblox-script.md) | Base class for client and server scripts | Entry point via `GameEntry()` |
| [ModuleScript](module-script.md) | Base class for shared modules | Imported via `require()` |

## Instance Types

| Type | Description | Guide |
|------|-------------|-------|
| [Instance](instance.md) | Base class for all Roblox objects | Properties, methods, events |
| [BasePart](base-part.md) | Base class for physical parts | Physics, collisions, appearance |

## Value Types

| Type | Description | Guide |
|------|-------------|-------|
| [Vector3](vector3.md) | 3D vector | Positions, directions, sizes |
| [CFrame](cframe.md) | Coordinate frame (position + rotation) | Transforms, orientations |

## Services

| Type | Description | Guide |
|------|-------------|-------|
| [Players](services.md#players) | Player management and events | Join/leave events, player list |
| [Workspace](services.md#workspace) | The 3D world | Raycasting, physics queries |

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
