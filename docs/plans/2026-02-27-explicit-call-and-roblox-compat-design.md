# Explicit Call Resolution & Static/Instance Compiler Validation

**Date:** 2026-02-27
**Scope:** Plugin (`Lowerer.lua`, `IntelliSense.lua`, `init.server.lua`)

## Problem

The transpiler emits all cross-class method calls with `:` (colon/instance syntax). It cannot distinguish static vs instance calls on other classes. This produces incorrect Luau:

```
-- C#: NewScript.SomeFunc()  (static call)
-- Current output (wrong):  NewScript:SomeFunc()
-- Correct output:          NewScript.SomeFunc()
```

The `rewriteSelfCallsInBlock` pass already handles bare same-class calls correctly (static → `.`, instance → `:`), but cross-class calls in `lowerExpression` blindly create `method_call` IR nodes.

The compiler does not enforce C# static/instance access rules — calling a static member on an instance should be a hard error per CS0176.

## Design

### Part 1: Same-Module Symbol Table

Before lowering individual classes, the Lowerer pre-scans all classes in the parse result to build a symbol table:

```lua
local moduleSymbols = {
    ["ClassName"] = {
        methods = { ["MethodName"] = { isStatic = true } },
        fields = { ["fieldName"] = { isStatic = false, fieldType = "int" } },
    }
}
```

Built once per `Lowerer.lower()` call from `parseResult.classes`. Gives the Lowerer visibility into all classes defined in the same script.

### Part 2: Local Variable Type Tracking

Extend existing local variable tracking in `rewriteSelfCallsInBlock` to also track **types** when known from declarations:

```lua
-- From: NewScript2 foo = new();
-- Track: localTypes["foo"] = "NewScript2"
```

Type is known when:
- `local_decl` has an explicit type annotation (from Parser's `variable_declaration`)
- `new ClassName()` expression provides the type
- Field declarations with explicit types

### Part 3: Call Style Resolution

When lowering a `call` node with `expr.target` (i.e., `object.method(args)`):

1. **Target is a known class name** in `moduleSymbols` → static access:
   - Method is `isStatic = true` → emit as `call` with `dot_access` callee (correct)
   - Method is `isStatic = false` → **CS0120 error**

2. **Target is a local variable** with known type in `localTypes`:
   - Look up method in `moduleSymbols[type]`:
     - `isStatic = true` → **CS0176 error** (static member accessed on instance)
     - `isStatic = false` → emit as `method_call` (colon, correct)

3. **Target type unknown** (cross-script, not in symbol table) → default to `method_call` (colon) — preserves current behavior for Roblox API calls

### Part 4: Compiler Diagnostics

The Lowerer collects diagnostics during lowering and returns them in the IR result:

| Scenario | Code | Message |
|---|---|---|
| `instance.StaticMethod()` | CS0176 | "Static member '{method}' cannot be accessed with an instance reference; qualify it with a type name instead" |
| `ClassName.InstanceMethod()` | CS0120 | "An object reference is required for the non-static field, method, or property '{method}'" |

`init.server.lua` checks `ir.diagnostics` during build. If any errors exist, the build is refused.

### Part 5: IntelliSense Diagnostic Updates

Update `collectInvalidUserMemberAccessDiagnostics` to use proper C# error codes and messages instead of the generic "does not contain a definition" message. The IntelliSense validation already checks `isStatic` consistency — just needs the correct error format.

### Part 6: Roblox API Calls

Roblox API calls (`game:GetService()`, `player:Kick()`, etc.) don't go through the symbol table — their targets aren't in `moduleSymbols`. These continue to emit as `method_call` (colon) by default, which is correct for Roblox's instance method convention.

## Implementation Location

| File | Changes |
|---|---|
| `Lowerer.lua` | `buildModuleSymbols()` pre-scan, `localTypes` tracking in `lowerBlock`, call style resolution in `lowerExpression`, diagnostic collection |
| `Emitter.lua` | No changes — already handles `call` (dot) vs `method_call` (colon) |
| `IntelliSense.lua` | Update `collectInvalidUserMemberAccessDiagnostics` to emit CS0176/CS0120 |
| `init.server.lua` | Check `ir.diagnostics` in `compileOne`, block build on errors |

## Error References

- [CS0176](https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0176): Static member cannot be accessed with instance reference
- [CS0120](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0120): Object reference required for non-static member
