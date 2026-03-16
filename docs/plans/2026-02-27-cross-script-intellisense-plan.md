# Cross-Script Modifier-Aware IntelliSense — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable IntelliSense to resolve user-defined types across scripts with C# access modifier visibility rules.

**Architecture:** A user type registry in IntelliSense lazily parses other scripts (via ScriptManager) when their namespaces are referenced through `using` directives. Parsed type stubs are cached by source hash and made available to the existing completion, hover, and type inference systems. Access filtering uses C# visibility rules: same-namespace sees public+internal+private, different namespace sees public only, protected only for subclasses.

**Tech Stack:** Luau (Roblox Studio plugin), existing Parser/ScriptManager/IntelliSense modules.

**Design doc:** `docs/plans/2026-02-27-cross-script-intellisense-design.md`

---

### Task 1: Add user type registry cache and stub extraction

**Files:**
- Modify: `plugin/src/IntelliSense.lua` (near top, after `symbolTable` at line 216)

**Step 1: Add the user type cache and extraction function**

After the `symbolTable` definition (line 216), add the user type registry:

```lua
-- Cross-script user type registry
-- Cached parsed type stubs from other scripts, keyed by script instance
local userTypeCache = {}  -- { [scriptInstance] = { hash = "...", namespace = "...", types = { ... } } }
local userTypesByName = {}  -- { [className] = typeStub } — flattened lookup

local function hashSource(source)
    -- Simple hash: length + checksum of first/last/middle chars
    local len = #source
    if len == 0 then return "empty" end
    local mid = math.floor(len / 2)
    return tostring(len) .. ":" .. string.byte(source, 1) .. ":" .. string.byte(source, mid) .. ":" .. string.byte(source, len)
end

local function extractTypeStubs(parseResult, namespace)
    local stubs = {}
    if not parseResult or type(parseResult) ~= "table" then return stubs end

    for _, cls in parseResult.classes or {} do
        local members = {}

        -- Constructor
        if cls.constructor then
            table.insert(members, {
                name = "new",
                kind = "constructor",
                type = nil,
                access = cls.constructor.accessModifier or "private",
                isStatic = true,
                parameters = {},
            })
            -- Extract constructor parameter info
            local params = {}
            for _, param in cls.constructor.parameters or {} do
                table.insert(params, { name = param.name, type = param.type or "Object" })
            end
            members[#members].parameters = params
        end

        -- Methods
        for _, method in cls.methods or {} do
            local params = {}
            for _, param in method.parameters or {} do
                table.insert(params, { name = param.name, type = param.type or "Object" })
            end
            table.insert(members, {
                name = method.name,
                kind = "method",
                type = method.returnType or "void",
                access = method.accessModifier or "private",
                isStatic = method.isStatic or false,
                isAsync = method.isAsync or false,
                isVirtual = method.isVirtual or false,
                isOverride = method.isOverride or false,
                isAbstract = method.isAbstract or false,
                parameters = params,
            })
        end

        -- Properties
        for _, prop in cls.properties or {} do
            table.insert(members, {
                name = prop.name,
                kind = "property",
                type = prop.propType or "Object",
                access = prop.accessModifier or "public",
                isStatic = false,
                canRead = prop.hasGet or false,
                canWrite = prop.hasSet or false,
            })
        end

        -- Fields
        for _, field in cls.fields or {} do
            table.insert(members, {
                name = field.name,
                kind = "field",
                type = field.fieldType or "Object",
                access = field.accessModifier or "private",
                isStatic = field.isStatic or false,
                isReadonly = field.isReadonly or false,
            })
        end

        local stub = {
            name = cls.name,
            fullName = (namespace and namespace ~= "") and (namespace .. "." .. cls.name) or cls.name,
            namespace = namespace or "",
            kind = "class",
            baseType = cls.baseClass or "",
            isStatic = cls.isStatic or false,
            isAbstract = cls.isAbstract or false,
            accessModifier = cls.accessModifier or "internal",
            members = members,
        }

        table.insert(stubs, stub)
    end

    -- Enums
    for _, enum in parseResult.enums or {} do
        local members = {}
        for _, val in enum.values or {} do
            table.insert(members, {
                name = val.name or val,
                kind = "field",
                type = enum.name,
                access = "public",
                isStatic = true,
            })
        end

        table.insert(stubs, {
            name = enum.name,
            fullName = (namespace and namespace ~= "") and (namespace .. "." .. enum.name) or enum.name,
            namespace = namespace or "",
            kind = "enum",
            baseType = "",
            accessModifier = enum.accessModifier or "public",
            members = members,
        })
    end

    return stubs
end
```

**Step 2: Commit**

```
feat(plugin): add user type registry cache and stub extraction
```

---

### Task 2: Add lazy script scanning and namespace resolution

**Files:**
- Modify: `plugin/src/IntelliSense.lua`

**Step 1: Add ScriptManager require at top**

After the existing requires (line 14), add:

```lua
local ScriptManager = requireModule("ScriptManager")
local Parser = requireModule("Parser")
```

Note: Check if Parser is already required. If so, skip that line.

**Step 2: Add lazy scan functions after `extractTypeStubs`**

```lua
-- Parse and cache a single script's types
local function ensureScriptCached(scriptInstance)
    local source = ScriptManager.getSource(scriptInstance)
    if type(source) ~= "string" or source == "" then return end

    local hash = hashSource(source)
    local cached = userTypeCache[scriptInstance]
    if cached and cached.hash == hash then return end

    -- Parse the script
    local tokens = Lexer.tokenize(source)
    local parseResult = Parser.parse(tokens)
    if not parseResult then return end

    -- Determine namespace from AST or attribute
    local namespace = nil
    if parseResult.namespace and parseResult.namespace.name then
        namespace = parseResult.namespace.name
    end
    if not namespace or namespace == "" then
        namespace = scriptInstance:GetAttribute("LUSharpNamespace") or ""
    end

    local stubs = extractTypeStubs(parseResult, namespace)

    -- Remove old entries from flattened lookup
    if cached then
        for _, oldStub in cached.types or {} do
            userTypesByName[oldStub.name] = nil
        end
    end

    -- Store cache and update flattened lookup
    userTypeCache[scriptInstance] = { hash = hash, namespace = namespace, types = stubs }
    for _, stub in stubs do
        userTypesByName[stub.name] = stub
    end
end

-- Scan all scripts for a given namespace (lazy — only parses uncached/changed scripts)
local function ensureNamespaceCached(targetNamespace)
    if type(targetNamespace) ~= "string" or targetNamespace == "" then return end

    local allScripts = ScriptManager.getAll()
    for _, scriptInstance in allScripts do
        -- Check attribute first (cheap) before parsing
        local scriptNamespace = scriptInstance:GetAttribute("LUSharpNamespace")
        if scriptNamespace == targetNamespace then
            ensureScriptCached(scriptInstance)
        else
            -- Parse to check AST namespace if attribute doesn't match
            local cached = userTypeCache[scriptInstance]
            if cached then
                -- Already cached — check namespace
                if cached.namespace == targetNamespace then
                    ensureScriptCached(scriptInstance) -- refresh if stale
                end
            else
                -- Not cached yet — must parse to discover namespace
                ensureScriptCached(scriptInstance)
            end
        end
    end
end

-- Resolve using directives and cache all referenced namespaces
local function ensureUsingsResolved(source, currentScriptInstance)
    -- Cache current script
    if currentScriptInstance then
        ensureScriptCached(currentScriptInstance)
    end

    -- Find all using directives in the source
    for line in (source .. "\n"):gmatch("([^\n]*)\n") do
        local namespaceName = line:match("^%s*using%s+([%a_][%w_%.]*)%s*;%s*$")
        if namespaceName then
            ensureNamespaceCached(namespaceName)
        end
    end

    -- Also cache current script's own namespace (same-namespace visibility)
    if currentScriptInstance then
        local cached = userTypeCache[currentScriptInstance]
        if cached and cached.namespace and cached.namespace ~= "" then
            ensureNamespaceCached(cached.namespace)
        end
    end
end

-- Resolve a user type by name
local function resolveUserType(typeName)
    return userTypesByName[typeName]
end
```

**Step 3: Commit**

```
feat(plugin): add lazy script scanning and namespace resolution
```

---

### Task 3: Add C# visibility filtering

**Files:**
- Modify: `plugin/src/IntelliSense.lua`

**Step 1: Add visibility check function after the lazy scan functions**

```lua
-- C# access modifier visibility check
local function isAccessible(member, accessContext)
    local access = member.access or "private"

    -- Same class sees everything
    if accessContext.sameClass then
        return true
    end

    if access == "public" then
        return true
    end

    if access == "private" then
        return false
    end

    if access == "protected" then
        return accessContext.isSubclass or false
    end

    if access == "internal" then
        return accessContext.sameNamespace or false
    end

    -- protected internal
    return (accessContext.sameNamespace or false) or (accessContext.isSubclass or false)
end

-- Build access context for filtering
local function buildAccessContext(currentSource, currentScriptInstance, targetTypeName)
    local context = {
        sameClass = false,
        sameNamespace = false,
        isSubclass = false,
    }

    -- Check if we're editing the target class itself
    local currentClassName = currentSource:match("class%s+(" .. targetTypeName .. ")%s*[%{%:]")
    if currentClassName then
        context.sameClass = true
        return context
    end

    -- Determine current namespace
    local currentNamespace = ""
    if currentScriptInstance then
        local cached = userTypeCache[currentScriptInstance]
        if cached then
            currentNamespace = cached.namespace or ""
        end
    end
    -- Also try parsing from source
    if currentNamespace == "" then
        currentNamespace = currentSource:match("^%s*namespace%s+([%a_][%w_%.]*)")  or ""
    end

    -- Determine target namespace
    local targetStub = userTypesByName[targetTypeName]
    local targetNamespace = targetStub and targetStub.namespace or ""

    -- Same namespace check
    if currentNamespace ~= "" and targetNamespace ~= "" and currentNamespace == targetNamespace then
        context.sameNamespace = true
    end

    -- Subclass check: see if current class extends target type
    local currentBaseClass = currentSource:match("class%s+[%a_][%w_]*%s*:%s*([%a_][%w_]*)")
    if currentBaseClass and currentBaseClass == targetTypeName then
        context.isSubclass = true
    end

    return context
end
```

**Step 2: Commit**

```
feat(plugin): add C# visibility filtering for cross-script IntelliSense
```

---

### Task 4: Integrate user types into resolveType and collectMembers

**Files:**
- Modify: `plugin/src/IntelliSense.lua:320-351`

**Step 1: Update `resolveType` to check user types**

Replace the `resolveType` function (line 320-328):

```lua
local function resolveType(typeName)
    local normalized = normalizeTypeName(typeName)
    if not normalized then
        return nil
    end

    local fullName = TypeDatabase.aliases[normalized] or MANUAL_TYPE_ALIASES[normalized] or normalized
    local resolved = TypeDatabase.types[fullName] or MANUAL_TYPES[fullName]
    if resolved then return resolved end

    -- Check user-defined types
    return userTypesByName[normalized]
end
```

**Step 2: Commit**

```
feat(plugin): integrate user types into type resolution
```

---

### Task 5: Add access filtering to getMemberCompletions

**Files:**
- Modify: `plugin/src/IntelliSense.lua:924-947`

**Step 1: Update `getMemberCompletions` to accept and use access context**

Replace the `getMemberCompletions` function (lines 924-947):

```lua
local function getMemberCompletions(typeName, prefix, accessContext)
    local members = {}
    collectMembers(typeName, nil, members)

    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, member in members do
        local label = member.name
        if type(label) == "string" and label ~= "" then
            -- Apply access filtering for user-defined types
            local shouldInclude = true
            if accessContext and member.access then
                shouldInclude = isAccessible(member, accessContext)
            end

            if shouldInclude and (normalizedPrefix == "" or string.sub(string.lower(label), 1, #normalizedPrefix) == normalizedPrefix) then
                addUniqueCompletion(result, seen, makeCompletion(
                    label,
                    memberKind(member),
                    memberDetail(member),
                    buildMemberDocumentation(typeName, member)
                ))
            end
        end
    end

    return sortAndLimit(result, 100)
end
```

**Step 2: Commit**

```
feat(plugin): add access modifier filtering to member completions
```

---

### Task 6: Add static vs instance filtering to member completions

**Files:**
- Modify: `plugin/src/IntelliSense.lua`

**Step 1: Add static filtering parameter to `getMemberCompletions`**

Update the function signature and add static filtering:

```lua
local function getMemberCompletions(typeName, prefix, accessContext, staticOnly)
    local members = {}
    collectMembers(typeName, nil, members)

    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, member in members do
        local label = member.name
        if type(label) == "string" and label ~= "" then
            -- Apply access filtering for user-defined types
            local shouldInclude = true
            if accessContext and member.access then
                shouldInclude = isAccessible(member, accessContext)
            end

            -- Apply static filtering
            if shouldInclude and staticOnly ~= nil then
                if staticOnly then
                    shouldInclude = member.isStatic == true or member.kind == "constructor"
                else
                    shouldInclude = not member.isStatic
                end
            end

            if shouldInclude and (normalizedPrefix == "" or string.sub(string.lower(label), 1, #normalizedPrefix) == normalizedPrefix) then
                addUniqueCompletion(result, seen, makeCompletion(
                    label,
                    memberKind(member),
                    memberDetail(member),
                    buildMemberDocumentation(typeName, member)
                ))
            end
        end
    end

    return sortAndLimit(result, 100)
end
```

**Step 2: Commit**

```
feat(plugin): add static vs instance filtering to member completions
```

---

### Task 7: Wire up cross-script resolution in getCompletions

**Files:**
- Modify: `plugin/src/IntelliSense.lua:2318-2350` and the dot-completion section

**Step 1: Pass `currentScript` through opts from init.server.lua**

In `plugin/src/init.server.lua`, update the three `opts` objects to include `currentScript`:

For `getCompletions` (line 490):
```lua
    local completions = IntelliSense.getCompletions(editor:getSource(), cursorPos, {
        context = context,
        currentScript = currentScript,
        visibleServices = settingsValues and settingsValues.intellisenseVisibleServices,
        validityProfile = getActiveValidityProfile(),
    })
```

For `getHoverInfo` (line 523):
```lua
    return IntelliSense.getHoverInfo(source, cursorPos, {
        context = context,
        currentScript = currentScript,
        searchNearby = true,
        nearbyRadius = 20,
        diagnostics = diagnostics,
        validityProfile = getActiveValidityProfile(),
    })
```

For `getDiagnostics` (line 304):
```lua
    local diagnostics = IntelliSense.getDiagnostics(parseResult, source, {
        validityProfile = validityProfile,
        currentScript = currentScript,
    })
```

**Step 2: In `IntelliSense.getCompletions`, trigger using resolution and pass context**

At the beginning of `IntelliSense.getCompletions` (after line 2323), add:

```lua
    -- Resolve cross-script user types from using directives
    local currentScriptInstance = opts.currentScript
    ensureUsingsResolved(source, currentScriptInstance)
```

**Step 3: Update the dot-completion section to use access context**

Find the section in `getCompletions` where `getMemberCompletions` is called for dot access (where `inferMemberAccessType` is used). After inferring the type, check if it's a user type and build access context:

After the `inferMemberAccessType` call, update the `getMemberCompletions` call to pass access context:

```lua
    local memberType, memberPrefix = inferMemberAccessType(tokens, symbolTypes)
    if memberType then
        local accessCtx = nil
        local isStaticAccess = nil
        if resolveUserType(memberType) then
            accessCtx = buildAccessContext(source, currentScriptInstance, memberType)
        end
        -- Check if this is a static access (ClassName. vs instance.)
        -- If the identifier before the dot matches a known type name, it's static access
        local beforeDot = left:match("([%a_][%w_]*)%s*%.%s*$")
        if beforeDot and resolveUserType(beforeDot) then
            isStaticAccess = true
            memberType = beforeDot
        end
        return getMemberCompletions(memberType, memberPrefix, accessCtx, isStaticAccess)
    end
```

**Step 4: Commit**

```
feat(plugin): wire cross-script type resolution into completions
```

---

### Task 8: Add user types to type completions (new ClassName)

**Files:**
- Modify: `plugin/src/IntelliSense.lua:949-980`

**Step 1: Update `getTypeCompletions` to include user-defined types**

After the existing loops in `getTypeCompletions` (after DOTNET_BASE_TYPES loop), add:

```lua
    -- Include user-defined types
    for typeName, stub in userTypesByName do
        if stub.kind == "class" or stub.kind == "struct" or stub.kind == "enum" then
            local isConstructable = stub.kind == "class" or stub.kind == "struct"
            if (not constructableOnly or isConstructable) then
                -- Only show types accessible from current context
                local typeAccess = stub.accessModifier or "internal"
                if typeAccess == "public" or typeAccess == "internal" then
                    if normalizedPrefix == "" or string.sub(string.lower(typeName), 1, #normalizedPrefix) == normalizedPrefix then
                        addUniqueCompletion(result, seen, makeCompletion(
                            typeName,
                            "type",
                            stub.kind,
                            stub.namespace ~= "" and ("User type from " .. stub.namespace) or "User type"
                        ))
                    end
                end
            end
        end
    end
```

**Step 2: Commit**

```
feat(plugin): include user-defined types in type completions
```

---

### Task 9: Add user types to hover info resolution

**Files:**
- Modify: `plugin/src/IntelliSense.lua` (in `resolveHoverInfoAtPosition`)

**Step 1: In `resolveHoverInfoAtPosition`, add user type hover resolution**

After the existing type hover checks (around the `buildTypeHoverInfo` call), add a fallback for user types. Find the section where it checks `buildTypeHoverInfo(identifier)` and add after it:

```lua
    -- Check user-defined types
    local userStub = resolveUserType(identifier)
    if userStub then
        local detail = userStub.kind .. " " .. identifier
        if userStub.namespace and userStub.namespace ~= "" then
            detail = detail .. " (namespace " .. userStub.namespace .. ")"
        end
        return {
            label = identifier,
            kind = userStub.kind,
            detail = detail,
            documentation = nil,
        }
    end
```

Also, when hovering over members of user types (in the dot-access section), the existing `findMemberInType` → `buildHoverInfoFromMember` flow should now work since `resolveType` checks user types (from Task 4).

**Step 2: Also trigger using resolution in `getHoverInfo`**

At the beginning of `IntelliSense.getHoverInfo` (after opts parsing), add:

```lua
    local currentScriptInstance = opts.currentScript
    ensureUsingsResolved(source, currentScriptInstance)
```

**Step 3: Commit**

```
feat(plugin): add user type hover info resolution
```

---

### Task 10: Add cache invalidation function

**Files:**
- Modify: `plugin/src/IntelliSense.lua` (near the bottom, after `resetSymbolTable`)

**Step 1: Add public cache management functions**

```lua
function IntelliSense.invalidateUserTypeCache(scriptInstance)
    if scriptInstance then
        local cached = userTypeCache[scriptInstance]
        if cached then
            for _, stub in cached.types or {} do
                userTypesByName[stub.name] = nil
            end
        end
        userTypeCache[scriptInstance] = nil
    end
end

function IntelliSense.clearUserTypeCache()
    userTypeCache = {}
    userTypesByName = {}
end
```

**Step 2: Commit**

```
feat(plugin): add user type cache invalidation
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
feat(plugin): cross-script modifier-aware IntelliSense with C# visibility rules
```
