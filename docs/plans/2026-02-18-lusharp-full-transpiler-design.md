# LUSharp Full Transpiler Design

**Date:** 2026-02-18
**Status:** Approved
**Scope:** Complete redesign of the transpilation pipeline to support the full C# → Luau feature set with a package system, Roblox API bindings, and `lusharp build` CLI command.

---

## 1. Goals

- Users write natural C# that mirrors Luau semantics — no boilerplate, no attributes, no configuration beyond `lusharp.json`
- Accurate, optimized Luau output for all common C# constructs
- Extensible via packages that ship both C# stubs and a pre-written Luau runtime
- `lusharp build` drives the entire pipeline in one command
- Watch mode for a live Rojo sync workflow

### Out of Scope

- C# reflection
- Roslyn full semantic compilation (not needed at this stage)
- LINQ (deferred to a future package)

---

## 2. User Experience

Game developers write C# that reads like Luau. Nothing else is required of them.

```csharp
public class Main : LocalScript
{
    public override void GameEntry()
    {
        var part = Instance.New<Part>();
        part.Name = "MyPart";
        part.Parent = game.Workspace;

        var players = game.GetService<Players>();

        players.PlayerAdded += (Player p) => {
            print("Joined: " + p.Name);
        };

        RunService.Heartbeat += (double dt) => {
            part.CFrame *= CFrame.Angles(0, dt, 0);
        };
    }
}
```

Enabling a package is one line in `lusharp.json`:

```json
{ "packages": ["ECS"] }
```

After that the user just writes:

```csharp
using Packages.ECS;
var entity = World.AddEntity(new HealthComponent(100));
```

The attributes, mappings, and runtime bundling are invisible to game developers. They are an internal concern of LUSharpAPI and package authors only.

---

## 3. Project Structure

```
LUSharp.sln
├── LUSharp/                    CLI project manager
├── LUSharpTranspiler/          Core transpiler engine
│   ├── Frontend/               Parse + symbol collection
│   ├── Transform/              Multi-pass IR lowering  ← new
│   │   ├── Passes/
│   │   │   ├── SymbolCollector.cs
│   │   │   ├── TypeResolver.cs
│   │   │   ├── ImportResolver.cs
│   │   │   ├── MethodBodyLowerer.cs
│   │   │   ├── ControlFlowLowerer.cs
│   │   │   ├── EventBinder.cs
│   │   │   └── Optimizer.cs
│   │   └── IR/                 Intermediate representation nodes  ← new
│   │       ├── LuaModule.cs
│   │       ├── LuaClassDef.cs
│   │       ├── Statements/
│   │       └── Expressions/
│   ├── Backend/                IR → Luau text emission
│   │   ├── ModuleEmitter.cs    ← new orchestrator
│   │   ├── StatementEmitter.cs ← new
│   │   ├── ExprEmitter.cs      ← new
│   │   └── RuntimeBundler.cs   ← new
│   └── AST/SourceConstructor/  Existing LuaWriter kept as-is
├── LUSharpAPI/                 Roblox bindings (built-in package)
│   └── Runtime/STL/
│       ├── Classes/Instance/
│       ├── Services/
│       ├── Types/
│       ├── Enums/
│       └── LuaToC/
└── packages/                   User packages  ← new
    └── <PackageName>/
        ├── src/                C# stubs
        ├── runtime/            Pre-written Luau
        └── package.lusharp
```

---

## 4. Pipeline

```
lusharp build
│
├── PackageLoader       load lusharp.json, register package symbols + rules
├── Frontend
│   ├── File scanner    Client/**/*.cs, Server/**/*.cs, Shared/**/*.cs
│   ├── Roslyn parser   CSharpSyntaxTree.ParseText() per file
│   └── SymbolCollector register all classes, methods, locations
│
├── Transform
│   ├── Pass 1  TypeResolver        C# types → Lua types
│   ├── Pass 2  ImportResolver      usings → require() paths
│   ├── Pass 3  MethodBodyLowerer   statements → IR nodes
│   ├── Pass 4  ControlFlowLowerer  switch/try/async → Lua equivalents
│   ├── Pass 5  EventBinder         += / .Connect() → :Connect()
│   └── Pass 6  Optimizer           constant folding, local caching, etc.
│                                   (full passes enabled with --release)
│
├── Backend
│   ├── ModuleEmitter   walks LuaModule IR, drives emitters
│   ├── StatementEmitter
│   ├── ExprEmitter
│   └── writes .lua files to out/
│
└── RuntimeBundler      copies packages/*/runtime/ → out/runtime/PackageName/
```

**Key change from today:** `Transpiler.cs` currently processes one file at a time and emits immediately. The new pipeline scans ALL files first so the Transform layer has a complete symbol table before any Lua is written.

---

## 5. Intermediate Representation (IR)

All nodes implement `ILuaNode`. The Transform layer produces IR; the Backend consumes it.

### Top Level

```
LuaModule
├── ScriptType       "LocalScript" | "ModuleScript" | "Script"
├── OutputPath       "out/client/Player.lua"
├── Requires[]       LuaRequire (name + path)
├── Classes[]        LuaClassDef
└── EntryBody[]      ILuaStatement[]   (Main class only)
```

### Class

```
LuaClassDef
├── Name             string
├── Constructor      LuaConstructorDef
├── StaticFields[]   LuaFieldDef
├── InstanceFields[] LuaFieldDef
├── Methods[]        LuaMethodDef
└── Events[]         LuaEventDef   (custom BindableEvent-backed events)
```

### Method

```
LuaMethodDef
├── Name             string
├── IsStatic         bool
├── Parameters[]     string
└── Body[]           ILuaStatement[]
```

### Statements (`ILuaStatement`)

| Node | Luau output |
|---|---|
| `LuaLocal` | `local x = expr` |
| `LuaAssign` | `x.y = expr` |
| `LuaReturn` | `return expr` |
| `LuaIf` | `if cond then ... elseif ... else ... end` |
| `LuaWhile` | `while cond do ... end` |
| `LuaForNum` | `for i = start, stop, step do ... end` |
| `LuaForIn` | `for k, v in pairs(t) do ... end` |
| `LuaRepeat` | `repeat ... until cond` |
| `LuaBreak` | `break` |
| `LuaContinue` | `continue` |
| `LuaError` | `error(msg)` |
| `LuaExprStatement` | bare expression as statement |
| `LuaPCall` | `local ok, err = pcall(fn)` — from `try/catch` |
| `LuaTaskSpawn` | `task.spawn(fn)` — from `async/await` |
| `LuaConnect` | `event:Connect(fn)` — from `+=` or `.Connect()` |
| `LuaMultiAssign` | `a, b = b, a` — from tuple deconstruction |

### Expressions (`ILuaExpression`)

| Node | Example / notes |
|---|---|
| `LuaLiteral` | `42`, `"hello"`, `true`, `nil` |
| `LuaIdent` | variable or function name |
| `LuaMember` | `self.Health`, `game.Workspace` |
| `LuaIndex` | `t[i]` — from indexer access |
| `LuaBinary` | `a + b`, `x == y`, `a and b` |
| `LuaUnary` | `not x`, `-n` |
| `LuaConcat` | `a .. b .. c` — from string `+`, chain-collapsed |
| `LuaInterp` | `` `Hello {name}` `` — from `$"..."` |
| `LuaCall` | `func(args)` |
| `LuaMethodCall` | `obj:Method(args)` |
| `LuaLambda` | `function(params) ... end` |
| `LuaTable` | `{1,2,3}` or `{key = val}` |
| `LuaNew` | `ClassName.new(args)` — from `new Foo()` |
| `LuaTernary` | `cond and a or b` — from `a ? b : c` |
| `LuaNullSafe` | `x and x.y` — from `x?.y` |
| `LuaCoalesce` | `x ~= nil and x or y` — from `x ?? y` |
| `LuaSpread` | `table.unpack(t)` |
| `LuaTypeAssert` | `x :: Type` — Luau type annotation |

---

## 6. Transform Passes

### Pass 1 — TypeResolver

Maps every C# type reference to its Luau equivalent.

| C# | Luau |
|---|---|
| `string` | `string` |
| `int` / `float` / `double` | `number` |
| `bool` | `boolean` |
| `void` | *(omit return)* |
| `object` | `any` |
| `List<T>` | `{T}` |
| `Dictionary<K,V>` | `{[K]: V}` |
| `Action<T>` / `Func<T,R>` | `(T) -> R` |
| `Task` / `Task<T>` | *(async marker — lowered in ControlFlowLowerer)* |
| `RBXScriptSignal<T>` | `RBXScriptSignal<T>` (Roblox native) |
| User-defined class | Resolved via SymbolTable |

### Pass 2 — ImportResolver

Converts `using` directives and cross-class references into `require()` calls with correct Roblox paths.

Script type determined by base class + file location:

| Base class | Output type | Roblox location |
|---|---|---|
| `ModuleScript` | ModuleScript | determined by file folder |
| `LocalScript` / file in `Client/` | LocalScript | StarterPlayerScripts |
| `Script` / file in `Server/` | Script | ServerScriptService |
| `RobloxScript` | determined by folder | — |

Require path mapping:

```
Shared/Player.cs   → require(game.ReplicatedStorage.Shared.Player)
Client/UI.cs       → require(game.StarterPlayer.StarterPlayerScripts.UI)
packages/ECS/      → require(game.ReplicatedStorage.Runtime.ECS)
```

### Pass 3 — MethodBodyLowerer

Walks every C# method body statement-by-statement using Roslyn and produces `ILuaStatement[]` IR. This is the largest new pass.

**Statement mapping:**

| C# | IR node |
|---|---|
| `var x = expr` | `LuaLocal` |
| `x = expr` | `LuaAssign` |
| `return expr` | `LuaReturn` |
| `if / else if / else` | `LuaIf` with `ElseIf[]` chain |
| `for (int i=0; i<n; i++)` | `LuaForNum` (detected as numeric loop) |
| `foreach (var x in col)` | `LuaForIn` with `pairs()` or `ipairs()` |
| `while (cond)` | `LuaWhile` |
| `do { } while (cond)` | `LuaRepeat` |
| `switch` | `LuaIf` elseif chain per case |
| `break` / `continue` | `LuaBreak` / `LuaContinue` |
| `throw` | `LuaError` |
| method call as statement | `LuaExprStatement` |

**Expression mapping:**

| C# | IR node |
|---|---|
| `a + b` (strings) | `LuaConcat` |
| `a + b` (numbers) | `LuaBinary(+)` |
| `a ? b : c` | `LuaTernary` |
| `x?.y` | `LuaNullSafe` |
| `x ?? y` | `LuaCoalesce` |
| `new Foo(args)` | `LuaNew` |
| `x[i]` | `LuaIndex` |
| `(T)x` / `x as T` | stripped (no-op) |
| `nameof(x)` | `LuaLiteral("x")` |
| `$"text {x}"` | `LuaInterp` |
| `x is T` | `LuaBinary(typeof)` |
| `(x) => expr` | `LuaLambda` |

### Pass 4 — ControlFlowLowerer

Handles constructs requiring structural transformation.

**`async`/`await`:**
```csharp
public async Task SpawnLoop() {
    await Task.Delay(1);
    DoThing();
}
```
```lua
function SpawnLoop()
    task.spawn(function()
        task.wait(1)
        DoThing()
    end)
end
```

**`try`/`catch`/`finally`:**
```csharp
try { riskyOp(); }
catch (Exception e) { print(e.Message); }
finally { cleanup(); }
```
```lua
local _ok, _err = pcall(function()
    riskyOp()
end)
if not _ok then
    local e = _err
    print(e)
end
cleanup()
```

**`switch`:**
```csharp
switch (state) {
    case GameState.Lobby:   startLobby(); break;
    case GameState.Playing: startGame();  break;
    default: error("unknown"); break;
}
```
```lua
if state == GameState.Lobby then
    startLobby()
elseif state == GameState.Playing then
    startGame()
else
    error("unknown")
end
```

### Pass 5 — EventBinder

Transforms all event usages into `:Connect()` calls.

```csharp
// Both forms:
players.PlayerAdded += (Player p) => { print(p.Name); };
players.PlayerAdded.Connect((Player p) => { print(p.Name); });
```
```lua
players.PlayerAdded:Connect(function(p)
    print(p.Name)
end)
```

Custom C# `event` declarations → `BindableEvent`:
```csharp
public event Action<int> OnDamaged;
OnDamaged?.Invoke(50);
```
```lua
Player._events = {}
Player._events.OnDamaged = Instance.new("BindableEvent")
-- connect: Player._events.OnDamaged.Event:Connect(fn)
-- fire:    Player._events.OnDamaged:Fire(50)
```

### Pass 6 — Optimizer

Runs last on the fully-built IR before Backend emission.

| Optimization | Description |
|---|---|
| Constant folding | `1 + 2` → `3`, `"a" + "b"` → `"ab"` at IR level |
| String concat chain collapse | `a + b + c + d` → single `LuaConcat` node, avoids intermediate allocations |
| Numeric for simplification | Confirms `LuaForNum` stride is constant; drops step if 1 |
| Local caching | `self.Health` accessed 3+ times in one scope → hoisted `local _health = self.Health` |
| Dead branch elimination | `if (true)` / `if (false)` branches pruned at IR level |
| Tail call preservation | Last `return f(args)` left as-is so Luau's TCO applies |
| Table literal pre-population | Known-size collections emitted as `{1,2,3}` not built with `table.insert` |

Full optimizer passes enabled with `lusharp build --release`.

---

## 7. Backend (Emitter)

The Backend takes `List<LuaModule>` IR and writes `.lua` files. The existing `LuaWriter` is reused as-is.

### Components

**`ModuleEmitter`** — top-level orchestrator per `LuaModule`:
1. Emit header comment
2. Emit `require()` declarations
3. Per `LuaClassDef`: emit table, constructor, static fields, methods, events
4. If `EntryBody` present: emit directly (no class wrapper) — for `Main`/`GameEntry`
5. If `ModuleScript`: emit `return ClassName` at end

**`StatementEmitter`** — recursively walks `ILuaStatement[]`, writes Lua using `LuaWriter` indentation.

**`ExprEmitter`** — recursively walks `ILuaExpression` nodes with precedence-aware parenthesisation.

**`RuntimeBundler`** — copies `packages/*/runtime/` → `out/runtime/PackageName/` after all `.lua` files are written.

### Output Path Mapping

| C# source | Luau output |
|---|---|
| `Client/Player.cs` | `out/client/Player.lua` |
| `Server/GameManager.cs` | `out/server/GameManager.lua` |
| `Shared/Utils.cs` | `out/shared/Utils.lua` |
| `packages/ECS/runtime/` | `out/runtime/ECS/` |

Paths map directly into the existing Rojo `default.project.json` structure.

---

## 8. LUSharpAPI

LUSharpAPI is the built-in package — always loaded, no runtime folder (Roblox provides the runtime). Users interact with it as natural C#; the attribute system is an internal implementation detail invisible to game developers.

### Attribute System (Internal Only)

| Attribute | Meaning |
|---|---|
| `[LuaGlobal]` | Available everywhere, no require needed |
| `[LuaService("X")]` | Emits `game:GetService("X")` on first use |
| `[LuaPackage("X")]` | From a user package; requires path to runtime |
| `[LuaMapping("expr")]` | Exact Lua expression to emit for this call |

### Core Coverage (Phase 1)

**Classes:** `Instance`, `BasePart`, `Part`, `MeshPart`, `Model`, `Humanoid`, `Player`, `ScreenGui`, `Frame`, `TextLabel`

**Services:** `Players`, `Workspace`, `RunService`, `TweenService`, `UserInputService`, `DataStoreService`, `ReplicatedStorage`

**Types:** `Vector3`, `CFrame`, `Color3`, `UDim2`, `Ray`, `TweenInfo`

**Enums:** Existing 35+ enums kept as-is

**Globals:** `print`, `warn`, `error`, `typeof`, `pcall`, `xpcall`, `task.wait`, `task.spawn`, `task.delay`, `pairs`, `ipairs`, `table.*`, `math.*`, `string.*`

### `RBXScriptSignal<T>`

Supports both `+=` and `.Connect()` transparently:

```csharp
public class RBXScriptSignal<T>
{
    public static RBXScriptSignal<T> operator +(RBXScriptSignal<T> signal, T handler) => signal;
    public static RBXScriptSignal<T> operator -(RBXScriptSignal<T> signal, T handler) => signal;

    [LuaMapping(":Connect")]
    public RBXScriptConnection Connect(T handler) => default!;
}

public class RBXScriptConnection
{
    [LuaMapping(":Disconnect")]
    public void Disconnect() { }
}
```

---

## 9. Package System

### Package Structure

```
packages/<PackageName>/
├── src/             C# stubs with LuaMapping attributes
├── runtime/         Pre-written Luau (bundled into out/runtime/)
└── package.lusharp  Metadata
```

### `package.lusharp`

```json
{
  "name": "ECS",
  "version": "1.0.0",
  "runtimeEntry": "runtime/init.lua",
  "runtimeRequirePath": "game.ReplicatedStorage.Runtime.ECS"
}
```

### How Packages Integrate

1. `PackageLoader` runs before `SymbolCollector`, reads `lusharp.json`, loads each package's C# stubs and registers transform rules into the symbol table
2. `MethodBodyLowerer` checks the symbol table when it hits a package method call; uses the registered `LuaMapping` to emit the correct node
3. `ImportResolver` injects `local ECS = require(...)` at module top when a package type is used (deduplicated)
4. `RuntimeBundler` copies `packages/ECS/runtime/` → `out/runtime/ECS/` after emit

---

## 10. `lusharp build` CLI Command

### Interface

```bash
lusharp build                 # build current project
lusharp build --watch         # rebuild on file change
lusharp build --out ./dist    # override output directory
lusharp build --release       # enable full optimizer passes
lusharp build --target server # only build server scripts
```

### `lusharp.json`

```json
{
  "name": "MyGame",
  "version": "1.0.0",
  "packages": ["ECS", "Observers"],
  "build": {
    "src": "./src",
    "out": "./out"
  }
}
```

### Error Output

```
ERROR  src/Client/Main.cs:14   Unknown method 'GetServce' on Players — did you mean 'GetService'?
WARN   src/Shared/Utils.cs:7   No Lua type mapping for 'decimal' — defaulting to number
WARN   src/Client/Main.cs:31   async method 'FetchData' — Task<string> return ignored, emitting task.spawn
```

### `--watch` Mode

Rebuilds incrementally on file save. Reuses symbol table for unchanged files; only re-runs Transform passes for changed files. Designed to stay fast enough for a live Rojo sync workflow.

---

## 11. Current State vs. Target State

| Area | Today | Target |
|---|---|---|
| Class structure (fields, properties, statics) | Working | Keep, move to IR-driven |
| Constructors | Working | Keep, move to IR-driven |
| Property getters/setters | Working | Keep, move to IR-driven |
| Collection initializers | Working | Keep |
| Method bodies | Stubbed / commented out | MethodBodyLowerer + StatementEmitter |
| Control flow | Not started | ControlFlowLowerer |
| Events | Not started | EventBinder |
| async/await | Not started | ControlFlowLowerer |
| try/catch | Not started | ControlFlowLowerer |
| Cross-file imports | Not started | ImportResolver |
| Package system | Not started | PackageLoader + RuntimeBundler |
| `lusharp build` | Not started | Full CLI command |
| Optimizer | Stub exists | 7 optimization passes |
| File output | stdout only | ModuleEmitter writes to out/ |
| Watch mode | Not started | Incremental rebuild |
