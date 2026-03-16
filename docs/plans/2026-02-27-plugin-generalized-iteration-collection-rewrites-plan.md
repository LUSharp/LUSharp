# Plugin: Generalized Iteration & Collection Rewrites — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove all `pairs()`/`ipairs()` from the plugin Lua source and make the Lowerer emit zero-overhead collection code for user C# (no wrappers — plain Lua tables with native table ops).

**Architecture:** Two areas of change: (1) mechanical find-replace of `pairs()`/`ipairs()` across all plugin `.lua` files, (2) add collection-aware rewriting to `Lowerer.lua` so user C# collection types (`List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `IEnumerable<T>`, `IList<T>`, `ICollection<T>`, `IDictionary<K,V>`) transpile to plain tables with method calls rewritten to native Lua equivalents.

**Tech Stack:** Luau (Roblox Studio plugin), no external dependencies.

**Design doc:** `docs/plans/2026-02-27-plugin-generalized-iteration-collection-rewrites-design.md`

---

### Task 1: Remove `pairs()`/`ipairs()` from plugin source — Lowerer.lua

**Files:**
- Modify: `plugin/src/Lowerer.lua`

**Step 1: Replace all `ipairs()` calls**

Replace every `in ipairs(...)` with `in ...` in Lowerer.lua. There are ~30 occurrences. Examples:

```lua
-- BEFORE
for _, member in ipairs(typeInfo.members) do
for _, arg in ipairs(expr.arguments) do
for _, field in ipairs(classNode.fields or {}) do

-- AFTER
for _, member in typeInfo.members do
for _, arg in expr.arguments do
for _, field in classNode.fields or {} do
```

Note: when stripping `ipairs(...)`, the parentheses that belonged to `ipairs` are removed too, but the inner expression's own parens (if any) stay. For `ipairs(expr.arguments or {})`, the result is `expr.arguments or {}` — no wrapping parens needed since `in` has low precedence.

**Step 2: Replace all `pairs()` calls**

Replace `in pairs(...)` with `in ...`. There is 1 occurrence:

```lua
-- BEFORE (line 998)
for k, v in pairs(set or {}) do

-- AFTER
for k, v in set or {} do
```

**Step 3: Fix the `foreach` codegen (line 817-828)**

```lua
-- BEFORE
if t == "foreach" then
    return {
        type = "for_in",
        vars = { "_", stmt.variable },
        iterator = {
            type = "call",
            callee = { type = "identifier", name = "pairs" },
            args = { lowerExpression(stmt.iterable) },
        },
        body = lowerBlock(stmt.body),
    }
end

-- AFTER
if t == "foreach" then
    return {
        type = "for_in",
        vars = { "_", stmt.variable },
        iterator = lowerExpression(stmt.iterable),
        body = lowerBlock(stmt.body),
    }
end
```

**Step 4: Commit**

```
feat(plugin): remove pairs/ipairs from Lowerer, use generalized iteration
```

---

### Task 2: Remove `pairs()`/`ipairs()` from plugin source — Emitter.lua

**Files:**
- Modify: `plugin/src/Emitter.lua`

**Step 1: Replace all `ipairs()` calls**

~10 occurrences. Same mechanical replacement as Task 1.

```lua
-- BEFORE
for _, arg in ipairs(expr.args or {}) do
for _, element in ipairs(expr.elements or {}) do
for _, stmt in ipairs(stmts or {}) do
for _, field in ipairs(cls.staticFields or {}) do
for _, field in ipairs(cls.instanceFields or {}) do
for _, method in ipairs(cls.methods or {}) do
for _, cls in ipairs(moduleIR.classes or {}) do
for _, m in ipairs(classIR.methods or {}) do

-- AFTER (strip ipairs wrapper)
for _, arg in expr.args or {} do
for _, element in expr.elements or {} do
for _, stmt in stmts or {} do
for _, field in cls.staticFields or {} do
for _, field in cls.instanceFields or {} do
for _, method in cls.methods or {} do
for _, cls in moduleIR.classes or {} do
for _, m in classIR.methods or {} do
```

**Step 2: Commit**

```
feat(plugin): remove ipairs from Emitter, use generalized iteration
```

---

### Task 3: Remove `pairs()`/`ipairs()` from plugin source — Editor.lua

**Files:**
- Modify: `plugin/src/Editor.lua`

**Step 1: Replace all `ipairs()` calls**

~12 occurrences. Same pattern.

**Step 2: Commit**

```
feat(plugin): remove ipairs from Editor, use generalized iteration
```

---

### Task 4: Remove `pairs()`/`ipairs()` from plugin source — EditorTextUtils.lua

**Files:**
- Modify: `plugin/src/EditorTextUtils.lua`

**Step 1: Replace `ipairs()` calls** (6 occurrences)

**Step 2: Replace `pairs()` calls** (3 occurrences)

**Step 3: Commit**

```
feat(plugin): remove pairs/ipairs from EditorTextUtils, use generalized iteration
```

---

### Task 5: Remove `pairs()`/`ipairs()` from plugin source — IntelliSense.lua

**Files:**
- Modify: `plugin/src/IntelliSense.lua`

**Step 1: Replace `ipairs()` calls** (~50 occurrences)

**Step 2: Replace `pairs()` calls** (~12 occurrences)

**Step 3: Commit**

```
feat(plugin): remove pairs/ipairs from IntelliSense, use generalized iteration
```

---

### Task 6: Remove `pairs()`/`ipairs()` from remaining plugin files

**Files:**
- Modify: `plugin/src/init.server.lua`
- Modify: `plugin/src/ProjectView.lua`
- Modify: `plugin/src/ScriptManager.lua`
- Modify: `plugin/src/SyntaxHighlighter.lua`
- Modify: `plugin/src/Settings.lua`
- Modify: `plugin/src/Lexer.lua`

**Step 1: Replace all `pairs()` and `ipairs()` calls in each file**

Same mechanical replacement. Counts per file:
- `init.server.lua`: 5 ipairs
- `ProjectView.lua`: 7 ipairs
- `ScriptManager.lua`: 2 ipairs
- `SyntaxHighlighter.lua`: 4 ipairs, 2 pairs
- `Settings.lua`: 6 ipairs, 4 pairs
- `Lexer.lua`: 1 ipairs

**Step 2: Commit**

```
feat(plugin): remove pairs/ipairs from remaining plugin files
```

---

### Task 7: Add collection type detection to Lowerer

**Files:**
- Modify: `plugin/src/Lowerer.lua` (add near top, after `UNARY_OP_MAP`)

**Step 1: Add the `COLLECTION_TYPES` lookup table**

Add after the `UNARY_OP_MAP` block (around line 57):

```lua
-- Collection types that map to plain Lua tables (no wrappers)
local COLLECTION_TYPES = {
    List = "array",
    ["IList"] = "array",
    ["ICollection"] = "array",
    ["IEnumerable"] = "array",
    Queue = "array",
    Stack = "array",
    Dictionary = "dict",
    ["IDictionary"] = "dict",
    HashSet = "set",
}

local function getCollectionKind(typeName)
    if type(typeName) ~= "string" then
        return nil
    end
    local normalized = normalizeTypeName(typeName)
    return COLLECTION_TYPES[normalized]
end
```

**Step 2: Commit**

```
feat(plugin): add collection type detection table to Lowerer
```

---

### Task 8: Rewrite `new` for collection types

**Files:**
- Modify: `plugin/src/Lowerer.lua` (the `new` handler, lines 305-319)

**Step 1: Add collection-aware `new` rewriting**

Replace the existing `new` handler:

```lua
-- new ClassName(args)
if t == "new" then
    -- Collection types → plain empty table or table with initializer
    local collectionKind = getCollectionKind(expr.className)
    if collectionKind then
        -- new List<T>() → {}
        -- new List<T> { 1, 2, 3 } → { 1, 2, 3 }
        -- new Dictionary<K,V> { {"a", 1} } → { a = 1 } (initializer already lowered)
        local elements = {}
        for _, item in expr.initializer or {} do
            table.insert(elements, lowerExpression(item))
        end
        return {
            type = "array_literal",
            elements = elements,
        }
    end

    local args = {}
    for _, arg in expr.arguments or {} do
        table.insert(args, lowerExpression(arg))
    end
    for _, item in expr.initializer or {} do
        table.insert(args, lowerExpression(item))
    end
    return {
        type = "new_object",
        class = expr.className,
        args = args,
    }
end
```

**Step 2: Commit**

```
feat(plugin): rewrite new List/Dictionary/etc to plain table literals
```

---

### Task 9: Rewrite collection method calls

**Files:**
- Modify: `plugin/src/Lowerer.lua` (the `call` handler, around lines 248-284)

**Step 1: Add collection method rewriting function**

Add before the `lowerExpression` function:

```lua
local function rewriteCollectionCall(methodName, objectExpr, args)
    -- Array-like: List, Queue, IList, ICollection, IEnumerable
    -- list.Add(x) → table.insert(list, x)
    if methodName == "Add" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end

    -- list.Remove(x) → table.remove(list, table.find(list, x))
    if methodName == "Remove" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = {
                objectExpr,
                {
                    type = "call",
                    callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                    args = { objectExpr, args[1] },
                },
            },
        }
    end

    -- list.RemoveAt(i) → table.remove(list, i + 1)
    if methodName == "RemoveAt" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = {
                objectExpr,
                { type = "binary_op", op = "+", left = args[1], right = { type = "literal", value = 1 } },
            },
        }
    end

    -- list.Insert(i, x) → table.insert(list, i + 1, x)
    if methodName == "Insert" and #args == 2 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = {
                objectExpr,
                { type = "binary_op", op = "+", left = args[1], right = { type = "literal", value = 1 } },
                args[2],
            },
        }
    end

    -- list.Contains(x) → table.find(list, x) ~= nil
    if methodName == "Contains" and #args == 1 then
        return {
            type = "binary_op",
            op = "~=",
            left = {
                type = "call",
                callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                args = { objectExpr, args[1] },
            },
            right = { type = "literal", value = "nil" },
        }
    end

    -- list.Clear() → table.clear(list)
    if methodName == "Clear" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "clear" },
            args = { objectExpr },
        }
    end

    -- list.IndexOf(x) → (table.find(list, x) or 0) - 1
    if methodName == "IndexOf" and #args == 1 then
        return {
            type = "binary_op",
            op = "-",
            left = {
                type = "binary_op",
                op = "or",
                left = {
                    type = "call",
                    callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                    args = { objectExpr, args[1] },
                },
                right = { type = "literal", value = 0 },
            },
            right = { type = "literal", value = 1 },
        }
    end

    return nil -- not a collection method
end

local function rewriteDictCall(methodName, objectExpr, args)
    -- dict.Add(key, val) → dict[key] = val (statement-level, return assignment)
    if methodName == "Add" and #args == 2 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = args[2],
        }
    end

    -- dict.Remove(key) → dict[key] = nil
    if methodName == "Remove" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "nil" },
        }
    end

    -- dict.ContainsKey(key) → dict[key] ~= nil
    if methodName == "ContainsKey" and #args == 1 then
        return "expression", {
            type = "binary_op",
            op = "~=",
            left = { type = "index_access", object = objectExpr, key = args[1] },
            right = { type = "literal", value = "nil" },
        }
    end

    return nil, nil
end

local function rewriteSetCall(methodName, objectExpr, args)
    -- set.Add(x) → set[x] = true
    if methodName == "Add" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "true" },
        }
    end

    -- set.Remove(x) → set[x] = nil
    if methodName == "Remove" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "nil" },
        }
    end

    -- set.Contains(x) → set[x] ~= nil
    if methodName == "Contains" and #args == 1 then
        return "expression", {
            type = "binary_op",
            op = "~=",
            left = { type = "index_access", object = objectExpr, key = args[1] },
            right = { type = "literal", value = "nil" },
        }
    end

    return nil, nil
end

local function rewriteQueueCall(methodName, objectExpr, args)
    -- queue.Enqueue(x) → table.insert(queue, x)
    if methodName == "Enqueue" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end

    -- queue.Dequeue() → table.remove(queue, 1)
    if methodName == "Dequeue" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = { objectExpr, { type = "literal", value = 1 } },
        }
    end

    -- queue.Peek() → queue[1]
    if methodName == "Peek" and #args == 0 then
        return {
            type = "index_access",
            object = objectExpr,
            key = { type = "literal", value = 1 },
        }
    end

    return nil
end

local function rewriteStackCall(methodName, objectExpr, args)
    -- stack.Push(x) → table.insert(stack, x)
    if methodName == "Push" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end

    -- stack.Pop() → table.remove(stack)
    if methodName == "Pop" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = { objectExpr },
        }
    end

    -- stack.Peek() → stack[#stack]
    if methodName == "Peek" and #args == 0 then
        return {
            type = "index_access",
            object = objectExpr,
            key = { type = "unary_op", op = "#", operand = objectExpr },
        }
    end

    return nil
end
```

**Step 2: Integrate into the `call` handler**

In the existing `call` handler (around line 248), add collection method detection before the generic method_call fallback. Insert after the `Console.WriteLine` check but before the generic `expr.target` branch:

```lua
-- Collection method rewrites (before generic method_call)
if expr.target and expr.name then
    local objectExpr = lowerExpression(expr.target)
    local args = {}
    for _, arg in expr.arguments do
        table.insert(args, lowerExpression(arg))
    end

    -- Try array-like rewrites (List, IList, ICollection, IEnumerable, Queue, Stack)
    local arrayResult = rewriteCollectionCall(expr.name, objectExpr, args)
    if arrayResult then
        return arrayResult
    end

    -- Try Queue-specific rewrites
    local queueResult = rewriteQueueCall(expr.name, objectExpr, args)
    if queueResult then
        return queueResult
    end

    -- Try Stack-specific rewrites
    local stackResult = rewriteStackCall(expr.name, objectExpr, args)
    if stackResult then
        return stackResult
    end

    -- Try dict rewrites
    local dictKind, dictResult = rewriteDictCall(expr.name, objectExpr, args)
    if dictResult then
        if dictKind == "assignment" then
            return dictResult -- caller handles as statement
        end
        return dictResult
    end

    -- Try set rewrites
    local setKind, setResult = rewriteSetCall(expr.name, objectExpr, args)
    if setResult then
        if setKind == "assignment" then
            return setResult
        end
        return setResult
    end
end
```

**Step 3: Commit**

```
feat(plugin): add collection method call rewrites to Lowerer
```

---

### Task 10: Rewrite collection property access (Count)

**Files:**
- Modify: `plugin/src/Lowerer.lua` (the `member_access` handler, around line 288)

**Step 1: Add property rewriting to member_access handler**

Replace the existing `member_access` handler:

```lua
-- Member access: obj.field
if t == "member_access" then
    -- .Count → #obj (for array-like collections)
    if expr.member == "Count" or expr.member == "Length" then
        return {
            type = "unary_op",
            op = "#",
            operand = lowerExpression(expr.object),
        }
    end

    return {
        type = "dot_access",
        object = lowerExpression(expr.object),
        field = expr.member,
    }
end
```

**Step 2: Commit**

```
feat(plugin): rewrite .Count/.Length to # operator in Lowerer
```

---

### Task 11: Verify Emitter handles all new IR node types

**Files:**
- Modify: `plugin/src/Emitter.lua` (if needed)

**Step 1: Verify the Emitter can render all IR nodes produced by the new rewrites**

The new rewrites produce these IR node types:
- `call` with `dot_access` callee — already handled (emits `table.insert(...)`)
- `binary_op` — already handled
- `unary_op` — already handled
- `index_access` — already handled
- `assignment` — already handled
- `array_literal` — already handled

Check the Emitter handles `dot_access` as a callee in `call` expressions. Looking at `emitExpression`, the `call` handler calls `emitExpression(expr.callee)`, and `dot_access` is handled — this produces `table.insert`, `table.remove`, etc. correctly.

No changes needed to Emitter.lua.

**Step 2: Commit (skip if no changes)**

---

### Task 12: Final verification and commit

**Step 1: Grep to verify zero `pairs(`/`ipairs(` remain in plugin source**

```bash
grep -rn "pairs\|ipairs" plugin/src/*.lua
```

Should return zero results (except possibly in string literals or comments referencing Lua semantics, which are fine).

**Step 2: Build the plugin**

```bash
rojo build plugin/plugin.project.json -o plugin/LUSharp-plugin.rbxmx
```

**Step 3: Final commit if any stragglers**

```
chore(plugin): verify zero pairs/ipairs usage in plugin source
```
