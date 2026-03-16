# Explicit Call Resolution & Static/Instance Validation — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix the `.` vs `:` call style ambiguity in the plugin transpiler so static method calls emit dot syntax and instance method calls emit colon syntax, with CS0176/CS0120 compiler errors when misused.

**Architecture:** Add a pre-scan symbol table pass to `Lowerer.lower()` that collects all class method/field signatures before lowering individual classes. During expression lowering, consult the symbol table to determine call style. Collect diagnostics in the IR result. Update IntelliSense to use C# error codes. Block builds on lowerer errors.

**Tech Stack:** Lua (Roblox Studio plugin), Parser AST, Lowerer IR, Emitter

---

### Task 1: Build module symbol table in Lowerer

**Files:**
- Modify: `plugin/src/Lowerer.lua:1665-1761` (the `Lowerer.lower()` function)

**Step 1: Add `buildModuleSymbols` function**

Insert before `Lowerer.lower` (around line 1664):

```lua
local function buildModuleSymbols(classNodes)
    local symbols = {}
    for _, cls in classNodes or {} do
        local classSym = { methods = {}, fields = {} }

        for _, method in cls.methods or {} do
            classSym.methods[method.name] = {
                isStatic = method.isStatic or false,
            }
        end

        for _, field in cls.fields or {} do
            classSym.fields[field.name] = {
                isStatic = field.isStatic or false,
                fieldType = field.fieldType,
            }
        end

        -- Constructor is always instance-level
        if cls.constructor then
            classSym.methods["new"] = { isStatic = true }
        end

        symbols[cls.name] = classSym
    end
    return symbols
end
```

**Step 2: Call it at the top of `Lowerer.lower`**

Inside `Lowerer.lower`, after `local module = { ... }` (line 1678), add:

```lua
local moduleSymbols = buildModuleSymbols(ast.classes)
```

**Step 3: Verify**

Run: `rojo build "plugin/plugin.project.json" -o "plugin/LUSharp-plugin.rbxmx"`
Expected: Builds without errors. No behavioral change yet — symbol table is built but not consumed.

---

### Task 2: Add local variable type tracking to lowerStatement

**Files:**
- Modify: `plugin/src/Lowerer.lua:771-788` (the `local_var` lowering in `lowerStatement`)

**Step 1: Thread `localTypes` through lowering context**

The Lowerer needs a shared context object accessible during lowering. Add an upvalue near the top of the lowering scope (inside `Lowerer.lower`, before class lowering begins):

```lua
local loweringContext = {
    moduleSymbols = moduleSymbols,
    localTypes = {},
    diagnostics = {},
}
```

**Step 2: Track variable types in `lowerStatement` for `local_var`**

At `plugin/src/Lowerer.lua:783-788`, after the `local_decl` node is created, add type tracking:

```lua
-- Track type for call resolution
if stmt.varType and stmt.varType ~= "var" then
    loweringContext.localTypes[stmt.name] = normalizeTypeName(stmt.varType)
elseif init and init.type == "new" and init.className then
    loweringContext.localTypes[stmt.name] = normalizeTypeName(init.className)
end
```

Also handle `var` with `new()` — when `stmt.varType == "var"` and the initializer is `new ClassName()`:

```lua
elseif stmt.varType == "var" and init and init.type == "new" and init.className then
    loweringContext.localTypes[stmt.name] = normalizeTypeName(init.className)
```

**Step 3: Verify**

Build plugin. No behavioral change — types are tracked but not consumed yet.

---

### Task 3: Resolve call style in `lowerExpression` for cross-class calls

**Files:**
- Modify: `plugin/src/Lowerer.lua:481-493` (the "Regular method call" section)

**Step 1: Replace the blind `method_call` creation**

Replace lines 481-493:

```lua
-- Regular method call on an object: obj.method(args)
if expr.target then
    local args = {}
    for _, arg in expr.arguments do
        table.insert(args, lowerExpression(arg))
    end
    return {
        type = "method_call",
        object = lowerExpression(expr.target),
        method = expr.name,
        args = args,
    }
end
```

With call-style-aware resolution:

```lua
-- Regular method call on an object: obj.method(args)
if expr.target then
    local args = {}
    for _, arg in expr.arguments do
        table.insert(args, lowerExpression(arg))
    end

    local targetName = expr.target.type == "identifier" and expr.target.name or nil
    local methodName = expr.name
    local resolved = false

    if targetName and loweringContext.moduleSymbols[targetName] then
        -- Target is a class name → static access (e.g., ClassName.Method())
        local classSym = loweringContext.moduleSymbols[targetName]
        local methodSym = classSym.methods[methodName]

        if methodSym then
            if methodSym.isStatic then
                -- Correct: static method called on class name → dot syntax
                resolved = true
                return {
                    type = "call",
                    callee = {
                        type = "dot_access",
                        object = { type = "identifier", name = targetName },
                        field = methodName,
                    },
                    args = args,
                }
            else
                -- CS0120: instance method called on class name
                table.insert(loweringContext.diagnostics, {
                    severity = "error",
                    code = "CS0120",
                    message = "An object reference is required for the non-static field, method, or property '" .. methodName .. "'",
                    target = targetName,
                    member = methodName,
                })
            end
        end
    elseif targetName and loweringContext.localTypes[targetName] then
        -- Target is a local variable with known type
        local varType = loweringContext.localTypes[targetName]
        local classSym = loweringContext.moduleSymbols[varType]

        if classSym then
            local methodSym = classSym.methods[methodName]
            if methodSym then
                if methodSym.isStatic then
                    -- CS0176: static method called on instance
                    table.insert(loweringContext.diagnostics, {
                        severity = "error",
                        code = "CS0176",
                        message = "Static member '" .. methodName .. "' cannot be accessed with an instance reference; qualify it with a type name instead",
                        target = targetName,
                        member = methodName,
                    })
                else
                    -- Correct: instance method on instance → colon syntax
                    resolved = true
                    return {
                        type = "method_call",
                        object = lowerExpression(expr.target),
                        method = methodName,
                        args = args,
                    }
                end
            end
        end
    end

    -- Fallback: unknown target type → default to method_call (colon)
    -- This covers Roblox API calls and cross-script types
    if not resolved then
        return {
            type = "method_call",
            object = lowerExpression(expr.target),
            method = methodName,
            args = args,
        }
    end
end
```

**Step 2: Verify with examples**

Given `example2.cs`:
```csharp
NewScript newscript = new();
newscript.SomeFunc();  // SomeFunc is static on NewScript
```

If `NewScript` and `NewScript2` are in the same script, the Lowerer would:
1. See `newscript` has type `NewScript` (from local_var tracking)
2. Look up `SomeFunc` in `moduleSymbols["NewScript"]`
3. Find `isStatic = true` → emit CS0176 error

For cross-script calls (different scripts), `moduleSymbols` won't have the class → falls back to `method_call` (colon), same as current behavior.

**Step 3: Verify**

Build plugin. Test with a single-script example containing both a class and a call to its static method from another class.

---

### Task 4: Return diagnostics from Lowerer.lower

**Files:**
- Modify: `plugin/src/Lowerer.lua:1756-1761` (end of `Lowerer.lower`)

**Step 1: Attach diagnostics to IR result**

Before `return ir` at the end of `Lowerer.lower`, add:

```lua
ir.diagnostics = loweringContext.diagnostics
```

**Step 2: Verify**

Build plugin. The IR now carries diagnostics but nothing consumes them yet.

---

### Task 5: Block build on Lowerer diagnostics in init.server.lua

**Files:**
- Modify: `plugin/src/init.server.lua:674-676` (after `Lowerer.lower` call in `compileOne`)

**Step 1: Add diagnostic check after lowering**

After line 674 (`local ir = Lowerer.lower(parseResult)`), insert:

```lua
-- Check for lowerer diagnostics (CS0176, CS0120, etc.)
if ir.diagnostics and #ir.diagnostics > 0 then
    warn("[LUSharp] Lowerer diagnostics in " .. scriptInstance:GetFullName())
    for _, diagnostic in ir.diagnostics do
        warn(string.format("  %s: %s", diagnostic.code or "error", diagnostic.message))
    end
    projectView:setScriptBuildStatus(scriptInstance, {
        ok = false,
        errors = #ir.diagnostics + errorCount,
        dirty = true,
    })
    return false, "lowerer_errors"
end
```

**Step 2: Verify**

Build plugin with a script that has a static/instance mismatch. The build should be refused with the CS0176 or CS0120 error in the output log.

---

### Task 6: Update IntelliSense diagnostics to use CS0176/CS0120

**Files:**
- Modify: `plugin/src/IntelliSense.lua:4766-4801` (inside `collectInvalidUserMemberAccessDiagnostics`)

**Step 1: Split the "not found" diagnostic into three cases**

Replace the current block at lines 4766-4801 that does a single `found` check with three distinct checks:

```lua
-- Check if member exists at all
local memberExists = false
local memberIsStatic = nil
local members = {}
collectMembers(objectType, nil, members)
for _, member in members do
    if member.name == memberName then
        memberExists = true
        memberIsStatic = member.isStatic or false
        break
    end
end

if not memberExists then
    -- Member doesn't exist on this type
    local line = tonumber(memberToken.line) or 1
    local column = tonumber(memberToken.column) or 1
    local length = #memberName
    table.insert(diagnostics, {
        severity = "error",
        message = "'" .. objectType .. "' does not contain a definition for '" .. memberName .. "'",
        line = line,
        column = column,
        endLine = line,
        endColumn = column + length,
        length = length,
        code = "CS1061",
    })
elseif isStaticAccess and not memberIsStatic and memberName ~= "new" then
    -- CS0120: class name used to access instance member
    local line = tonumber(memberToken.line) or 1
    local column = tonumber(memberToken.column) or 1
    local length = #memberName
    table.insert(diagnostics, {
        severity = "error",
        message = "An object reference is required for the non-static field, method, or property '" .. memberName .. "'",
        line = line,
        column = column,
        endLine = line,
        endColumn = column + length,
        length = length,
        code = "CS0120",
    })
elseif (not isStaticAccess) and memberIsStatic then
    -- CS0176: instance used to access static member
    local line = tonumber(memberToken.line) or 1
    local column = tonumber(memberToken.column) or 1
    local length = #memberName
    table.insert(diagnostics, {
        severity = "error",
        message = "Static member '" .. memberName .. "' cannot be accessed with an instance reference; qualify it with a type name instead",
        line = line,
        column = column,
        endLine = line,
        endColumn = column + length,
        length = length,
        code = "CS0176",
    })
end
```

**Step 2: Verify**

Build plugin and install. Open a script with `instance.StaticMethod()` — should show red squiggle with CS0176 message. Open a script with `ClassName.InstanceMethod()` — should show red squiggle with CS0120 message.

---

### Task 7: Thread loweringContext through the lowering pipeline

**Files:**
- Modify: `plugin/src/Lowerer.lua` (multiple locations)

This is the plumbing task that makes Tasks 2-4 actually work. The `lowerExpression` and `lowerStatement` functions are currently standalone — they need access to `loweringContext`.

**Step 1: Make loweringContext accessible**

The cleanest approach: declare `loweringContext` as a module-level upvalue that gets set at the start of `Lowerer.lower()` and cleared at the end:

```lua
-- At module level (near top of file, after local declarations)
local loweringContext = nil
```

Then in `Lowerer.lower()`:

```lua
function Lowerer.lower(ast)
    local moduleSymbols = buildModuleSymbols(ast.classes)

    loweringContext = {
        moduleSymbols = moduleSymbols,
        localTypes = {},
        diagnostics = {},
    }

    -- ... existing lowering code ...

    local ir = { modules = {} }
    -- ... etc ...

    ir.diagnostics = loweringContext.diagnostics
    loweringContext = nil  -- cleanup

    return ir
end
```

**Step 2: Reset localTypes per class/method scope**

In `lowerClass` (line 1419), before lowering methods, reset `localTypes` to avoid leaking types from a previous class:

```lua
-- Inside lowerClass, before method lowering loop
if loweringContext then
    loweringContext.localTypes = {}
end
```

Also reset per method in the method lowering loop (line 1474), before `lowerBlock(method.body)`:

```lua
if loweringContext then
    loweringContext.localTypes = {}
    -- Track method parameters as locals (no type info, but prevents false matches)
    for _, p in method.parameters or {} do
        -- Parameters don't have reliable type info from Parser, so just mark as known
    end
end
```

**Step 3: Verify**

Build plugin. The full pipeline is now connected: symbol table → type tracking → call resolution → diagnostics → build blocking.

---

### Task 8: Handle field type tracking for class-level fields

**Files:**
- Modify: `plugin/src/Lowerer.lua:1430-1441` (field lowering in `lowerClass`)

**Step 1: Track field types in loweringContext**

In the field lowering loop inside `lowerClass`, after creating the IR field, add type tracking:

```lua
for _, field in classNode.fields or {} do
    local irField = {
        name = field.name,
        value = lowerFieldInitializer(field.initializer),
    }
    if field.isStatic then
        table.insert(cls.staticFields, irField)
    else
        table.insert(cls.instanceFields, irField)
    end

    -- Track field type for call resolution
    if loweringContext and field.fieldType then
        local normalizedType = normalizeTypeName(field.fieldType)
        if normalizedType and normalizedType ~= "" then
            loweringContext.localTypes[field.name] = normalizedType
        end
    end
end
```

This handles cases like:
```csharp
NewScript2 newScript = new();  // field declaration
```

Where `newScript` is used later in method bodies like `newScript.SomeFunc()`.

**Step 2: Verify**

Build plugin. Fields with explicit types are now tracked, enabling call resolution for field access patterns.

---

### Task 9: End-to-end verification

**Files:**
- No modifications — verification only

**Step 1: Build and install plugin**

```bash
rojo build "plugin/plugin.project.json" -o "plugin/LUSharp-plugin.rbxmx"
cp -f "plugin/LUSharp-plugin.rbxmx" "/c/Users/table/AppData/Local/Roblox/Plugins/LUSharp-plugin.rbxmx"
```

Verify: First lines must include `<Item class="Script"`.

**Step 2: Test static call resolution**

Create a script with two classes in the same script:
```csharp
namespace Game.Server {
    public class Helper {
        public static void DoStuff() { print("static"); }
        public void DoInstanceStuff() { print("instance"); }
    }
    public class Main {
        public static void Main() {
            Helper helper = new();
            Helper.DoStuff();        // should emit: Helper.DoStuff()
            helper.DoInstanceStuff(); // should emit: helper:DoInstanceStuff()
        }
    }
}
```

Expected output:
```luau
Helper.DoStuff()
helper:DoInstanceStuff()
```

**Step 3: Test CS0176 error**

```csharp
Helper helper = new();
helper.DoStuff();  // CS0176: static member on instance
```

Expected: Build refused with CS0176 error. Editor shows red squiggle.

**Step 4: Test CS0120 error**

```csharp
Helper.DoInstanceStuff();  // CS0120: instance member on class name
```

Expected: Build refused with CS0120 error. Editor shows red squiggle.

**Step 5: Test Roblox API unaffected**

```csharp
var players = game.GetService("Players");
players.PlayerAdded.Connect((Player p) => { print("joined"); });
```

Expected: Roblox calls still emit with `:` syntax. No errors.
