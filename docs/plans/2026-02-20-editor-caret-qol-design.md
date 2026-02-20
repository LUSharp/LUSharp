# Design: Editor Cursor/Caret QoL Fixes

## Problem
Users report cursor/caret mismatch in the LUSharp in-plugin editor, commonly when:
- Ending a line with `;`, pressing Enter (newline), then pressing Tab quickly.

Symptoms:
- Visual caret position, status bar (Ln/Col), and insertion point can temporarily desync.

## Goals
- Keep these consistent at all times:
  1) visual caret position
  2) status bar Ln/Col
  3) insertion point for typing / Tab insertion
- Avoid race conditions between deferred auto-indent work and subsequent keystrokes.
- Keep changes minimal and low-risk (no editor rewrite).

## Non-goals
- Full autocomplete UX overhaul (separate milestone).
- Adding full undo/redo, search, diagnostics UI, etc. (later milestones).

## Approach
### 1) Single refresh scheduling
Introduce a single “schedule refresh” path so caret + status + overlay refresh happen once per tick, after TextBox has applied changes.
- Replace scattered immediate refresh calls (especially inside key handlers) with a deferred refresh.

### 2) Remove Return-key deferred auto-indent race
Today auto-indent is scheduled from the Return key handler with `task.defer(...)`. If the user hits Tab quickly, the deferred auto-indent can still run after the Tab edit and shift the cursor unexpectedly.

Fix:
- Detect newline insertion from the `Text` changed handler and apply auto-indent there (only when the inserted character is `\n` at the cursor).
- Ensure this path is guarded to avoid recursion and only runs when autoIndent is enabled.

### 3) Track last known text/cursor
Maintain `self._lastText` and `self._lastCursorPos` to allow lightweight change detection and safer refresh scheduling.

## Testing
- Add/adjust Luau unit tests for any extracted pure helper logic used to decide when to auto-indent.
- Manual Studio validation:
  - type `;` then Enter then Tab quickly; confirm caret + Ln/Col remain consistent and indentation doesn’t double-apply.

## Rollout
- Rebuild plugin `.rbxmx` and copy to Studio Plugins folder.
- Restart Studio and validate behavior.
