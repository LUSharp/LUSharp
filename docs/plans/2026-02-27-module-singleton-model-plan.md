# Module/Singleton Model Redesign — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Overhaul the plugin transpiler to emit ModuleScript singletons with service hoisting, smart requires, circular dep guards, and fix auto-indent/namespace bugs.

**Architecture:** Every script becomes a ModuleScript returning its class table. A lazy loader runtime script discovers, requires, instantiates, and runs all modules. The Lowerer hoists GetService calls, emits require() for cross-script deps, and wraps output in a circular dependency guard. Bug fixes for auto-indent (brace depth), namespace diagnostics, and hover resolution.

**Tech Stack:** Luau (Roblox Studio plugin), CollectionService for discovery.

**Design doc:** `docs/plans/2026-02-27-module-singleton-model-design.md`

---

### Task 1: Rename GameEntry to Main — ScriptManager templates

**Files:**
- Modify: `plugin/src/ScriptManager.lua:12-26`

**Step 1: Update source templates**

Replace lines 12-26:

```lua
local SOURCE_TEMPLATE = [[public class %s {
    public static void Main() {

    }
}
]]

local SOURCE_TEMPLATE_WITH_NAMESPACE = [[namespace %s {
public class %s {
    public static void Main() {

    }
}
}
]]
```

**Step 2: Commit**

```
feat(plugin): rename GameEntry to Main in script templates
```

---

### Task 2: Rename GameEntry to Main — Lowerer entry detection

**Files:**
- Modify: `plugin/src/Lowerer.lua:1555-1575`

**Step 1: Remove `determineScriptType` and simplify `findEntryClass`**

Replace lines 1555-1575:

```lua
-- Find the entry class (one with Main method)
local function findEntryClass(classNodes)
    for _, cls in classNodes do
        for _, method in cls.methods or {} do
            if method.name == "Main" then
                return cls.name
            end
        end
    end
    return nil
end
```

**Step 2: Remove SCRIPT_BASES constant**

Delete lines 10-15 (`local SCRIPT_BASES = { ... }`).

**Step 3: Update `Lowerer.lower` — remove scriptType determination**

In `Lowerer.lower` (around line 1602-1603), remove the `determineScriptType` call. The `scriptType` is already defaulted to `"ModuleScript"` at line 1585, which is now always correct.

```lua
-- DELETE this line:
-- module.scriptType = determineScriptType(ast.classes or {})
```

**Step 4: Commit**

```
feat(plugin): rename GameEntry to Main in Lowerer, always ModuleScript
```

---

### Task 3: Update Emitter — always return class table

**Files:**
- Modify: `plugin/src/Emitter.lua:315-349`

**Step 1: Simplify `Emitter.emit` to always return class table**

Replace the script type branching (lines 327-346) with:

```lua
function Emitter.emit(moduleIR)
    local lines = {}

    if moduleIR.needsEventConnectionCache then
        appendLine(lines, 0, "local __eventConnections = setmetatable({}, { __mode = \"k\" })")
        appendLine(lines, 0, "")
    end

    -- Emit service locals (hoisted, always first after event cache)
    for _, svc in moduleIR.services or {} do
        appendLine(lines, 0, "local " .. svc.name .. " = game:GetService(\"" .. svc.name .. "\")")
    end
    if moduleIR.services and #moduleIR.services > 0 then
        appendLine(lines, 0, "")
    end

    -- Emit requires
    for _, req in moduleIR.requires or {} do
        appendLine(lines, 0, "local " .. req.name .. " = require(" .. req.path .. ")")
    end
    if moduleIR.requires and #moduleIR.requires > 0 then
        appendLine(lines, 0, "")
    end

    -- Emit classes
    for _, cls in moduleIR.classes or {} do
        emitClass(cls, lines)
    end

    -- Always return the entry class (or first class)
    local returnClass = moduleIR.entryClass
    if not returnClass and moduleIR.classes and moduleIR.classes[1] then
        returnClass = moduleIR.classes[1].name
    end
    if returnClass then
        appendLine(lines, 0, "return " .. returnClass)
    end

    return table.concat(lines, "\n")
end
```

**Step 2: Commit**

```
feat(plugin): Emitter always returns class table, emits services and requires
```

---

### Task 4: Add circular dependency guard to Emitter

**Files:**
- Modify: `plugin/src/Emitter.lua`

**Step 1: Emit guard preamble at top of module**

In `Emitter.emit`, after building `lines = {}`, add the guard before everything else:

```lua
function Emitter.emit(moduleIR)
    local lines = {}

    -- Circular dependency guard
    local guardName = moduleIR.entryClass
    if not guardName and moduleIR.classes and moduleIR.classes[1] then
        guardName = moduleIR.classes[1].name
    end
    if guardName then
        appendLine(lines, 0, "if _G.__LUSharp_Loading == nil then _G.__LUSharp_Loading = {} end")
        appendLine(lines, 0, "if _G.__LUSharp_Loading[\"" .. guardName .. "\"] then return _G.__LUSharp_Loading[\"" .. guardName .. "\"] end")
        appendLine(lines, 0, "_G.__LUSharp_Loading[\"" .. guardName .. "\"] = {}")
        appendLine(lines, 0, "")
    end

    -- ... rest of emit (event cache, services, requires, classes) ...

    -- Before the final return, register the real table
    if guardName and returnClass then
        appendLine(lines, 0, "_G.__LUSharp_Loading[\"" .. guardName .. "\"] = " .. returnClass)
    end
    appendLine(lines, 0, "return " .. returnClass)

    return table.concat(lines, "\n")
end
```

**Step 2: Commit**

```
feat(plugin): add circular dependency guard to emitted modules
```

---

### Task 5: Service hoisting optimization pass in Lowerer

**Files:**
- Modify: `plugin/src/Lowerer.lua`

**Step 1: Add service collection function**

Add before `Lowerer.lower`:

```lua
-- Collect all game:GetService("X") calls from IR and return deduplicated service names
local function collectServiceCalls(stmts, found)
    found = found or {}
    if type(stmts) ~= "table" then return found end

    for _, stmt in stmts do
        collectServiceCallsFromExpr(stmt, found)
    end
    return found
end

local function collectServiceCallsFromExpr(node, found)
    if type(node) ~= "table" then return end

    -- Match: game:GetService("ServiceName") or game.GetService("ServiceName")
    if node.type == "call" or node.type == "method_call" then
        local isGetService = false
        local serviceName = nil

        if node.type == "method_call" and node.method == "GetService" then
            if node.object and node.object.type == "identifier" and node.object.name == "game" then
                isGetService = true
            end
        elseif node.type == "call" and node.callee then
            if node.callee.type == "dot_access" and node.callee.field == "GetService" then
                if node.callee.object and node.callee.object.type == "identifier" and node.callee.object.name == "game" then
                    isGetService = true
                end
            end
        end

        if isGetService and node.args and #node.args >= 1 then
            local firstArg = node.args[1]
            if firstArg.type == "literal" and type(firstArg.value) == "string" then
                serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
            end
        end

        if serviceName and serviceName ~= "" and not found[serviceName] then
            found[serviceName] = true
        end
    end

    -- Recurse into all table children
    for k, v in node do
        if type(v) == "table" then
            collectServiceCallsFromExpr(v, found)
        end
    end
end

-- Replace game:GetService("X") nodes in IR with identifier "X"
local function replaceServiceCalls(node)
    if type(node) ~= "table" then return node end

    -- Check if this node is a GetService call
    local isGetService = false
    local serviceName = nil

    if node.type == "method_call" and node.method == "GetService" then
        if node.object and node.object.type == "identifier" and node.object.name == "game" then
            isGetService = true
        end
    elseif node.type == "call" and node.callee then
        if node.callee.type == "dot_access" and node.callee.field == "GetService" then
            if node.callee.object and node.callee.object.type == "identifier" and node.callee.object.name == "game" then
                isGetService = true
            end
        end
    end

    if isGetService and node.args and #node.args >= 1 then
        local firstArg = node.args[1]
        if firstArg.type == "literal" and type(firstArg.value) == "string" then
            serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
        end
    end

    if serviceName then
        return { type = "identifier", name = serviceName }
    end

    -- Recurse
    for k, v in node do
        if type(v) == "table" then
            node[k] = replaceServiceCalls(v)
        end
    end
    return node
end
```

**Step 2: Integrate into `Lowerer.lower`**

After lowering classes and before returning, add:

```lua
    -- Service hoisting pass
    local serviceSet = {}
    for _, cls in module.classes do
        for _, method in cls.methods or {} do
            collectServiceCallsFromExpr(method, serviceSet)
        end
        if cls.constructor then
            collectServiceCallsFromExpr(cls.constructor, serviceSet)
        end
    end

    -- Build sorted service list for IR
    local services = {}
    for name in serviceSet do
        table.insert(services, { name = name })
    end
    table.sort(services, function(a, b) return a.name < b.name end)
    module.services = services

    -- Replace GetService calls with identifiers
    for _, cls in module.classes do
        for _, method in cls.methods or {} do
            replaceServiceCalls(method)
        end
        if cls.constructor then
            replaceServiceCalls(cls.constructor)
        end
    end
```

**Step 3: Commit**

```
feat(plugin): add service hoisting optimization pass to Lowerer
```

---

### Task 6: Fix auto-indent — always use brace depth

**Files:**
- Modify: `plugin/src/EditorTextUtils.lua:399-418`

**Step 1: Replace indent logic**

Replace lines 399-418:

```lua
    local tokens = Lexer.tokenize(newText:sub(1, insertedNewlinePos - 1))
    local depth = 0
    for _, token in tokens do
        if token.type == "punctuation" then
            if token.value == "{" then
                depth += 1
            elseif token.value == "}" then
                depth = math.max(0, depth - 1)
            end
        end
    end

    local desiredIndent = string.rep(tabText, depth)
```

This removes the special-case branching (`trimmed:sub(-1) == "{"` vs `trimmed == ""`) and always computes indent from brace depth. Works uniformly for namespace, class, method, foreach, and any other `{` nesting.

**Step 2: Commit**

```
fix(plugin): auto-indent always uses brace depth for consistent nesting
```

---

### Task 7: Fix namespace diagnostic — `using System;` false error

**Files:**
- Modify: `plugin/src/IntelliSense.lua:1128-1138`

**Step 1: Add explicit namespace key registration**

In `getUsingNamespaceNames`, after iterating `DOTNET_NAMESPACE_MEMBERS` (line 1128), the loop calls `addNamespaceWithParents` on each key which should add `System`. The bug is that `addNamespaceWithParents` iterates segments using `gmatch("[%a_][%w_]*")` and builds progressively — for `System` (single segment) it adds `System`. For `System.Collections` it adds `System` then `System.Collections`. This should work.

Check if the issue is that `DOTNET_NAMESPACE_MEMBERS` iteration order causes `System` to only be registered through child namespaces. The actual bug may be that `using System;` is checked against `knownNamespaces` (line 3248) which uses `getUsingNamespaceNames(opts.validityProfile)`. If `opts.validityProfile` is a table with an empty `namespaces` field, it takes the validityProfile branch (line 1100) which recurses into `getUsingNamespaceNames(nil)` — this should still return `System`.

Look more carefully: line 3253 pattern is `^%s*using%s+([%a_][%w_%.]*)%s*;%s*$`. This matches `using System;` correctly, extracting `System`. Then checks `knownNamespaces[string.lower("System")]` = `knownNamespaces["system"]`. The `addNamespaceWithParents` at line 1091 stores `string.lower(current)` as key. So `system` should be in `seen`.

The real issue may be timing: `USING_NAMESPACE_NAMES` is cached at line 1148. If the first call happens before `TypeDatabase` is loaded or `DOTNET_NAMESPACE_MEMBERS` is populated, the cache is empty. Or the `validityProfile` path may have a bug.

**Fix:** Add explicit `Game` namespace family to `DOTNET_NAMESPACE_MEMBERS` so user namespaces (`Game.Server`, `Game.Client`, `Game.Shared`) are always known. Also clear the cache when recomputing:

In `getUsingNamespaceNames`, after the base namespace loop, add user-defined namespace roots:

```lua
    -- Add LUSharp user namespace roots
    addNamespaceWithParents(out, seen, "Game")
    addNamespaceWithParents(out, seen, "Game.Server")
    addNamespaceWithParents(out, seen, "Game.Client")
    addNamespaceWithParents(out, seen, "Game.Shared")
```

Also add to `DOTNET_NAMESPACE_MEMBERS`:

```lua
DOTNET_NAMESPACE_MEMBERS["Game"] = {
    { label = "Server", kind = "namespace", detail = "namespace", documentation = "Game.Server" },
    { label = "Client", kind = "namespace", detail = "namespace", documentation = "Game.Client" },
    { label = "Shared", kind = "namespace", detail = "namespace", documentation = "Game.Shared" },
}
```

**Step 2: Commit**

```
fix(plugin): register Game namespace family, fix using System false error
```

---

### Task 8: Fix hover resolution for dot-paths (Game.Server)

**Files:**
- Modify: `plugin/src/IntelliSense.lua:2882-2905`

**Step 1: Add namespace path resolution to hover**

In `resolveHoverInfoAtPosition`, after the existing member access check (line 2882-2905), add namespace resolution before returning nil:

```lua
    if beforeTrimmed:sub(-1) == "." then
        -- ... existing member resolution code ...

        -- Namespace path resolution: collect full dot-path
        local fullPath = identifier
        local scanBack = beforeTrimmed:sub(1, -2)  -- strip trailing dot
        while true do
            local prevSegment = scanBack:match("([%a_][%w_]*)%s*$")
            if not prevSegment then break end
            fullPath = prevSegment .. "." .. fullPath
            scanBack = scanBack:sub(1, -(#prevSegment + 1)):gsub("%s*%.%s*$", "")
            if scanBack == "" or not scanBack:match("%.$") then
                -- Check if what's before is also a dot access
                if not scanBack:gsub("%s+$", ""):match("%.$") then
                    break
                end
            end
        end

        -- Check if fullPath is a known namespace
        local knownNamespaces = getUsingNamespaceNames(opts and opts.validityProfile)
        for _, ns in knownNamespaces do
            if string.lower(ns) == string.lower(fullPath) then
                return {
                    label = fullPath,
                    kind = "namespace",
                    detail = "namespace " .. fullPath,
                    documentation = nil,
                }
            end
        end
    end
```

**Step 2: Commit**

```
fix(plugin): resolve full dot-path for namespace hover info
```

---

### Task 9: Create lazy loader runtime script

**Files:**
- Create: `plugin/src/LUSharpLoader.lua`

**Step 1: Write the loader**

```lua
-- LUSharp Lazy Loader
-- Discovers, requires, instantiates, and runs all LUSharp ModuleScripts

local CollectionService = game:GetService("CollectionService")

local TAG = "LUSharp"

local _modules = {}
local _registry = {}

-- Phase 1+2: Discover and require all tagged ModuleScripts
local tagged = CollectionService:GetTagged(TAG)
for _, instance in tagged do
    if instance:IsA("ModuleScript") then
        local ok, classTable = pcall(require, instance)
        if ok and type(classTable) == "table" then
            _modules[instance.Name] = classTable
        else
            warn("[LUSharp] Failed to load module: " .. instance:GetFullName())
        end
    end
end

-- Phase 3: Instantiate singletons
for name, classTable in _modules do
    if type(classTable.new) == "function" then
        local ok, instance = pcall(classTable.new)
        if ok then
            _registry[name] = instance
        else
            warn("[LUSharp] Failed to instantiate: " .. name)
        end
    else
        _registry[name] = classTable
    end
end

-- Phase 4: Call Main() on each singleton that has it
for name, instance in _registry do
    if type(instance.Main) == "function" then
        local ok, err = pcall(function()
            instance:Main()
        end)
        if not ok then
            warn("[LUSharp] Error in " .. name .. ":Main() — " .. tostring(err))
        end
    end
end
```

**Step 2: Add to Rojo project**

Modify `plugin/default.project.json` to map the loader into the runtime location (e.g., `ReplicatedStorage/LUSharp/Loader`). The exact mapping depends on the existing Rojo config.

**Step 3: Commit**

```
feat(plugin): add LUSharp lazy loader runtime script
```

---

### Task 10: Add require generation to Lowerer

**Files:**
- Modify: `plugin/src/Lowerer.lua`

**Step 1: Add require resolution to `Lowerer.lower`**

After the service hoisting pass, add require generation. The Lowerer needs access to `ast.usings` and the identifiers used in method bodies to determine which classes need to be required.

Add before `Lowerer.lower`:

```lua
-- Collect all identifiers referenced in IR
local function collectReferencedIdentifiers(node, found)
    if type(node) ~= "table" then return end
    found = found or {}

    if node.type == "identifier" and node.name then
        found[node.name] = true
    end
    if node.type == "new_object" and node.class then
        local className = tostring(node.class):gsub("<.*>", ""):match("([%w_]+)$") or ""
        if className ~= "" then
            found[className] = true
        end
    end

    for k, v in node do
        if type(v) == "table" then
            collectReferencedIdentifiers(v, found)
        end
    end
    return found
end
```

In `Lowerer.lower`, after the service pass:

```lua
    -- Require generation pass
    -- Only emit requires for identifiers that are:
    -- 1. Not defined in this module (not a local class or enum)
    -- 2. Referenced in method bodies
    -- 3. Could be a class from an imported namespace
    local localNames = {}
    for _, cls in module.classes do
        localNames[cls.name] = true
    end
    for _, enum in module.enums do
        localNames[enum.name] = true
    end

    local referenced = {}
    for _, cls in module.classes do
        collectReferencedIdentifiers(cls, referenced)
    end

    local requires = {}
    for name in referenced do
        if not localNames[name] and name:sub(1, 1):match("[A-Z]") then
            -- Looks like an external class reference (PascalCase, not local)
            -- Skip known globals and services
            if not serviceSet[name] and name ~= "game" and name ~= "script"
                and name ~= "workspace" and name ~= "print" and name ~= "warn"
                and name ~= "error" and name ~= "type" and name ~= "typeof"
                and name ~= "tostring" and name ~= "tonumber" and name ~= "pcall"
                and name ~= "table" and name ~= "string" and name ~= "math"
                and name ~= "Instance" and name ~= "Enum" and name ~= "Vector3"
                and name ~= "CFrame" and name ~= "Color3" and name ~= "UDim2"
                and name ~= "UDim" and name ~= "BrickColor" and name ~= "Ray"
                and name ~= "Region3" and name ~= "TweenInfo" and name ~= "task"
                and name ~= "coroutine" and name ~= "setmetatable" then
                table.insert(requires, { name = name })
            end
        end
    end
    table.sort(requires, function(a, b) return a.name < b.name end)
    module.requires = requires
```

**Step 2: Update Emitter to emit require paths**

The Emitter already has the `requires` emission from Task 3. Update the require path to use `FindFirstChild` with the `recursive` flag:

```lua
    -- Emit requires
    for _, req in moduleIR.requires or {} do
        -- Use script.Parent as the search root for sibling modules
        appendLine(lines, 0, "local " .. req.name .. " = require(script.Parent:FindFirstChild(\"" .. req.name .. "\", true))")
    end
```

**Step 3: Commit**

```
feat(plugin): add smart require generation for cross-script dependencies
```

---

### Task 11: Build and install plugin

**Step 1: Build plugin**

```bash
rojo build plugin/plugin.project.json -o plugin/LUSharp-plugin.rbxmx
```

**Step 2: Copy to Roblox Plugins**

```bash
cp -f plugin/LUSharp-plugin.rbxmx /c/Users/table/AppData/Local/Roblox/Plugins/LUSharp-plugin.rbxmx
```

**Step 3: Verify artifact**

```bash
head -5 /c/Users/table/AppData/Local/Roblox/Plugins/LUSharp-plugin.rbxmx
```

Expected: `<Item class="Script"` with `name="LUSharp"`

**Step 4: Commit all changes**

```
feat(plugin): module/singleton model with service hoisting, smart requires, and bug fixes
```
