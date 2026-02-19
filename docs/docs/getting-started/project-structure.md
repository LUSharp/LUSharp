# Project Structure

When you run `lusharp new MyGame`, the following project is generated:

```
MyGame/
├── src/
│   ├── client/
│   │   └── ClientMain.cs        # Client entry point (LocalScript)
│   ├── server/
│   │   └── ServerMain.cs        # Server entry point (Script)
│   └── shared/
│       └── SharedModule.cs      # Shared module (ModuleScript)
├── out/                         # Transpiled Luau output (gitignored)
│   ├── client/
│   ├── server/
│   └── shared/
├── lib/
│   └── LUSharpAPI.dll           # Roblox API bindings for IntelliSense
├── MyGame.csproj                # .NET project file
├── MyGame.sln                   # Solution file
├── lusharp.json                 # LUSharp configuration
├── default.project.json         # Rojo configuration
└── .gitignore
```

## Source Directories

### `src/client/`

Scripts that run on the **client** (player's machine). These become `LocalScript` instances in Roblox.

- Mapped to `StarterPlayer/StarterPlayerScripts` in Roblox
- Classes here should extend `RobloxScript`
- Has access to client-only APIs like `UserInputService`, local `Player`, etc.

### `src/server/`

Scripts that run on the **server**. These become `Script` instances in Roblox.

- Mapped to `ServerScriptService` in Roblox
- Classes here should extend `RobloxScript`
- Has access to server-only APIs like `DataStoreService`, `ServerStorage`, etc.

### `src/shared/`

Modules shared between client and server. These become `ModuleScript` instances in Roblox.

- Mapped to `ReplicatedStorage/Shared` in Roblox
- Classes here should extend `ModuleScript`
- Imported via `require()` from client or server scripts

## Configuration Files

### `lusharp.json`

Controls the LUSharp build process.

```json
{
  "name": "MyGame",
  "version": "1.0.0",
  "packages": [],
  "build": {
    "src": "./src",
    "out": "./out"
  }
}
```

| Field | Description |
|-------|-------------|
| `name` | Project name (used in Rojo config and output) |
| `version` | Project version |
| `packages` | Community packages to include |
| `build.src` | Source directory containing your C# files |
| `build.out` | Output directory for transpiled Luau |

### `default.project.json`

Rojo project configuration. Maps output folders to Roblox's instance hierarchy.

```json
{
  "name": "MyGame",
  "tree": {
    "$className": "DataModel",
    "ServerScriptService": {
      "$path": "out/server"
    },
    "StarterPlayer": {
      "StarterPlayerScripts": {
        "$path": "out/client"
      }
    },
    "ReplicatedStorage": {
      "Shared": {
        "$path": "out/shared"
      }
    }
  }
}
```

!!! info
    You generally don't need to edit this file. LUSharp generates it with the correct mappings for your project.

### `MyGame.csproj`

Standard .NET project file. References `LUSharpAPI.dll` for IntelliSense.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="LUSharpAPI">
      <HintPath>lib/LUSharpAPI.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

!!! warning
    The `.csproj` is only used for IntelliSense and type checking. LUSharp does its own parsing — you don't need `dotnet build` for transpilation, but it's useful to verify your code compiles.

## Output Directory

The `out/` folder contains transpiled Luau and is **gitignored** by default. After running `lusharp build`:

```
out/
├── client/
│   └── ClientMain.luau
├── server/
│   └── ServerMain.luau
└── shared/
    └── SharedModule.luau
```

Each `.cs` file in `src/` produces a corresponding `.luau` file in `out/`.
