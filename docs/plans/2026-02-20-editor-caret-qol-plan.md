# Editor Caret/Cursor QoL Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan.

**Goal:** Fix LUSharp editor caret/Ln-Col mismatch caused by newline + fast Tab (auto-indent race), keeping caret, status, and insertion point consistent.

**Architecture:** Move auto-indent triggering to the TextBox `Text` change path (newline detection) and avoid Return-key deferred indentation. Extract pure indent decision logic into `EditorTextUtils` so it can be unit tested.

**Tech Stack:** Luau (Roblox Studio plugin UI), existing plugin test harness (`./luau.exe plugin/tests/run.lua`).

---

### Task 1: Add pure helper for newline auto-indent decision

**Files:**
- Modify: `plugin/src/EditorTextUtils.lua`
- Test: `plugin/tests/EditorTextUtilsTests.lua`

**Step 1: Write the failing test**

Add a new test that models “newline was inserted” and expects indent text when previous line ends with `{`.

```lua
it("computes auto-indent after newline", function()
    local TextUtils = require("../src/EditorTextUtils")

    local prevText = "if (x) {"
    local prevCursor = #prevText + 1

    local newText = "if (x) {\n"
    local newCursor = #newText + 1

    local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
    expect(indent):toBe("    ")
end)
```

Also add a negative test for “no extra indent after semicolon line”.

```lua
it("does not auto-indent after newline when previous line does not open scope", function()
    local TextUtils = require("../src/EditorTextUtils")

    local prevText = "x;"
    local prevCursor = #prevText + 1

    local newText = "x;\n"
    local newCursor = #newText + 1

    local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
    expect(indent):toBe("")
end)
```

**Step 2: Run tests to verify RED**

Run: `./luau.exe plugin/tests/run.lua`

Expected: FAIL with something like `attempt to call nil value 'computeAutoIndentInsertion'`.

**Step 3: Write minimal implementation**

Implement in `plugin/src/EditorTextUtils.lua`:

- `computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, tabText) -> string`
  - Return `""` if it’s not a newline insertion event.
  - Detect newline insertion by checking that `newCursor > 1` and `newText:sub(newCursor-1,newCursor-1) == "\n"` and that `newText` is exactly `prevText .. "\n"` for this minimal v1 (single char insertion).
  - Compute indentation from the previous line in `newText` (line before the cursor):
    - start with leading whitespace of previous line
    - if previous line (trim right) ends with `{`, append `tabText`

**Step 4: Run tests to verify GREEN**

Run: `./luau.exe plugin/tests/run.lua`

Expected: PASS.

**Step 5: Commit**

```bash
git add plugin/src/EditorTextUtils.lua plugin/tests/EditorTextUtilsTests.lua
git commit -m "fix(editor): compute auto-indent on newline"
```

---

### Task 2: Use newline-triggered auto-indent in the editor (remove Return defer)

**Files:**
- Modify: `plugin/src/Editor.lua`
- (optional) Modify: `plugin/src/EditorTextUtils.lua` (if helper needs a small tweak)

**Step 1: Write a minimal failing reproduction note (manual)**

Manual repro in Studio (current bug):
- Type a line ending with `;`
- Press Enter
- Immediately press Tab
Expected: caret/Ln-Col stay consistent; indent only happens from Tab.

**Step 2: Implement minimal editor change**

In `plugin/src/Editor.lua`:
- Add `self._lastText` and `self._lastCursorPos` fields.
- In the TextBox `Text` changed handler:
  - Before calling `refresh(self)`, compute `indent = EditorTextUtils.computeAutoIndentInsertion(self._lastText, self._lastCursorPos, textBox.Text, textBox.CursorPosition, self.options.tabText)`.
  - If `indent ~= ""`, insert it at cursor using existing `insertAtCursor(textBox, indent)` guarded by `_suppressTextChange` to avoid recursion.
- Remove the `Return` key handler’s `task.defer(applyAutoIndent...)` block so auto-indent is no longer racing with subsequent Tab.
- Update `self._lastText`/`self._lastCursorPos` after refresh.

**Step 3: Verify existing automated tests still pass**

Run: `./luau.exe plugin/tests/run.lua`
Expected: PASS.

**Step 4: Rebuild plugin and copy into Studio plugins folder**

Run:
```bash
rojo build plugin/plugin.project.json -o plugin/build/LUSharp.rbxmx
cp -f plugin/build/LUSharp.rbxmx "/c/Users/table/AppData/Local/Roblox/Plugins/LUSharp.rbxmx"
```

**Step 5: Manual Studio verification**

- Restart Studio
- Repro sequence again:
  - `;` then Enter then immediate Tab
  - Confirm caret + Ln/Col + insertion remain consistent (no “jump back” after Tab).

**Step 6: Commit**

```bash
git add plugin/src/Editor.lua
git commit -m "fix(editor): avoid newline/tab auto-indent race"
```

---

### Task 3: Final verification + publish branch

**Files:**
- None (verification only)

**Step 1: Verify clean working tree and tests**

Run:
```bash
git status -uno
./luau.exe plugin/tests/run.lua
```

Expected: clean working tree, 0 failures.

**Step 2: Push branch**

```bash
git push -u origin fix/editor-caret-qol
```

**Step 3: Create PR**

```bash
gh pr create --base master --head fix/editor-caret-qol --title "fix(editor): caret/cursor stability" --body "..."
```
