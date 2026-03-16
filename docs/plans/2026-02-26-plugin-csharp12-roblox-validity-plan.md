# Plugin C# 12 + Roblox Validity Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement plugin-side C# 12 language support improvements plus Roblox-valid namespace/class enforcement with live-cache + static-snapshot fallback.

**Architecture:** The plugin resolves an active Roblox validity profile from a bundled snapshot plus validated cached/live updates, then uses that profile in IntelliSense and diagnostics. C# 12 support is controlled by an explicit support matrix so parsed-but-unsupported compile paths emit clear diagnostics instead of silently degrading.

**Tech Stack:** Luau plugin modules (`plugin/src/*.lua`), LUSharp test runner (`luau.exe plugin/tests/run.lua`), Roblox Studio plugin settings/cache APIs.

---

### Task 1: Baseline feature inventory + support matrix skeleton

**Files:**
- Create: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`
- Modify: `plugin/tests/LexerTests.lua`

**Step 1: Write failing inventory-driven tests**

```lua
-- add failing cases for one representative syntax per C# 12 feature
it("parses primary constructor declaration", function() ... end)
it("parses collection expression literal", function() ... end)
it("diagnoses unsupported interceptors", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL with new C# 12 test cases failing.

**Step 3: Add minimal support matrix module**

```lua
return {
  primary_constructors = { parse = false, intellisense = false, compile = false },
  collection_expressions = { parse = false, intellisense = false, compile = false },
  -- ...
}
```

**Step 4: Re-run tests (still expected fail but with deterministic matrix import path)**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL only on new assertions, no module-load/runtime errors.

**Step 5: Commit**

```bash
git add plugin/src/CSharp12SupportMatrix.lua plugin/tests/LexerTests.lua plugin/tests/ParserTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "test(plugin): add csharp12 inventory baseline tests"
```

---

### Task 2: Roblox validity snapshot + provider foundation

**Files:**
- Create: `plugin/src/RobloxValiditySnapshot.lua`
- Create: `plugin/src/RobloxValidityProvider.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Write failing provider tests**

```lua
it("uses bundled snapshot when cache missing", function() ... end)
it("ignores invalid cached profile", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on missing provider/snapshot behavior.

**Step 3: Implement snapshot + provider minimal behavior**

```lua
-- Provider: load snapshot, then cached profile if schema/hash valid.
-- return activeProfile, metadata
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS for new provider tests.

**Step 5: Commit**

```bash
git add plugin/src/RobloxValiditySnapshot.lua plugin/src/RobloxValidityProvider.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add roblox validity snapshot and provider"
```

---

### Task 3: Integrate provider into namespace/class diagnostics + completions

**Files:**
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Write failing namespace/class validity tests**

```lua
it("filters using completions to valid namespaces", function() ... end)
it("diagnoses invalid roblox class names", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on new validity filtering/diagnostic tests.

**Step 3: Implement provider-backed filtering/diagnostics**

```lua
local validity = RobloxValidityProvider.getActiveProfile(...)
-- use validity.namespaces and validity.classes for checks
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS for new validity tests.

**Step 5: Commit**

```bash
git add plugin/src/IntelliSense.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): enforce roblox namespace and class validity"
```

---

### Task 4: Live updater + cached profile refresh (plugin-side only)

**Files:**
- Create: `plugin/src/RobloxValidityLiveUpdater.lua`
- Modify: `plugin/src/Settings.lua`
- Modify: `plugin/src/init.server.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Write failing updater/cache tests**

```lua
it("prefers newer validated live profile over snapshot", function() ... end)
it("falls back cleanly when live fetch fails", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on live update/caching behavior.

**Step 3: Implement updater with atomic cache write + validation**

```lua
-- fetch -> validate schema/hash -> persist to plugin settings -> notify provider
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS on updater/cache tests.

**Step 5: Commit**

```bash
git add plugin/src/RobloxValidityLiveUpdater.lua plugin/src/Settings.lua plugin/src/init.server.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add live roblox validity refresh with cache fallback"
```

---

### Task 5: Primary constructors

**Files:**
- Modify: `plugin/src/Lexer.lua`
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/Emitter.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/LexerTests.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/LowererTests.lua`
- Modify: `plugin/tests/EmitterTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing tests for parse + compile behavior**

```lua
it("parses class primary constructor parameters", function() ... end)
it("lowers primary constructor fields correctly", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on primary constructor tests.

**Step 3: Implement minimal end-to-end support**

```lua
-- parser stores primary ctor params
-- lowerer maps params to constructor/init behavior
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS primary-constructor tests.

**Step 5: Commit**

```bash
git add plugin/src/Lexer.lua plugin/src/Parser.lua plugin/src/Lowerer.lua plugin/src/Emitter.lua plugin/src/IntelliSense.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/LexerTests.lua plugin/tests/ParserTests.lua plugin/tests/LowererTests.lua plugin/tests/EmitterTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add primary constructor support"
```

---

### Task 6: Collection expressions

**Files:**
- Modify: `plugin/src/Lexer.lua`
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/Emitter.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/LowererTests.lua`
- Modify: `plugin/tests/EmitterTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing collection expression tests**

```lua
it("parses collection expression with spread", function() ... end)
it("emits diagnostic for unsupported collection target", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on collection expression tests.

**Step 3: Implement support for practical targets + explicit unsupported diagnostics**

```lua
-- support array/list-like lowering
-- diagnostic code when unsupported target shape encountered
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS collection-expression tests.

**Step 5: Commit**

```bash
git add plugin/src/Lexer.lua plugin/src/Parser.lua plugin/src/Lowerer.lua plugin/src/Emitter.lua plugin/src/IntelliSense.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/ParserTests.lua plugin/tests/LowererTests.lua plugin/tests/EmitterTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add collection expression support with diagnostics"
```

---

### Task 7: Optional/default lambda parameters

**Files:**
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/LowererTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing tests**

```lua
it("parses lambda default parameter values", function() ... end)
it("keeps lambda invocation behavior deterministic", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on lambda default parameter tests.

**Step 3: Implement minimal support**

```lua
-- represent default values in lambda parameter nodes
-- apply lowering behavior for omitted arguments
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS lambda-default tests.

**Step 5: Commit**

```bash
git add plugin/src/Parser.lua plugin/src/Lowerer.lua plugin/src/IntelliSense.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/ParserTests.lua plugin/tests/LowererTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add default lambda parameter support"
```

---

### Task 8: `ref readonly` parameters

**Files:**
- Modify: `plugin/src/Lexer.lua`
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/LexerTests.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing tests**

```lua
it("parses ref readonly parameter modifiers", function() ... end)
it("diagnoses unsupported byref semantics if encountered", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on ref-readonly tests.

**Step 3: Implement parse/intellisense and compile-path handling**

```lua
-- parse modifiers, preserve in signatures
-- emit diagnostic where semantics cannot be preserved in transpile path
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS ref-readonly tests.

**Step 5: Commit**

```bash
git add plugin/src/Lexer.lua plugin/src/Parser.lua plugin/src/Lowerer.lua plugin/src/IntelliSense.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/LexerTests.lua plugin/tests/ParserTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add ref readonly parameter support"
```

---

### Task 9: Alias any type

**Files:**
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing tests**

```lua
it("parses using alias for tuple/array aliases", function() ... end)
it("resolves alias in declaration and completion contexts", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on alias-any-type tests.

**Step 3: Implement common alias forms + diagnostics for unsupported unsafe forms**

```lua
-- support named/tuple/array alias resolution
-- explicit diagnostic for unsupported pointer/function-pointer alias forms
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS alias tests.

**Step 5: Commit**

```bash
git add plugin/src/Parser.lua plugin/src/IntelliSense.lua plugin/src/Lowerer.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/ParserTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add alias any type support"
```

---

### Task 10: Inline arrays + experimental attribute + interceptors diagnostics

**Files:**
- Modify: `plugin/src/Lexer.lua`
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/src/CSharp12SupportMatrix.lua`
- Modify: `plugin/tests/LexerTests.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing tests**

```lua
it("recognizes InlineArray attribute usage", function() ... end)
it("emits unsupported compile diagnostic for interceptors", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on inline-array/experimental/interceptor tests.

**Step 3: Implement parse/intellisense + deterministic diagnostics**

```lua
-- parse and annotate feature usage
-- diagnostic codes for unsupported compile paths
```

**Step 4: Run tests to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS new diagnostic tests.

**Step 5: Commit**

```bash
git add plugin/src/Lexer.lua plugin/src/Parser.lua plugin/src/IntelliSense.lua plugin/src/CSharp12SupportMatrix.lua plugin/tests/LexerTests.lua plugin/tests/ParserTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "feat(plugin): add csharp12 diagnostics for inline arrays experimental and interceptors"
```

---

### Task 11: Bugfix sweep from inventory gaps

**Files:**
- Modify: `plugin/src/Parser.lua`
- Modify: `plugin/src/Lowerer.lua`
- Modify: `plugin/src/Emitter.lua`
- Modify: `plugin/src/IntelliSense.lua`
- Modify: `plugin/tests/ParserTests.lua`
- Modify: `plugin/tests/LowererTests.lua`
- Modify: `plugin/tests/EmitterTests.lua`
- Modify: `plugin/tests/IntelliSenseTests.lua`

**Step 1: Add failing regression tests for identified bugs**

```lua
it("regression: <specific gap from inventory>", function() ... end)
```

**Step 2: Run tests to verify failure**

Run: `./luau.exe plugin/tests/run.lua`
Expected: FAIL on newly added regression tests.

**Step 3: Apply minimal fixes only for proven bugs**

```lua
-- small targeted fix in parser/lowerer/intellisense paths
```

**Step 4: Run full suite to verify pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS full suite.

**Step 5: Commit**

```bash
git add plugin/src/Parser.lua plugin/src/Lowerer.lua plugin/src/Emitter.lua plugin/src/IntelliSense.lua plugin/tests/ParserTests.lua plugin/tests/LowererTests.lua plugin/tests/EmitterTests.lua plugin/tests/IntelliSenseTests.lua
git commit -m "fix(plugin): close csharp12 inventory gaps and regressions"
```

---

### Task 12: Final verification + plugin refresh

**Files:**
- Modify: `plugin/src/*` (only if required by final fixes)
- Modify: `plugin/tests/*` (only if required by final fixes)

**Step 1: Run full tests**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS all tests, 0 failed.

**Step 2: Rebuild plugin artifact and install**

Run: `rojo build "plugin/plugin.project.json" -o "plugin/LUSharp-plugin.rbxmx"`
Run: `cp -f "plugin/LUSharp-plugin.rbxmx" "/c/Users/table/AppData/Local/Roblox/Plugins/LUSharp-plugin.rbxmx"`
Expected: Destination file exists and begins with `<Item class="Script"`.

**Step 3: Commit final stabilization changes**

```bash
git add plugin/src plugin/tests
git commit -m "feat(plugin): complete csharp12 support and roblox validity integration"
```

**Step 4: Push branch**

```bash
git push -u origin feature/lang-imprv
```

---

## Execution Notes
- Keep each task strictly TDD (`RED -> GREEN -> COMMIT`).
- Do not silently transpile unsupported C# 12 features.
- Use deterministic diagnostics and stable range clamping.
- Prefer fixing one proven failing test cluster at a time.
