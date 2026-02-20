# Script Execution & Luau Runtime Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix transpiler output so scripts actually run in Roblox, and add Luau runtime utilities for OOP, events, and imports.

**Architecture:** Three transpiler bugs (file extensions, ScriptType detection, missing entry call) prevent scripts from executing. Fix those in TransformPipeline and ModuleEmitter. Then add three Luau runtime files (Class, Signal, Import) as embedded resources, copied to `out/runtime/` during build.

**Tech Stack:** C# / .NET 9, xUnit, Roslyn, Luau

---

### Task 1: Fix DeriveOutputPath File Extensions

**Files:**
- Modify: `LUSharpTranspiler/Transform/TransformPipeline.cs:78-85`
- Test: `LUSharpTests/TransformPipelineTests.cs` (new)

**Step 1: Write the failing tests**

Create `LUSharpTests/TransformPipelineTests.cs`:

```csharp
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class TransformPipelineTests
{
    private static LuaModule RunPipeline(string csharp, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(csharp);
        var pipeline = new TransformPipeline();
        var modules = pipeline.Run(new() { new ParsedFile(filePath, tree) });
        return modules[0];
    }

    [Theory]
    [InlineData("Server/ServerMain.cs", "server/ServerMain.server.lua")]
    [InlineData("Client/ClientMain.cs", "client/ClientMain.client.lua")]
    [InlineData("Shared/SharedModule.cs", "shared/SharedModule.lua")]
    public void DeriveOutputPath_UsesCorrectExtension(string inputPath, string expectedOutput)
    {
        var module = RunPipeline("public class Foo {}", inputPath);
        Assert.Equal(expectedOutput, module.OutputPath);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter TransformPipelineTests -v n`
Expected: FAIL — server gets `.lua` not `.server.lua`

**Step 3: Fix DeriveOutputPath**

In `LUSharpTranspiler/Transform/TransformPipeline.cs`, replace `DeriveOutputPath`:

```csharp
private static string DeriveOutputPath(string filePath)
{
    var name = Path.GetFileNameWithoutExtension(filePath);
    if (filePath.Contains("Client")) return $"client/{name}.client.lua";
    if (filePath.Contains("Server")) return $"server/{name}.server.lua";
    return $"shared/{name}.lua";
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter TransformPipelineTests -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add LUSharpTests/TransformPipelineTests.cs LUSharpTranspiler/Transform/TransformPipeline.cs
git commit -m "fix: use .server.lua/.client.lua extensions for Rojo script type detection"
```

---

### Task 2: Fix ScriptType Determination

**Files:**
- Modify: `LUSharpTranspiler/Transform/TransformPipeline.cs:38-69`
- Test: `LUSharpTests/TransformPipelineTests.cs` (add tests)

**Step 1: Write the failing tests**

Add to `TransformPipelineTests.cs`:

```csharp
[Fact]
public void ServerScript_GetsScriptType()
{
    var module = RunPipeline(
        "public class ServerMain { public void GameEntry() { } }",
        "Server/ServerMain.cs");
    Assert.Equal(ScriptType.Script, module.ScriptType);
}

[Fact]
public void ClientScript_GetsLocalScriptType()
{
    var module = RunPipeline(
        "public class ClientMain { public void GameEntry() { } }",
        "Client/ClientMain.cs");
    Assert.Equal(ScriptType.LocalScript, module.ScriptType);
}

[Fact]
public void SharedModule_GetsModuleScriptType()
{
    var module = RunPipeline(
        "public class SharedUtils { public static int Add(int a, int b) { return a + b; } }",
        "Shared/SharedUtils.cs");
    Assert.Equal(ScriptType.ModuleScript, module.ScriptType);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter TransformPipelineTests -v n`
Expected: FAIL — `ServerMain` gets `ModuleScript` instead of `Script`

**Step 3: Fix Pass 3 in TransformPipeline.Run()**

Replace the Pass 3 section (lines 38-69) in `TransformPipeline.cs`:

```csharp
// Pass 3: determine ScriptType from symbol table (any class in this file)
var scriptType = ScriptType.ModuleScript;
foreach (var cls in classes)
{
    var sym = _symbols.LookupClass(cls.Name);
    if (sym != null && sym.ScriptType != ScriptType.ModuleScript)
    {
        scriptType = sym.ScriptType;
        break;
    }
}

modules.Add(new LuaModule
{
    SourceFile = f.FilePath,
    OutputPath = DeriveOutputPath(f.FilePath),
    ScriptType = scriptType,
    Classes = classes,
    EntryBody = new List<ILuaStatement>()
});
```

Note: We remove the `GameEntry()` extraction from Pass 3. The entry call will be emitted by the backend in Task 3 instead — the method stays on the class and gets called at the bottom. This is simpler than extracting the body.

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter TransformPipelineTests -v n`
Expected: PASS

**Step 5: Run full test suite**

Run: `dotnet test -v n`
Expected: All tests pass. Some existing tests may need adjustment if they relied on `EntryBody` extraction — check and fix.

**Step 6: Commit**

```bash
git add LUSharpTranspiler/Transform/TransformPipeline.cs LUSharpTests/TransformPipelineTests.cs
git commit -m "fix: determine ScriptType from actual class symbols, not hardcoded Main"
```

---

### Task 3: Emit GameEntry() Call for Script/LocalScript

**Files:**
- Modify: `LUSharpTranspiler/Backend/ModuleEmitter.cs:10-36`
- Test: `LUSharpTests/ModuleEmitterTests.cs` (add tests)

**Step 1: Write the failing tests**

Add to `LUSharpTests/ModuleEmitterTests.cs`:

```csharp
[Fact]
public void ServerScript_CallsGameEntry()
{
    var module = new LuaModule
    {
        ScriptType = ScriptType.Script,
        Classes = new()
        {
            new LuaClassDef
            {
                Name = "ServerMain",
                Methods = new()
                {
                    new LuaMethodDef
                    {
                        Name = "GameEntry",
                        Parameters = new() { "self" },
                        Body = new() { new LuaExprStatement(
                            new LuaCall(new LuaIdent("print"),
                                new() { new LuaLiteral("\"Server starting\"") })) }
                    }
                }
            }
        }
    };

    var result = ModuleEmitter.Emit(module);

    Assert.Contains("function ServerMain:GameEntry()", result);
    Assert.Contains("ServerMain:GameEntry()", result);
    Assert.DoesNotContain("return ServerMain", result);
}

[Fact]
public void LocalScript_CallsGameEntry()
{
    var module = new LuaModule
    {
        ScriptType = ScriptType.LocalScript,
        Classes = new()
        {
            new LuaClassDef
            {
                Name = "ClientMain",
                Methods = new()
                {
                    new LuaMethodDef
                    {
                        Name = "GameEntry",
                        Parameters = new() { "self" },
                        Body = new() { new LuaExprStatement(
                            new LuaCall(new LuaIdent("print"),
                                new() { new LuaLiteral("\"Client starting\"") })) }
                    }
                }
            }
        }
    };

    var result = ModuleEmitter.Emit(module);

    Assert.Contains("ClientMain:GameEntry()", result);
    Assert.DoesNotContain("return ClientMain", result);
}

[Fact]
public void ModuleScript_DoesNotCallGameEntry()
{
    var module = new LuaModule
    {
        ScriptType = ScriptType.ModuleScript,
        Classes = new()
        {
            new LuaClassDef
            {
                Name = "Utils",
                Methods = new()
                {
                    new LuaMethodDef
                    {
                        Name = "GameEntry",
                        Parameters = new() { "self" },
                        Body = new()
                    }
                }
            }
        }
    };

    var result = ModuleEmitter.Emit(module);

    Assert.Contains("return Utils", result);
    // The "GameEntry()" in the function definition shouldn't be confused with a call
    var lines = result.Split('\n').Select(l => l.Trim()).ToList();
    Assert.DoesNotContain("Utils:GameEntry()", lines.Where(l => !l.StartsWith("function")));
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ModuleEmitterTests -v n`
Expected: FAIL — no `ServerMain:GameEntry()` call emitted

**Step 3: Modify ModuleEmitter.Emit()**

In `LUSharpTranspiler/Backend/ModuleEmitter.cs`, replace the module return section (after entry body emission):

```csharp
// Entry call for Script/LocalScript — call GameEntry() on the first class that has it
if (module.ScriptType != ScriptType.ModuleScript)
{
    var entryClass = module.Classes.FirstOrDefault(c =>
        c.Methods.Any(m => m.Name == "GameEntry"));
    if (entryClass != null)
        w.WriteLine($"{entryClass.Name}:GameEntry()");
}

// Module return (ModuleScript only)
if (module.ScriptType == ScriptType.ModuleScript && module.Classes.Count > 0)
    w.WriteLine($"return {module.Classes[0].Name}");
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ModuleEmitterTests -v n`
Expected: PASS

**Step 5: Run full test suite**

Run: `dotnet test -v n`
Expected: All pass

**Step 6: Commit**

```bash
git add LUSharpTranspiler/Backend/ModuleEmitter.cs LUSharpTests/ModuleEmitterTests.cs
git commit -m "fix: emit GameEntry() call at bottom of Script/LocalScript output"
```

---

### Task 4: Create Runtime Luau Files

**Files:**
- Create: `LUSharp/Runtime/Class.lua`
- Create: `LUSharp/Runtime/Signal.lua`
- Create: `LUSharp/Runtime/Import.lua`
- Modify: `LUSharp/LUSharp.csproj` (embed as resources)

**Step 1: Create `LUSharp/Runtime/Class.lua`**

```lua
-- LUSharp Runtime: Class System
-- Provides OOP support via metatables

local Class = {}

function Class.new(name, base)
    local cls = {}
    cls.__index = cls
    cls.__name = name

    if base then
        setmetatable(cls, { __index = base })
    end

    function cls.new(...)
        local self = setmetatable({}, cls)
        if cls._constructor then
            cls._constructor(self, ...)
        end
        return self
    end

    return cls
end

return Class
```

**Step 2: Create `LUSharp/Runtime/Signal.lua`**

```lua
-- LUSharp Runtime: Signal (Event System)
-- Lightweight replacement for BindableEvents

local Signal = {}
Signal.__index = Signal

function Signal.new()
    return setmetatable({ _handlers = {} }, Signal)
end

function Signal:Connect(fn)
    table.insert(self._handlers, fn)
    return {
        Disconnect = function()
            local idx = table.find(self._handlers, fn)
            if idx then table.remove(self._handlers, idx) end
        end
    }
end

function Signal:Fire(...)
    for _, fn in self._handlers do
        fn(...)
    end
end

function Signal:Wait()
    local co = coroutine.running()
    local conn
    conn = self:Connect(function(...)
        conn.Disconnect()
        coroutine.resume(co, ...)
    end)
    return coroutine.yield()
end

return Signal
```

**Step 3: Create `LUSharp/Runtime/Import.lua`**

```lua
-- LUSharp Runtime: Import Helper
-- Safe require() wrapper with error messages

local Import = {}

function Import.require(path)
    local ok, result = pcall(require, path)
    if not ok then
        warn("[LUSharp] Failed to require: " .. tostring(path) .. "\n" .. tostring(result))
        error(result)
    end
    return result
end

return Import
```

**Step 4: Register as embedded resources in `LUSharp/LUSharp.csproj`**

Add to the existing `<Compile Remove>` ItemGroup:

```xml
<Compile Remove="Runtime\Class.lua" />
<Compile Remove="Runtime\Signal.lua" />
<Compile Remove="Runtime\Import.lua" />
```

Add to the existing `<EmbeddedResource>` ItemGroup:

```xml
<EmbeddedResource Include="Runtime\Class.lua" />
<EmbeddedResource Include="Runtime\Signal.lua" />
<EmbeddedResource Include="Runtime\Import.lua" />
```

**Step 5: Verify build succeeds**

Run: `dotnet build`
Expected: 0 errors

**Step 6: Commit**

```bash
git add LUSharp/Runtime/Class.lua LUSharp/Runtime/Signal.lua LUSharp/Runtime/Import.lua LUSharp/LUSharp.csproj
git commit -m "feat: add Luau runtime files (Class, Signal, Import) as embedded resources"
```

---

### Task 5: Copy Runtime Files During Build

**Files:**
- Modify: `LUSharpTranspiler/Build/BuildCommand.cs`
- Modify: `LUSharp/Project/ProjectScaffolder.cs` (extract runtime copy helper)

**Step 1: Add CopyRuntime method to ProjectScaffolder**

Add to `LUSharp/Project/ProjectScaffolder.cs`:

```csharp
internal static void CopyRuntimeFiles(string outDir)
{
    var runtimeDir = Path.Combine(outDir, "runtime");
    Directory.CreateDirectory(runtimeDir);

    var asm = Assembly.GetExecutingAssembly();
    var runtimeFiles = new[] { "Class.lua", "Signal.lua", "Import.lua" };

    foreach (var file in runtimeFiles)
    {
        var resourceName = $"LUSharp.Runtime.{file}";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.Log(Logger.LogSeverity.Warning, $"Runtime resource '{resourceName}' not found, skipping.");
            continue;
        }
        using var reader = new StreamReader(stream);
        File.WriteAllText(Path.Combine(runtimeDir, file), reader.ReadToEnd());
    }
}
```

**Step 2: Call CopyRuntime from BuildCommand**

In `LUSharpTranspiler/Build/BuildCommand.cs`, after the `Console.WriteLine($"Building {config.Name}...")` line (around line 24), add:

```csharp
// Copy runtime Luau files to out/runtime/
LUSharp.Project.ProjectScaffolder.CopyRuntimeFiles(outDir);
```

Note: `BuildCommand` is in `LUSharpTranspiler` which references `LUSharp`. Check the project reference direction — if `LUSharpTranspiler` can't call `LUSharp.Project.ProjectScaffolder`, then move `CopyRuntimeFiles` to a shared location or have `BuildCommand` accept a delegate. Check `LUSharpTranspiler.csproj` for project references.

If `LUSharpTranspiler` does NOT reference `LUSharp`, instead add the runtime copy logic directly in `BuildCommand.cs` using `Assembly.GetEntryAssembly()` to find the embedded resources from the CLI assembly. Alternative: pass the runtime files as a parameter from `Program.cs`.

**Step 3: Verify build works end-to-end**

Run:
```bash
dotnet run --project LUSharp -- build "C:\Users\table\Documents\TestProject\AgarioRoblox"
ls "C:\Users\table\Documents\TestProject\AgarioRoblox\out\runtime\"
```
Expected: `Class.lua`, `Signal.lua`, `Import.lua` present in `out/runtime/`

**Step 4: Run full test suite**

Run: `dotnet test -v n`
Expected: All pass

**Step 5: Commit**

```bash
git add LUSharp/Project/ProjectScaffolder.cs LUSharpTranspiler/Build/BuildCommand.cs
git commit -m "feat: copy Luau runtime files to out/runtime/ during build"
```

---

### Task 6: Update ProjectFixer for Runtime Files

**Files:**
- Modify: `LUSharp/ProjectFixer.cs`

**Step 1: Add runtime check to ProjectFixer.Run()**

After the `CheckApiDll` call, add:

```csharp
Report(CheckRuntimeFiles(projectDir), "out/runtime/ files", ref fixed_, ref warnings);
```

**Step 2: Implement CheckRuntimeFiles**

Add to `ProjectFixer.cs`:

```csharp
private static (CheckStatus, string) CheckRuntimeFiles(string dir)
{
    var runtimeDir = Path.Combine(dir, "out", "runtime");
    var expected = new[] { "Class.lua", "Signal.lua", "Import.lua" };
    var missing = expected.Where(f => !File.Exists(Path.Combine(runtimeDir, f))).ToList();

    if (missing.Count == 0)
        return (CheckStatus.Ok, "");

    ProjectScaffolder.CopyRuntimeFiles(Path.Combine(dir, "out"));

    // Verify they were copied
    var stillMissing = expected.Where(f => !File.Exists(Path.Combine(runtimeDir, f))).ToList();
    if (stillMissing.Count == 0)
        return (CheckStatus.Fixed, $"copied {missing.Count} file(s)");

    return (CheckStatus.Warn, $"could not copy: {string.Join(", ", stillMissing)}");
}
```

**Step 3: Verify fix command works**

Run:
```bash
dotnet run --project LUSharp -- fix "C:\Users\table\Documents\TestProject\AgarioRoblox"
```
Expected: Shows `[OK]` or `[FIXED]` for `out/runtime/ files`

**Step 4: Run full test suite**

Run: `dotnet test -v n`
Expected: All pass

**Step 5: Commit**

```bash
git add LUSharp/ProjectFixer.cs
git commit -m "feat: add runtime file check to lusharp fix command"
```

---

### Task 7: End-to-End Verification

**Step 1: Rebuild the test project**

```bash
dotnet run --project LUSharp -- build "C:\Users\table\Documents\TestProject\AgarioRoblox"
```

**Step 2: Verify output file extensions**

```bash
ls C:\Users\table\Documents\TestProject\AgarioRoblox\out\server\
ls C:\Users\table\Documents\TestProject\AgarioRoblox\out\client\
ls C:\Users\table\Documents\TestProject\AgarioRoblox\out\shared\
ls C:\Users\table\Documents\TestProject\AgarioRoblox\out\runtime\
```

Expected:
- `out/server/ServerMain.server.lua`
- `out/client/ClientMain.client.lua`
- `out/shared/SharedModule.lua`
- `out/runtime/Class.lua`, `Signal.lua`, `Import.lua`

**Step 3: Verify server script calls GameEntry**

Read `out/server/ServerMain.server.lua`. Should end with:
```lua
ServerMain:GameEntry()
```
NOT `return ServerMain`.

**Step 4: Verify shared module returns itself**

Read `out/shared/SharedModule.lua`. Should end with:
```lua
return SharedModule
```
NOT contain any `GameEntry()` call.

**Step 5: Verify runtime files are valid Luau**

Read each file in `out/runtime/` — should be the exact content from the embedded resources.

**Step 6: Run lusharp fix**

```bash
dotnet run --project LUSharp -- fix "C:\Users\table\Documents\TestProject\AgarioRoblox"
```

Expected: All `[OK]`, no `[FIXED]` or `[WARN]` (except possibly the MSBuild target warning).

**Step 7: Final commit**

Only if any fixups were needed:
```bash
git add -A && git commit -m "fix: end-to-end verification fixes"
```
