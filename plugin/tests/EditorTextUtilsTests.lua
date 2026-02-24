return function(describe, it, expect)
    local TextUtils = require("../src/EditorTextUtils")

    describe("EditorTextUtils", function()
        it("dedents a closing brace by one indent level", function()
            local text = "    }"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(true)
            expect(newText):toBe("}")
            expect(newCursor):toBe(2)
        end)

        it("dedents when there are multiple indent levels", function()
            local text = "        }" -- 8 spaces
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(true)
            expect(newText):toBe("    }")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("dedents one level for tab indentation", function()
            local text = "\t\t}"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(true)
            expect(newText):toBe("\t}")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("dedents fallback indentation from line end", function()
            local text = "     }"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "  ")

            expect(changed):toBe(true)
            expect(newText):toBe("   }")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("does nothing when line has other tokens", function()
            local text = "    } // comment"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(cursorPos)
        end)

        it("does nothing when previous char is not a closing brace", function()
            local text = "    x"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(cursorPos)
        end)

        it("dedents a closing brace after a newline", function()
            local text = "if (x) {\n    }"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(true)
            expect(newText):toBe("if (x) {\n}")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("does not over-dedent method closing brace at class scope", function()
            local text = "class Main\n{\n    void GameEntry()\n    {\n    }"
            local cursorPos = #text + 1
            local newText, newCursor, changed = TextUtils.autoDedentClosingBrace(text, cursorPos, "    ")

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(cursorPos)
        end)

        it("computes auto-indent after newline", function()
            local prevText = "if (x) {"
            local prevCursor = #prevText + 1

            local newText = "if (x) {\n"
            local newCursor = #newText + 1

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("    ")
        end)

        it("carries forward leading whitespace after newline", function()
            local prevText = "    x;"
            local prevCursor = #prevText + 1

            local newText = "    x;\n"
            local newCursor = #newText + 1

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("    ")
        end)

        it("auto-indents blank lines based on scope depth", function()
            local prevText = "if (x) {\n    "
            local prevCursor = #prevText + 1

            local newText = "if (x) {\n    \n"
            local newCursor = #newText + 1

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("    ")
        end)

        it("computes auto-indent when newline replaces a selection", function()
            local prevText = "    abcdef"
            local prevCursor = 10 -- simulate selection end

            local newText = "    ab\nf"
            local newCursor = 8 -- cursor after inserted newline

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("    ")
        end)

        it("does not auto-indent when only the cursor moves", function()
            local textWithNewline = "if (x) {\n"
            local prevCursor = #textWithNewline -- cursor still before the '\n'
            local newCursor = #textWithNewline + 1 -- cursor moved after the '\n'

            local indent = TextUtils.computeAutoIndentInsertion(textWithNewline, prevCursor, textWithNewline, newCursor, "    ")
            expect(indent):toBe("")
        end)

        it("does not auto-indent when cursor crosses an existing newline", function()
            local text = "if (x) {\n    y"
            local newlineIndex = assert(text:find("\n", 1, true))
            local prevCursor = newlineIndex
            local newCursor = newlineIndex + 1

            local indent = TextUtils.computeAutoIndentInsertion(text, prevCursor, text, newCursor, "    ")
            expect(indent):toBe("")
        end)

        it("does not auto-indent after newline when previous line does not open scope", function()
            local prevText = "x;"
            local prevCursor = #prevText + 1

            local newText = "x;\n"
            local newCursor = #newText + 1

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("")
        end)

        it("suppresses auto-trigger when current line is whitespace only", function()
            local text = "class Main {\n    \n"
            local cursor = #text + 1
            local shouldTrigger = TextUtils.shouldAutoTriggerCompletions(text, cursor)
            expect(shouldTrigger):toBe(false)
        end)

        it("suppresses auto-trigger after trailing space without identifier", function()
            local text = "class "
            local cursor = #text + 1
            local shouldTrigger = TextUtils.shouldAutoTriggerCompletions(text, cursor)
            expect(shouldTrigger):toBe(false)
        end)

        it("allows auto-trigger while typing an identifier", function()
            local text = "clas"
            local cursor = #text + 1
            local shouldTrigger = TextUtils.shouldAutoTriggerCompletions(text, cursor)
            expect(shouldTrigger):toBe(true)
        end)

        it("allows auto-trigger for member identifier after dot", function()
            local text = "game.Pl"
            local cursor = #text + 1
            local shouldTrigger = TextUtils.shouldAutoTriggerCompletions(text, cursor)
            expect(shouldTrigger):toBe(true)
        end)

        it("suppresses auto-trigger immediately after dot", function()
            local text = "game."
            local cursor = #text + 1
            local shouldTrigger = TextUtils.shouldAutoTriggerCompletions(text, cursor)
            expect(shouldTrigger):toBe(false)
        end)

        it("extracts completion label from completion object", function()
            local label = TextUtils.extractCompletionLabel({ label = "WriteLine" })
            expect(label):toBe("WriteLine")
        end)

        it("extracts completion label from raw string", function()
            local label = TextUtils.extractCompletionLabel("WriteLine")
            expect(label):toBe("WriteLine")
        end)

        it("returns nil when completion item has no label", function()
            local label = TextUtils.extractCompletionLabel({ detail = "method" })
            expect(label):toBeNil()
        end)

        it("resolves active completion label by selected index", function()
            local label = TextUtils.resolveCompletionLabel({
                { label = "Namespace" },
                { label = "GetService" },
            }, 2)
            expect(label):toBe("GetService")
        end)

        it("falls back to first completion label when selected index is invalid", function()
            local label = TextUtils.resolveCompletionLabel({
                { label = "Namespace" },
                { label = "GetService" },
            }, 20)
            expect(label):toBe("Namespace")
        end)

        it("resolves completion label from string entries", function()
            local label = TextUtils.resolveCompletionLabel({ "Namespace", "GetService" }, 2)
            expect(label):toBe("GetService")
        end)

        it("advances completion selection down and wraps", function()
            expect(TextUtils.resolveNextCompletionIndex(nil, 3, 1)):toBe(1)
            expect(TextUtils.resolveNextCompletionIndex(1, 3, 1)):toBe(2)
            expect(TextUtils.resolveNextCompletionIndex(3, 3, 1)):toBe(1)
        end)

        it("moves completion selection up and wraps", function()
            expect(TextUtils.resolveNextCompletionIndex(nil, 3, -1)):toBe(3)
            expect(TextUtils.resolveNextCompletionIndex(3, 3, -1)):toBe(2)
            expect(TextUtils.resolveNextCompletionIndex(1, 3, -1)):toBe(3)
        end)

        it("returns nil completion selection when list is empty", function()
            expect(TextUtils.resolveNextCompletionIndex(1, 0, 1)):toBeNil()
        end)

        it("maps completion kind to method icon", function()
            local icon = TextUtils.getCompletionKindIcon("method")
            expect(icon):toBe("rbxassetid://138609826896991")
        end)

        it("maps variable aliases to local variable icon", function()
            expect(TextUtils.getCompletionKindIcon("variable")):toBe("rbxassetid://109131190629355")
            expect(TextUtils.getCompletionKindIcon("local variable")):toBe("rbxassetid://109131190629355")
            expect(TextUtils.getCompletionKindIcon("local_variable")):toBe("rbxassetid://109131190629355")
        end)

        it("maps enum item aliases to enum item icon", function()
            expect(TextUtils.getCompletionKindIcon("enumitem")):toBe("rbxassetid://120188564562495")
            expect(TextUtils.getCompletionKindIcon("enum item")):toBe("rbxassetid://120188564562495")
            expect(TextUtils.getCompletionKindIcon("enum_item")):toBe("rbxassetid://120188564562495")
        end)

        it("maps generic type completions to class icon", function()
            expect(TextUtils.getCompletionKindIcon("type")):toBe("rbxassetid://109871698753471")
        end)

        it("maps method kind to hover badge text", function()
            expect(TextUtils.getHoverKindBadgeText("method")):toBe("M")
        end)

        it("uses fallback hover badge text for unknown kind", function()
            expect(TextUtils.getHoverKindBadgeText("unknown-kind")):toBe("•")
        end)

        it("clamps completion viewport rows", function()
            expect(TextUtils.getCompletionViewportCount(0, 8)):toBe(0)
            expect(TextUtils.getCompletionViewportCount(3, 8)):toBe(3)
            expect(TextUtils.getCompletionViewportCount(30, 8)):toBe(8)
            expect(TextUtils.getCompletionViewportCount(3, 0)):toBe(1)
        end)

        it("suppresses completion info without recent mouse movement", function()
            local shouldShow = TextUtils.shouldShowCompletionInfo(10.0, 10.5, 0.25)
            expect(shouldShow):toBe(false)
        end)

        it("shows completion info after recent mouse movement", function()
            local shouldShow = TextUtils.shouldShowCompletionInfo(10.0, 10.1, 0.25)
            expect(shouldShow):toBe(true)
        end)

        it("suppresses completion info when mouse moved before popup shown", function()
            local shouldShow = TextUtils.shouldShowCompletionInfo(10.0, 10.2, 10.25, 0.25)
            expect(shouldShow):toBe(false)
        end)

        it("shows completion info when mouse moved after popup shown", function()
            local shouldShow = TextUtils.shouldShowCompletionInfo(10.3, 10.2, 10.35, 0.25)
            expect(shouldShow):toBe(true)
        end)

        it("suppresses hover while completion list is visible", function()
            expect(TextUtils.shouldSuppressHoverInfo(true)):toBe(true)
        end)

        it("allows hover when completion list is hidden", function()
            expect(TextUtils.shouldSuppressHoverInfo(false)):toBe(false)
            expect(TextUtils.shouldSuppressHoverInfo(nil)):toBe(false)
        end)

        it("suppresses hover when editor is not focused", function()
            local shouldSuppress = TextUtils.shouldSuppressHoverInfo(false, nil, 10.0, 0.25, false)
            expect(shouldSuppress):toBe(true)
        end)

        it("suppresses hover briefly after typing", function()
            local shouldSuppress = TextUtils.shouldSuppressHoverInfo(false, 10.0, 10.1, 0.25)
            expect(shouldSuppress):toBe(true)
        end)

        it("allows hover after typing suppression window", function()
            local shouldSuppress = TextUtils.shouldSuppressHoverInfo(false, 10.0, 10.4, 0.25)
            expect(shouldSuppress):toBe(false)
        end)

        it("computes completion replacement range for identifier suffix", function()
            local text = "Console.Wri"
            local cursor = #text + 1
            local startPos, endPos = TextUtils.computeCompletionReplacementRange(text, cursor)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("computes empty replacement range right after dot", function()
            local text = "Console."
            local cursor = #text + 1
            local startPos, endPos = TextUtils.computeCompletionReplacementRange(text, cursor)
            expect(startPos):toBe(#text + 1)
            expect(endPos):toBe(#text + 1)
        end)

        it("clamps completion replacement range for invalid cursor", function()
            local text = "print"
            local startPos, endPos = TextUtils.computeCompletionReplacementRange(text, -1)
            expect(startPos):toBe(1)
            expect(endPos):toBe(#text + 1)
        end)

        it("uses anchored completion replacement range when cursor stays in anchor", function()
            local text = "Console.Wri"
            local cursor = #text + 1
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, cursor, 9, #text + 1)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("prefers current caret when stale active snapshot disagrees", function()
            local text = "Console.Wri"
            local currentCursor = #text + 1
            local staleActiveCursor = 3
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, currentCursor, 9, #text + 1, staleActiveCursor)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("falls back to cursor-derived range when cursor drifts away from anchor", function()
            local text = "Console.Wri"
            local cursor = 3
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, cursor, 9, #text + 1)
            expect(startPos):toBe(1)
            expect(endPos):toBe(3)
        end)

        -- Manual click-flow acceptance checklist (runtime UI path):
        -- 1) Type "Console.Wri" and trigger completions.
        -- 2) Move mouse over completion list and click "WriteLine".
        -- 3) Verify insertion replaces "Wri" at caret token range.
        -- 4) Verify no insertion occurs at mouse click location.
        it("prefers active caret range when click drift moves cursor outside anchor", function()
            local text = "Console.Wri"
            local clickDriftCursor = 3
            local activeCaretCursor = #text + 1
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, clickDriftCursor, 9, #text + 1, activeCaretCursor)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("still falls back when both current and active cursors are outside anchor", function()
            local text = "Console.Wri"
            local clickDriftCursor = 3
            local activeCaretCursor = 2
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, clickDriftCursor, 9, #text + 1, activeCaretCursor)
            expect(startPos):toBe(1)
            expect(endPos):toBe(3)
        end)

        it("falls back to cursor-derived range when anchored range is invalid", function()
            local text = "Console.Wri"
            local cursor = #text + 1
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, cursor, 12, 9)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("falls back to cursor-derived range when anchored range is out of bounds", function()
            local text = "Console.Wri"
            local cursor = #text + 1
            local startPos, endPos = TextUtils.resolveCompletionReplacementRange(text, cursor, 0, 999)
            expect(startPos):toBe(9)
            expect(endPos):toBe(#text + 1)
        end)

        it("ctrl-delete removes a single word segment", function()
            local text = "alpha beta"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 1)

            expect(changed):toBe(true)
            expect(newText):toBe(" beta")
            expect(newCursor):toBe(1)
        end)

        it("ctrl-delete treats punctuation as its own segment", function()
            local text = "foo.bar"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 4)

            expect(changed):toBe(true)
            expect(newText):toBe("foobar")
            expect(newCursor):toBe(4)
        end)

        it("ctrl-delete treats each punctuation character as its own token", function()
            local text = "foo();bar"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 4)

            expect(changed):toBe(true)
            expect(newText):toBe("foo);bar")
            expect(newCursor):toBe(4)
        end)

        it("ctrl-delete removes only contiguous horizontal whitespace", function()
            local text = "foo   bar"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 4)

            expect(changed):toBe(true)
            expect(newText):toBe("foobar")
            expect(newCursor):toBe(4)
        end)

        it("ctrl-delete does not cross newline boundaries", function()
            local text = "foo\nbar"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 4)

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(4)
        end)

        it("ctrl-delete helper deletes selected range as one edit", function()
            local text = "alpha beta"
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlDeleteEdit(text, 7, 1)

            expect(changed):toBe(true)
            expect(newText):toBe("beta")
            expect(newCursor):toBe(1)
            expect(newSelection):toBe(-1)
        end)

        it("ctrl-delete helper returns unchanged state when nothing is deleted", function()
            local text = "foo\nbar"
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlDeleteEdit(text, 4, -1)

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(4)
            expect(newSelection):toBe(-1)
        end)

        it("ctrl-backspace removes a single previous word segment", function()
            local text = "alpha beta"
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, #text + 1)

            expect(changed):toBe(true)
            expect(newText):toBe("alpha ")
            expect(newCursor):toBe(7)
        end)

        it("ctrl-backspace treats punctuation as its own segment", function()
            local text = "foo.bar"
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, 5)

            expect(changed):toBe(true)
            expect(newText):toBe("foobar")
            expect(newCursor):toBe(4)
        end)

        it("ctrl-backspace helper deletes selected range as one edit", function()
            local text = "alpha beta"
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlBackspaceEdit(text, 1, 7)

            expect(changed):toBe(true)
            expect(newText):toBe("beta")
            expect(newCursor):toBe(1)
            expect(newSelection):toBe(-1)
        end)

        it("ctrl-backspace does not cross newline boundaries", function()
            local text = "foo\nbar"
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlBackspaceEdit(text, 5, -1)

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(5)
            expect(newSelection):toBe(-1)
        end)

        it("ctrl-backspace removes the previous identifier token", function()
            local text = "LocalPlayer"
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, #text + 1)

            expect(changed):toBe(true)
            expect(newText):toBe("")
            expect(newCursor):toBe(1)
        end)

        it("ctrl-delete removes the next identifier token", function()
            local text = "LocalPlayer"
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, 1)

            expect(changed):toBe(true)
            expect(newText):toBe("")
            expect(newCursor):toBe(1)
        end)

        it("ctrl-backspace on snake_case removes the whole identifier token", function()
            local text = "local_player"
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, #text + 1)

            expect(changed):toBe(true)
            expect(newText):toBe("")
            expect(newCursor):toBe(1)
        end)

        it("ctrl-backspace at end of GetService statement removes only trailing semicolon", function()
            local text = "var players = game.GetService(\"Players\");"
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, #text + 1)

            expect(changed):toBe(true)
            expect(newText):toBe("var players = game.GetService(\"Players\")")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("detects ctrl+z and ctrl+y as undo/redo shortcuts", function()
            expect(TextUtils.isUndoRedoShortcut("Z", true, false)):toBe(true)
            expect(TextUtils.isUndoRedoShortcut("Y", true, false)):toBe(true)
        end)

        it("does not treat other key chords as undo/redo shortcuts", function()
            expect(TextUtils.isUndoRedoShortcut("Z", false, false)):toBe(false)
            expect(TextUtils.isUndoRedoShortcut("Z", true, true)):toBe(false)
            expect(TextUtils.isUndoRedoShortcut("X", true, false)):toBe(false)
        end)

        it("detects word-delete shortcuts for ctrl+backspace and ctrl/alt+delete", function()
            expect(TextUtils.isWordDeleteShortcut("Backspace", true, false)):toBe(true)
            expect(TextUtils.isWordDeleteShortcut("Delete", true, false)):toBe(true)
            expect(TextUtils.isWordDeleteShortcut("Delete", false, true)):toBe(true)
        end)

        it("does not detect word-delete shortcut for plain backspace/delete", function()
            expect(TextUtils.isWordDeleteShortcut("Backspace", false, false)):toBe(false)
            expect(TextUtils.isWordDeleteShortcut("Delete", false, false)):toBe(false)
            expect(TextUtils.isWordDeleteShortcut("Backspace", false, true)):toBe(false)
        end)

        it("repairs suspicious newline-spanning deletes without selection", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "entire line\n", "", false)):toBe(true)
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "\nwhole line", "", false)):toBe(true)
        end)

        it("does not repair multi-char non-newline delete without shortcut confirmation", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "wholetoken", "", false)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "abc", "", false)):toBe(false)
        end)

        it("does not repair normal single-char delete or selection deletes", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "x", "", false)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Backspace", true, "many chars", "", false)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "abc", "x", false)):toBe(false)
        end)

        it("repairs shortcut delete when native path only removed one character", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "x", "", true)):toBe(true)
        end)

        it("does not repair shortcut delete when native path already removed a token", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "Players", "", true)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "game", "", true)):toBe(false)
        end)

        it("does not repair shortcut delete when there was an active selection", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", true, "game.GetService", "", true)):toBe(false)
        end)

        it("infers backspace direction when cursor moves left", function()
            expect(TextUtils.resolveWordDeleteDirection(12, 5)):toBe("backward")
        end)

        it("infers delete direction when cursor stays or moves right", function()
            expect(TextUtils.resolveWordDeleteDirection(5, 5)):toBe("forward")
            expect(TextUtils.resolveWordDeleteDirection(5, 8)):toBe("forward")
        end)

        it("defaults delete direction to backward when cursors are invalid", function()
            expect(TextUtils.resolveWordDeleteDirection(nil, 5)):toBe("backward")
            expect(TextUtils.resolveWordDeleteDirection(8, nil)):toBe("backward")
        end)

        it("derives backward repair cursor from removed splice length", function()
            expect(TextUtils.resolveWordDeleteCursorFromSplice(4, "abc", "backward")):toBe(7)
        end)

        it("derives forward repair cursor from splice start", function()
            expect(TextUtils.resolveWordDeleteCursorFromSplice(4, "abc", "forward")):toBe(4)
        end)

        it("returns nil repair cursor for invalid splice start", function()
            expect(TextUtils.resolveWordDeleteCursorFromSplice(nil, "abc", "backward")):toBeNil()
            expect(TextUtils.resolveWordDeleteCursorFromSplice(0, "abc", "forward")):toBeNil()
        end)

        it("consumes enter completion key when completions are available", function()
            expect(TextUtils.shouldConsumeCompletionAcceptInput("Return", true, 3)):toBe(true)
            expect(TextUtils.shouldConsumeCompletionAcceptInput("KeypadEnter", false, 2)):toBe(true)
        end)

        it("does not consume completion key when completions are unavailable", function()
            expect(TextUtils.shouldConsumeCompletionAcceptInput("Return", false, 0)):toBe(false)
            expect(TextUtils.shouldConsumeCompletionAcceptInput("Tab", false, 0)):toBe(false)
            expect(TextUtils.shouldConsumeCompletionAcceptInput("Space", true, 5)):toBe(false)
        end)

        it("consumes tab completion key when completion popup is visible", function()
            expect(TextUtils.shouldConsumeCompletionAcceptInput("Tab", true, 0)):toBe(true)
        end)

        it("resolves cursor column from x using measured widths", function()
            local measure = function(text)
                return #text
            end

            expect(TextUtils.resolveCursorColumnFromX("abcdef", 0, 8, measure)):toBe(1)
            expect(TextUtils.resolveCursorColumnFromX("abcdef", 2.4, 8, measure)):toBe(3)
            expect(TextUtils.resolveCursorColumnFromX("abcdef", 2.8, 8, measure)):toBe(4)
            expect(TextUtils.resolveCursorColumnFromX("abcdef", 99, 8, measure)):toBe(7)
        end)

        it("resolves cursor column with tab-aware measured widths", function()
            local measure = function(text)
                local width = 0
                for i = 1, #text do
                    local ch = text:sub(i, i)
                    if ch == "\t" then
                        width += 4
                    else
                        width += 1
                    end
                end
                return width
            end

            local line = "\ta"
            expect(TextUtils.resolveCursorColumnFromX(line, 3.9, 8, measure)):toBe(2)
            expect(TextUtils.resolveCursorColumnFromX(line, 4.6, 8, measure)):toBe(3)
        end)
    end)
end
