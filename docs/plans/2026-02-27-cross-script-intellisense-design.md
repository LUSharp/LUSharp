# Cross-Script Modifier-Aware IntelliSense

**Date:** 2026-02-27
**Scope:** Plugin (`plugin/src/IntelliSense.lua`)

## Problem

IntelliSense has zero cross-script awareness. It doesn't know about classes defined in other scripts, can't provide completions for user-defined types, and has no visibility filtering by access modifiers. The Parser already tracks all modifiers (`access`, `isStatic`, `isOverride`, `isVirtual`, `isAbstract`, `isReadonly`, `isAsync`) on every AST node, but IntelliSense ignores them.

## Design

### Part 1: User Type Registry (Lazy Parse + Cache)

A cache inside IntelliSense that stores parsed type stubs from other scripts:

```lua
local userTypeCache = {}  -- { [scriptInstance] = { hash = "...", types = { ... } } }
```

**Flow:**
1. When IntelliSense encounters a `using` directive or an unresolved type name, it checks `userTypeCache`
2. If cache miss or source changed (hash mismatch), it calls `ScriptManager.getSource(script)`, parses with `Parser.parse()`, extracts class stubs
3. Stubs are stored in cache and registered into a user types lookup table

**Cache invalidation:** Compare source hash on each access. Only re-parse if source has changed.

**Stub extraction:** From a parsed AST class node, extract:
- Class name, namespace, base class, `accessModifier`, `isStatic`, `isAbstract`
- Constructor: parameters, `accessModifier`
- Methods: name, returnType, parameters, `accessModifier`, `isStatic`, `isAsync`
- Properties: name, propType, `accessModifier`, `hasGet`, `hasSet`
- Fields: name, fieldType, `accessModifier`, `isStatic`, `isReadonly`

Stubs are shaped to match `TypeDatabase.types` entries so existing completion logic works.

### Part 2: Namespace Resolution

When IntelliSense processes `using Game.Server;`:

1. Scan all scripts via `ScriptManager.getAll()`
2. For each script, read its `LUSharpNamespace` attribute (already stored on the instance)
3. If namespace matches the `using` target, lazily parse that script and cache its types
4. Register those types as available for completion in the current editing context

The current script's own namespace is always implicitly imported (same-namespace classes are always visible).

### Part 3: C# Visibility Rules

Access filtering based on the relationship between the requesting context and the target type:

| Modifier | Same class | Same namespace | Subclass (any namespace) | Different namespace |
|---|---|---|---|---|
| `public` | Yes | Yes | Yes | Yes |
| `private` | Yes | No | No | No |
| `protected` | Yes | No | Yes | No |
| `internal` | Yes | Yes | No | No |
| `protected internal` | Yes | Yes | Yes | No |

**Implementation in `getMemberCompletions`:**

```lua
local function isAccessible(member, context)
    local access = member.accessModifier or member.access or "private"

    if context.sameClass then
        return true  -- Everything visible in own class
    end

    if access == "public" then
        return true
    end

    if access == "private" then
        return false
    end

    if access == "protected" then
        return context.isSubclass
    end

    if access == "internal" then
        return context.sameNamespace
    end

    -- protected internal
    return context.sameNamespace or context.isSubclass
end
```

**Context determination:**
- `sameClass`: cursor is inside the same class definition
- `sameNamespace`: current script's namespace matches target type's namespace
- `isSubclass`: current class's `baseClass` chain includes the target type

### Part 4: Static vs Instance Filtering

When providing dot completions:

- **`ClassName.`** (identifier matches a known type name) → show only `isStatic = true` members + nested types
- **`instance.`** (identifier has an inferred instance type) → show only `isStatic = false` members
- **`new ClassName()`** → only offer if constructor is accessible per visibility rules
- **`this.`** / `self.` → show all members of current class (no visibility filter, no static filter)

### Part 5: Integration Points

The user type stubs integrate into the existing completion system at these points:

1. **`getMemberCompletions(typeName, prefix)`** — add `isAccessible()` filter before adding each member
2. **`getTypeCompletions(prefix, constructableOnly)`** — include user-defined classes from imported namespaces
3. **`inferExprTypeFromTokens()`** — resolve `new UserClass()` to `UserClass` type
4. **`resolveHoverInfoAtPosition()`** — show hover info for user-defined types and members
5. **`findMemberInType()`** — include user type members (with access filtering)

### Part 6: Type Stub Shape

User type stubs match the existing TypeDatabase shape:

```lua
{
    name = "EnemyController",
    namespace = "Game.Server",
    kind = "class",
    baseType = "",
    isStatic = false,
    isAbstract = false,
    accessModifier = "public",
    members = {
        {
            name = "Attack",
            kind = "method",
            type = "void",
            access = "public",
            isStatic = false,
            parameters = { { name = "target", type = "Player" } },
        },
        {
            name = "Health",
            kind = "property",
            type = "int",
            access = "public",
            canRead = true,
            canWrite = true,
        },
        {
            name = "new",
            kind = "constructor",
            access = "public",
            parameters = {},
        },
    },
}
```

## Implementation Location

| File | Changes |
|---|---|
| `plugin/src/IntelliSense.lua` | User type cache, lazy parsing, stub extraction, visibility filtering in `getMemberCompletions`/`getTypeCompletions`, namespace-aware type resolution, static/instance filtering |
| `plugin/src/ScriptManager.lua` | No changes — already has `getAll()` and `getSource()` |
| `plugin/src/Parser.lua` | No changes — already tracks all modifiers |
