# Plugin: Generalized Iteration & Collection Rewrites

**Date:** 2026-02-27
**Scope:** Plugin only (`plugin/src/*.lua`)

## Problem

1. Plugin Lua source uses `pairs()`/`ipairs()` everywhere — Luau has generalized iteration via `__iter`, making these unnecessary.
2. The plugin's Lowerer wraps `foreach` iterators in `pairs()` when generating Luau code from user C#.
3. User C# collection methods (`List<T>.Add()`, `Dictionary<K,V>.Remove()`, etc.) pass through as raw method calls, producing invalid Luau like `list:Add(x)`.
4. `new List<T>()` / `new Dictionary<K,V>()` emit as `List.new()` / `Dictionary.new()` instead of `{}`.
5. `IEnumerable<T>` patterns (LINQ-style or custom iterables) need to produce valid Luau iteration.

## Design

### Part 1: Remove `pairs()`/`ipairs()` from plugin source

Mechanical replacement across all `plugin/src/*.lua` files:

- `for k, v in pairs(t) do` → `for k, v in t do`
- `for i, v in ipairs(t) do` → `for i, v in t do`
- Same for `_` discard patterns.

No behavioral change — Luau generalized iteration handles both array and dictionary tables.

**Affected files:** Editor.lua, EditorTextUtils.lua, Emitter.lua, IntelliSense.lua, Lowerer.lua, ScriptManager.lua, ProjectView.lua, TypeDatabase.lua, Settings.lua, init.server.lua, and any others.

### Part 2: Fix Lowerer `foreach` codegen

In `Lowerer.lua:817-828`, change `foreach` → `for_in` to emit the raw iterable expression instead of wrapping in `pairs()`.

**Before:**
```lua
iterator = { type = "call", callee = { type = "identifier", name = "pairs" }, args = { lowerExpression(stmt.iterable) } }
```

**After:**
```lua
iterator = lowerExpression(stmt.iterable)
```

This produces `for _, v in myTable do` — correct for all iterable types in Luau.

### Part 3: Collection method rewrites in the Lowerer

Add collection-aware rewriting to `lowerExpression` for `call` and `member_access` nodes. Detection uses method name matching (the Lowerer already strips generic params via `normalizeTypeName()`).

#### Collection types treated as plain tables

| C# type | Luau equivalent |
|---|---|
| `List<T>` | `{}` (array table) |
| `Dictionary<K,V>` | `{}` (dictionary table) |
| `HashSet<T>` | `{}` (dictionary table, keys = values) |
| `Queue<T>` | `{}` (array table) |
| `Stack<T>` | `{}` (array table) |
| `IEnumerable<T>` | passthrough (already a table or iterator) |
| `IList<T>` | passthrough (already a table) |
| `ICollection<T>` | passthrough (already a table) |
| `IDictionary<K,V>` | passthrough (already a table) |

#### Method/property rewrites

**List / IList / ICollection (array-like):**

| C# | Luau |
|---|---|
| `list.Add(x)` | `table.insert(list, x)` |
| `list.Remove(x)` | `table.remove(list, table.find(list, x))` |
| `list.RemoveAt(i)` | `table.remove(list, i + 1)` |
| `list.Insert(i, x)` | `table.insert(list, i + 1, x)` |
| `list.Count` | `#list` (unary length) |
| `list.Contains(x)` | `table.find(list, x) ~= nil` |
| `list.Clear()` | `table.clear(list)` |
| `list.IndexOf(x)` | `(table.find(list, x) or 0) - 1` |
| `list[i]` | `list[i + 1]` (0-based → 1-based) |

**Dictionary / IDictionary:**

| C# | Luau |
|---|---|
| `dict.Add(key, val)` | `dict[key] = val` |
| `dict.Remove(key)` | `dict[key] = nil` |
| `dict.ContainsKey(key)` | `dict[key] ~= nil` |
| `dict.TryGetValue(key, out var v)` | `local v = dict[key]` (special statement rewrite) |
| `dict.Count` | skip for now (no cheap Luau equivalent for dictionaries) |
| `dict.Keys` | skip for now |
| `dict.Values` | skip for now |

**HashSet:**

| C# | Luau |
|---|---|
| `set.Add(x)` | `set[x] = true` |
| `set.Remove(x)` | `set[x] = nil` |
| `set.Contains(x)` | `set[x] ~= nil` |

**Queue:**

| C# | Luau |
|---|---|
| `queue.Enqueue(x)` | `table.insert(queue, x)` |
| `queue.Dequeue()` | `table.remove(queue, 1)` |
| `queue.Peek()` | `queue[1]` |
| `queue.Count` | `#queue` |

**Stack:**

| C# | Luau |
|---|---|
| `stack.Push(x)` | `table.insert(stack, x)` |
| `stack.Pop()` | `table.remove(stack)` |
| `stack.Peek()` | `stack[#stack]` |
| `stack.Count` | `#stack` |

#### `new` expression rewrites

When `normalizeTypeName(expr.className)` matches a collection type, emit `{}` instead of `ClassName.new()`.

If the `new` has an initializer (e.g., `new List<int> { 1, 2, 3 }`), emit `{1, 2, 3}`. Already partially handled by `collection_expression` path.

For `new Dictionary<K,V> { {"a", 1}, {"b", 2} }`, emit `{ a = 1, b = 2 }`.

### Part 4: IEnumerable / iterable support

`IEnumerable<T>` is not a concrete type — it's an interface indicating the object is iterable. In Luau, any table is iterable via generalized iteration.

**Rules:**

1. **Parameters typed `IEnumerable<T>`**: No transformation needed. The caller passes a table, the callee iterates it with `for k, v in param do`.
2. **Return type `IEnumerable<T>`**: The method returns a table. Type annotation stripped.
3. **`foreach` over `IEnumerable<T>`**: Already handled by Part 2 — the raw expression is the iterator, no `pairs()` wrapping.
4. **LINQ methods** (`Where`, `Select`, `Any`, `First`, etc.): Out of scope for now. These would require a runtime library. If encountered, emit a comment `--[[ LINQ not supported ]]`.
5. **`yield return`**: Out of scope. Would require coroutine transformation.

### Implementation location

All changes in `plugin/src/Lowerer.lua`:
- Add a `COLLECTION_TYPES` set for detection.
- Add a `rewriteCollectionCall(methodName, object, args)` function before the generic `call` handler.
- Add a `rewriteCollectionMemberAccess(memberName, object)` function before the generic `member_access` handler.
- Modify `new` handler to check for collection types.
- Modify `foreach` handler to drop `pairs()`.

All changes in `plugin/src/*.lua` (plugin internal code):
- Find/replace `pairs()`/`ipairs()` → raw table iteration.
