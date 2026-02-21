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

        it("detects ctrl+z and ctrl+y as undo/redo shortcuts", function()
            expect(TextUtils.isUndoRedoShortcut("Z", true, false)):toBe(true)
            expect(TextUtils.isUndoRedoShortcut("Y", true, false)):toBe(true)
        end)

        it("does not treat other key chords as undo/redo shortcuts", function()
            expect(TextUtils.isUndoRedoShortcut("Z", false, false)):toBe(false)
            expect(TextUtils.isUndoRedoShortcut("Z", true, true)):toBe(false)
            expect(TextUtils.isUndoRedoShortcut("X", true, false)):toBe(false)
        end)
    end)
end
