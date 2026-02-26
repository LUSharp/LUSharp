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

        it("tops up indentation when newline insert already includes partial indent", function()
            local prevText = "if (x) {\n    if (y) {\n        print(\"hi\");\n    }\n}"
            local prevCursor = prevText:find("        print", 1, true)

            local newText = prevText:sub(1, prevCursor - 1) .. "\n    " .. prevText:sub(prevCursor)
            local newCursor = prevCursor + #"\n    "

            local indent = TextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, "    ")
            expect(indent):toBe("    ")
        end)

        it("does not duplicate indentation when newline insert already matches context", function()
            local prevText = "if (x) {\n    if (y) {\n        print(\"hi\");\n    }\n}"
            local prevCursor = prevText:find("        print", 1, true)

            local newText = prevText:sub(1, prevCursor - 1) .. "\n        " .. prevText:sub(prevCursor)
            local newCursor = prevCursor + #"\n        "

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

        it("maps service kind to a visible icon", function()
            expect(TextUtils.getCompletionKindIcon("service")):toBe("rbxassetid://99768122416664")
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

        it("uses symbolic icons for property and variable hover badges", function()
            expect(TextUtils.getHoverKindBadgeText("property")):toBe("▣")
            expect(TextUtils.getHoverKindBadgeText("variable")):toBe("◉")
            expect(TextUtils.getHoverKindBadgeText("local_variable")):toBe("◉")
            expect(TextUtils.getHoverKindBadgeText("P")):toBe("•")
            expect(TextUtils.getHoverKindBadgeText("V")):toBe("•")
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

        it("ctrl-delete at opening quote deletes only the opening quote boundary", function()
            local text = "game.GetService(\"Players\")"
            local quotePos = assert(text:find("\"", 1, true))
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, quotePos)

            expect(changed):toBe(true)
            expect(newText):toBe("game.GetService(Players\")")
            expect(newCursor):toBe(quotePos)
        end)

        it("ctrl-delete at member/index boundary deletes only dot punctuation", function()
            local text = "players[0].Name"
            local dotPos = assert(text:find(".", 1, true))
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, dotPos)

            expect(changed):toBe(true)
            expect(newText):toBe("players[0]Name")
            expect(newCursor):toBe(dotPos)
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

        it("ctrl-delete on newline boundary remains no-op", function()
            local text = "foo\nbar"
            local newlinePos = assert(text:find("\n", 1, true))
            local newText, newCursor, changed = TextUtils.deleteNextWordSegment(text, newlinePos)

            expect(changed):toBe(false)
            expect(newText):toBe(text)
            expect(newCursor):toBe(newlinePos)
        end)

        it("ctrl-delete helper still prioritizes active selection deletion", function()
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

        it("ctrl-delete helper preserves quote-adjacent token boundary behavior", function()
            local text = "game.GetService(\"Players\")"
            local quotePos = assert(text:find("\"", 1, true))
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlDeleteEdit(text, quotePos, -1)

            expect(changed):toBe(true)
            expect(newText):toBe("game.GetService(Players\")")
            expect(newCursor):toBe(quotePos)
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

        it("ctrl-backspace at closing bracket deletes only bracket boundary", function()
            local text = "data[0]"
            local cursor = #text + 1
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, cursor)

            expect(changed):toBe(true)
            expect(newText):toBe("data[0")
            expect(newCursor):toBe(#newText + 1)
        end)

        it("ctrl-backspace at member/index boundary deletes only dot punctuation", function()
            local text = "players[0].Name"
            local dotPos = assert(text:find(".", 1, true))
            local newText, newCursor, changed = TextUtils.deletePrevWordSegment(text, dotPos + 1)

            expect(changed):toBe(true)
            expect(newText):toBe("players[0]Name")
            expect(newCursor):toBe(dotPos)
        end)

        it("ctrl-backspace helper deletes selected range as one edit", function()
            local text = "alpha beta"
            local newText, newCursor, newSelection, changed = TextUtils.computeCtrlBackspaceEdit(text, 1, 7)

            expect(changed):toBe(true)
            expect(newText):toBe("beta")
            expect(newCursor):toBe(1)
            expect(newSelection):toBe(-1)
        end)

        it("ctrl-backspace helper keeps newline boundary guard after refactor", function()
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

        it("ctrl-backspace removes chained member call tokens one segment at a time", function()
            local text = "var l = playersService.LocalPlayer.AccountAge.ToString();"

            local t1, c1, ch1 = TextUtils.deletePrevWordSegment(text, #text + 1)
            expect(ch1):toBe(true)
            expect(t1):toBe("var l = playersService.LocalPlayer.AccountAge.ToString()")
            expect(c1):toBe(#t1 + 1)

            local t2, c2, ch2 = TextUtils.deletePrevWordSegment(t1, #t1 + 1)
            expect(ch2):toBe(true)
            expect(t2):toBe("var l = playersService.LocalPlayer.AccountAge.ToString(")
            expect(c2):toBe(#t2 + 1)

            local t3, c3, ch3 = TextUtils.deletePrevWordSegment(t2, #t2 + 1)
            expect(ch3):toBe(true)
            expect(t3):toBe("var l = playersService.LocalPlayer.AccountAge.ToString")
            expect(c3):toBe(#t3 + 1)

            local t4, c4, ch4 = TextUtils.deletePrevWordSegment(t3, #t3 + 1)
            expect(ch4):toBe(true)
            expect(t4):toBe("var l = playersService.LocalPlayer.AccountAge.")
            expect(c4):toBe(#t4 + 1)
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

        it("keeps shortcut modifier down true when live state is true even if reported is false", function()
            expect(TextUtils.resolveShortcutModifierDown(false, true, false)):toBe(true)
        end)

        it("keeps shortcut modifier down false when reported and live are false", function()
            expect(TextUtils.resolveShortcutModifierDown(false, false, false)):toBe(false)
        end)

        it("does not trust stale cached modifier when reported and live are false", function()
            expect(TextUtils.resolveShortcutModifierDown(false, false, true)):toBe(false)
        end)

        it("falls back to cached modifier only when reported state is unavailable", function()
            expect(TextUtils.resolveShortcutModifierDown(nil, false, true)):toBe(true)
            expect(TextUtils.resolveShortcutModifierDown(nil, false, false)):toBe(false)
        end)

        it("keeps reported true shortcut modifier down regardless of live/cache", function()
            expect(TextUtils.resolveShortcutModifierDown(true, false, false)):toBe(true)
            expect(TextUtils.resolveShortcutModifierDown(true, true, true)):toBe(true)
        end)

        it("sanitizes selection for ctrl-delete word shortcut", function()
            local selection = TextUtils.sanitizeWordDeleteSelection(20, 1, "Delete", true, false)
            expect(selection):toBe(-1)
        end)

        it("sanitizes selection for ctrl-backspace word shortcut", function()
            local selection = TextUtils.sanitizeWordDeleteSelection(20, 1, "Backspace", true, false)
            expect(selection):toBe(-1)
        end)

        it("keeps selection for non-word-delete shortcuts", function()
            local selection = TextUtils.sanitizeWordDeleteSelection(20, 1, "Delete", false, false)
            expect(selection):toBe(1)
        end)

        it("prefers sanitized selection for shortcut delete detection", function()
            local selectionForDetect = TextUtils.resolveWordDeleteSelectionForDetect(1, -1, true)
            expect(selectionForDetect):toBe(-1)
        end)

        it("prefers raw selection for non-shortcut delete detection", function()
            local selectionForDetect = TextUtils.resolveWordDeleteSelectionForDetect(1, -1, false)
            expect(selectionForDetect):toBe(1)
        end)

        it("falls back to sanitized selection when raw selection is unavailable", function()
            local selectionForDetect = TextUtils.resolveWordDeleteSelectionForDetect(nil, -1, false)
            expect(selectionForDetect):toBe(-1)
        end)

        it("deduplicates pending word-delete snapshot across different sources inside dedupe window", function()
            local shouldDedupe = TextUtils.shouldDeduplicateWordDeleteSnapshot({
                keyCode = "Backspace",
                source = "textbox",
                createdAt = 10,
            }, "Backspace", "uis", 10.01, 0.05)
            expect(shouldDedupe):toBe(true)
        end)

        it("does not deduplicate pending word-delete snapshot from same source", function()
            local shouldDedupe = TextUtils.shouldDeduplicateWordDeleteSnapshot({
                keyCode = "Backspace",
                source = "uis",
                createdAt = 10,
            }, "Backspace", "uis", 10.01, 0.05)
            expect(shouldDedupe):toBe(false)
        end)

        it("does not deduplicate pending word-delete snapshot outside dedupe window", function()
            local shouldDedupe = TextUtils.shouldDeduplicateWordDeleteSnapshot({
                keyCode = "Backspace",
                source = "textbox",
                createdAt = 10,
            }, "Backspace", "uis", 10.2, 0.05)
            expect(shouldDedupe):toBe(false)
        end)

        it("treats matching selection splice as active selection", function()
            expect(TextUtils.isSelectionDeleteSplice(20, 1, 1)):toBe(true)
        end)

        it("treats mismatched selection splice as stale selection", function()
            expect(TextUtils.isSelectionDeleteSplice(20, 1, 12)):toBe(false)
        end)

        it("ignores missing or collapsed selection", function()
            expect(TextUtils.isSelectionDeleteSplice(20, -1, 20)):toBe(false)
            expect(TextUtils.isSelectionDeleteSplice(20, 20, 20)):toBe(false)
        end)

        it("repairs suspicious newline-spanning deletes without selection", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "entire line\n", "", false)):toBe(true)
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "\nwhole line", "", false)):toBe(true)
        end)

        it("repairs multi-char non-newline backspace delete without shortcut confirmation", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "wholetoken", "", false)):toBe(true)
        end)

        it("repairs punctuation-rich backspace delete without shortcut confirmation", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "playersService.LocalPlayer.AccountAge.ToString();", "", false)):toBe(true)
        end)

        it("repairs suspicious multi-char non-newline delete without shortcut confirmation", function()
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "game.GetService(\"Players\");", "", false)):toBe(true)
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "abc", "", false)):toBe(true)
        end)

        it("does not repair normal single-char delete or selection deletes", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "x", "", false)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Backspace", true, "many chars", "", false)):toBe(false)
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "abc", "x", false)):toBe(false)
        end)

        it("repairs shortcut delete when native path only removed one character", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "x", "", true)):toBe(true)
        end)

        it("does not repair shortcut backspace when native path already removed a token", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "Players", "", true)):toBe(false)
        end)

        it("repairs shortcut backspace when native path removes chained expression tails", function()
            expect(TextUtils.shouldRepairWordDelete("Backspace", false, "playersService.LocalPlayer.AccountAge.ToString();", "", true)):toBe(true)
        end)

        it("repairs shortcut delete when native path removed a whole expression tail", function()
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "game.GetService(\"Players\");", "", true)):toBe(true)
        end)

        it("repairs shortcut delete when native path already removed a token", function()
            expect(TextUtils.shouldRepairWordDelete("Delete", false, "game", "", true)):toBe(true)
        end)

        it("does not repair shortcut delete when native path removed only semicolon", function()
            expect(TextUtils.shouldRepairWordDelete("Delete", false, ";", "", true)):toBe(false)
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

        it("selects string segment on smart double-click", function()
            local source = "print(\"hello world from luasharp\");"
            local cursorPos = source:find("world", 1, true) + 2
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("world")
        end)

        it("expands smart double-click inside string to full line", function()
            local source = "print(\"hello world\");\nnextLine();"
            local cursorPos = source:find("world", 1, true) + 1
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, true)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("print(\"hello world\");")
        end)

        it("does not apply smart double-click selection on punctuation", function()
            local source = "var players = game.GetService(\"Players\");"
            local cursorPos = source:find("=", 1, true)
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toBeNil()
            expect(endPosExclusive):toBeNil()
        end)

        it("selects left identifier when cursor is inside game in member chain", function()
            local source = "var players = game.GetService(\"Workspace\");"
            local cursorPos = source:find("game", 1, true) + 1
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("game")
        end)

        it("selects method identifier when cursor is inside GetService", function()
            local source = "var players = game.GetService(\"Workspace\");"
            local cursorPos = source:find("GetService", 1, true) + 3
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("GetService")
        end)

        it("expands to full line when expand mode is requested", function()
            local source = "var svc = game.GetService(\"Workspace\");"
            local cursorPos = source:find("GetService", 1, true) + 4
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, true)

            expect(startPos):toBe(1)
            expect(endPosExclusive):toBe(#source + 1)
            expect(source:sub(startPos, endPosExclusive - 1)):toBe(source)
        end)

        it("expands method declaration click to full line in expand mode", function()
            local source = "public void OnPlayerJoined(Players p)"
            local cursorPos = source:find("OnPlayerJoined", 1, true) + 2
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, true)

            expect(startPos):toBe(1)
            expect(endPosExclusive):toBe(#source + 1)
            expect(source:sub(startPos, endPosExclusive - 1)):toBe(source)
        end)

        it("selects string inner text Workspace without quotes", function()
            local source = "var players = game.GetService(\"Workspace\");"
            local cursorPos = source:find("Workspace", 1, true) + 2
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("Workspace")
        end)

        it("selects identifier before index accessor", function()
            local source = "data[0] = 3"
            local cursorPos = source:find("data", 1, true) + 2
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("data")
        end)

        it("does not select token when cursor lands on dot separator", function()
            local source = "var svc = game.GetService(\"Workspace\");"
            local dotPos = source:find(".", source:find("game", 1, true), true)

            expect(dotPos):toNotBeNil()

            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, dotPos, false)

            expect(startPos):toBeNil()
            expect(endPosExclusive):toBeNil()
        end)

        it("does not force identifier expansion when cursor lands on interpolated string quote boundary", function()
            local source = "Console.WriteLine($\"{p.Name} has joined the game!\");"
            local quotePos = source:find("\"", source:find("WriteLine", 1, true), true)

            expect(quotePos):toNotBeNil()

            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, quotePos, false)

            expect(startPos):toBeNil()
            expect(endPosExclusive):toBeNil()
        end)

        it("selects identifier token inside interpolation expression", function()
            local source = "Console.WriteLine($\"{p.Name} has joined the game!\");"
            local namePos = source:find("Name", 1, true)

            expect(namePos):toNotBeNil()

            local cursorPos = namePos + 1
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursorPos, false)

            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("Name")
        end)

        it("respects custom stop characters at quote boundary", function()
            local source = "Console.WriteLine($\"{p.Name} has joined the game!\");"
            local quotePos = source:find("\"", source:find("$\"", 1, true), true)
            local stopChars = {
                ["\""] = true,
                ["{"] = true,
                ["}"] = true,
                ["$"] = true,
                ["("] = true,
                [")"] = true,
                ["."] = true,
                [":"] = true,
                [","] = true,
                [";"] = true,
            }

            expect(quotePos):toNotBeNil()

            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, quotePos, false, stopChars)

            expect(startPos):toBeNil()
            expect(endPosExclusive):toBeNil()
        end)

        it("suppresses duplicate primary mouse down events within age and cursor thresholds", function()
            local shouldIgnore = TextUtils.shouldIgnoreDuplicatePrimaryMouseDown(10.0, 42, 10.01, 42, 0.03, 1)
            expect(shouldIgnore):toBe(true)
        end)

        it("does not suppress primary mouse down events outside dedupe age threshold", function()
            local shouldIgnore = TextUtils.shouldIgnoreDuplicatePrimaryMouseDown(10.0, 42, 10.05, 42, 0.03, 1)
            expect(shouldIgnore):toBe(false)
        end)

        it("does not suppress primary mouse down events beyond cursor delta threshold", function()
            local shouldIgnore = TextUtils.shouldIgnoreDuplicatePrimaryMouseDown(10.0, 42, 10.01, 45, 0.03, 1)
            expect(shouldIgnore):toBe(false)
        end)

        it("treats slower repeat click as valid smart double-click within relaxed window", function()
            local isDouble = TextUtils.isSmartDoubleClick(10.85, 10.0, 200, 202, 0.9, 24)
            expect(isDouble):toBe(true)
        end)

        it("does not treat delayed repeat click as smart double-click outside relaxed window", function()
            local isDouble = TextUtils.isSmartDoubleClick(11.20, 10.0, 200, 202, 0.9, 24)
            expect(isDouble):toBe(false)
        end)

        it("expands double-click cycle when third click stays within expanded cursor delta", function()
            local shouldExpand = TextUtils.shouldExpandSmartDoubleClickCycle(
                11.40,
                214,
                { cursorPos = 202, lastAt = 10.0, expandToLine = false },
                1.5,
                12
            )
            expect(shouldExpand):toBe(true)
        end)

        it("does not expand double-click cycle when third click cursor delta is too large", function()
            local shouldExpand = TextUtils.shouldExpandSmartDoubleClickCycle(
                11.40,
                230,
                { cursorPos = 202, lastAt = 10.0, expandToLine = false },
                1.5,
                12
            )
            expect(shouldExpand):toBe(false)
        end)

        it("promotes slow third click to expand cycle when prior double-click exists", function()
            local shouldPromote = TextUtils.shouldPromoteSmartDoubleClickCycleExpand(
                44.60,
                365,
                { cursorPos = 365, lastAt = 42.566, expandToLine = false, pending = nil },
                2.4,
                12
            )
            expect(shouldPromote):toBe(true)
        end)

        it("resolves second click in streak to word mode", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.85,
                200,
                { at = 10.0, cursorPos = 200, count = 1 },
                1.3,
                2.4,
                12
            )

            expect(clickState.count):toBe(2)
            expect(clickState.mode):toBe("word")
            expect(clickState.inSequence):toBe(true)
        end)

        it("ignores duplicate source event after first click", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.02,
                200,
                { at = 10.0, cursorPos = 200, count = 1 },
                1.3,
                2.4,
                12,
                2,
                0.05,
                2
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("ignores duplicate source event after second click", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.02,
                200,
                { at = 10.0, cursorPos = 200, count = 2 },
                1.3,
                2.4,
                12,
                2,
                0.05,
                2
            )

            expect(clickState.count):toBe(2)
            expect(clickState.mode):toBe("word")
            expect(clickState.inSequence):toBe(false)
        end)

        it("resolves third click in streak to line mode inside burst window", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.95,
                200,
                { at = 10.7, cursorPos = 200, count = 2, burstStartAt = 10.5 },
                1.3,
                2.4,
                12,
                2
            )

            expect(clickState.count):toBe(3)
            expect(clickState.mode):toBe("line")
            expect(clickState.inSequence):toBe(true)
        end)

        it("does not promote word to line when third click drifts to another token", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                12.90,
                209,
                { at = 10.85, cursorPos = 201, count = 2 },
                1.3,
                2.4,
                12,
                2
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("resets click streak when slower third click exceeds line window", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                13.80,
                200,
                { at = 10.85, cursorPos = 200, count = 2 },
                1.3,
                2.4,
                12
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("resets click streak after line-mode click before starting new sequence", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.20,
                200,
                { at = 10.0, cursorPos = 200, count = 3 },
                1.3,
                2.4,
                12
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("requires cadence tighter than grace window for second click", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                11.05,
                200,
                { at = 10.0, cursorPos = 200, count = 1 },
                0.9,
                2.0,
                12
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("does not apply second-click grace when cursor drift is large", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.90,
                214,
                { at = 10.0, cursorPos = 200, count = 1 },
                0.75,
                0.5,
                24,
                2
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("allows slower second click inside grace window even when base window is tighter", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.90,
                200,
                { at = 10.0, cursorPos = 200, count = 1 },
                0.75,
                0.5,
                24,
                2
            )

            expect(clickState.count):toBe(2)
            expect(clickState.mode):toBe("word")
            expect(clickState.inSequence):toBe(true)
        end)

        it("accepts semi-slow second click inside widened double-click window", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.72,
                200,
                { at = 10.0, cursorPos = 200, count = 1 },
                0.75,
                0.5,
                12,
                2
            )

            expect(clickState.count):toBe(2)
            expect(clickState.mode):toBe("word")
            expect(clickState.inSequence):toBe(true)
        end)

        it("accepts second click after line jump when cursor delta stays in max threshold", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.42,
                218,
                { at = 10.0, cursorPos = 200, count = 1 },
                0.75,
                0.5,
                24,
                2
            )

            expect(clickState.count):toBe(2)
            expect(clickState.mode):toBe("word")
            expect(clickState.inSequence):toBe(true)
        end)

        it("does not promote to line when click burst exceeds short window", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                11.55,
                200,
                { at = 10.9, cursorPos = 200, count = 2, burstStartAt = 10.0 },
                0.5,
                2.0,
                12,
                2
            )

            expect(clickState.count):toBe(1)
            expect(clickState.mode):toBe("character")
            expect(clickState.inSequence):toBe(false)
        end)

        it("promotes to line when click burst stays inside short window", function()
            local clickState = TextUtils.resolveSmartSelectionClickState(
                10.45,
                200,
                { at = 10.3, cursorPos = 200, count = 2, burstStartAt = 10.0 },
                0.5,
                2.0,
                12,
                2
            )

            expect(clickState.count):toBe(3)
            expect(clickState.mode):toBe("line")
            expect(clickState.inSequence):toBe(true)
        end)

        it("does not promote slow third click when cycle is already expanded", function()
            local shouldPromote = TextUtils.shouldPromoteSmartDoubleClickCycleExpand(
                44.60,
                365,
                { cursorPos = 365, lastAt = 42.566, expandToLine = true, pending = nil },
                2.4,
                12
            )
            expect(shouldPromote):toBe(false)
        end)

        it("does not promote slow third click while cycle apply is still pending", function()
            local shouldPromote = TextUtils.shouldPromoteSmartDoubleClickCycleExpand(
                44.60,
                365,
                { cursorPos = 365, lastAt = 42.566, expandToLine = false, pending = true },
                2.4,
                12
            )
            expect(shouldPromote):toBe(false)
        end)

        it("resolves line mode selection range including newline", function()
            local source = "public void A()\npublic void B()"
            local startPos, endPosExclusive = TextUtils.resolveSelectionRangeForMode(source, 8, "line", nil, true)

            expect(startPos):toBe(1)
            expect(endPosExclusive):toBe(17)
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("public void A()\n")
        end)

        it("expands drag selection by full lines in line mode", function()
            local source = "line1\nline2\nline3"
            local anchorStart, anchorEndExclusive = TextUtils.resolveSelectionRangeForMode(source, 2, "line", nil, true)
            local dragStart, dragEndExclusive = TextUtils.resolveDragSelectionForMode(
                source,
                anchorStart,
                anchorEndExclusive,
                14,
                "line",
                nil,
                true
            )

            expect(dragStart):toBe(1)
            expect(dragEndExclusive):toBe(#source + 1)
            expect(source:sub(dragStart, dragEndExclusive - 1)):toBe(source)
        end)

        it("expands drag selection by full words in word mode", function()
            local source = "OnPlayerJoined(Players p)"
            local stopChars = { ["("] = true, [")"] = true, [" "] = true }
            local anchorStart, anchorEndExclusive = TextUtils.resolveSelectionRangeForMode(source, 3, "word", stopChars, false)
            local dragStart, dragEndExclusive = TextUtils.resolveDragSelectionForMode(
                source,
                anchorStart,
                anchorEndExclusive,
                20,
                "word",
                stopChars,
                false
            )

            expect(source:sub(dragStart, dragEndExclusive - 1)):toBe("OnPlayerJoined(Players")
        end)

        it("suppresses caret hover cursor when selection is active", function()
            local shouldUseCaret = TextUtils.shouldUseCaretHoverCursor(180, 160)
            expect(shouldUseCaret):toBe(false)
        end)

        it("resolves hover cursor from active selection start", function()
            local hoverCursor = TextUtils.resolveHoverCursorFromSelection(203, 213, 441)
            expect(hoverCursor):toBe(203)
        end)

        it("does not resolve hover cursor from collapsed selection", function()
            local hoverCursor = TextUtils.resolveHoverCursorFromSelection(213, 213, 441)
            expect(hoverCursor):toBe(nil)
        end)

        it("allows caret hover cursor when selection is collapsed", function()
            local shouldUseCaret = TextUtils.shouldUseCaretHoverCursor(180, 180)
            expect(shouldUseCaret):toBe(true)
        end)

        it("rejects preferred selection cursor when text length is zero", function()
            local cursor = TextUtils.resolvePreferredSelectionCursor(1, 1, 0, true)
            expect(cursor):toBeNil()
        end)

        it("validates smart selection cycle id before deferred apply", function()
            local shouldApply = TextUtils.shouldApplySmartSelectionForCycle({ id = 4 }, 5)
            expect(shouldApply):toBe(false)

            local shouldApplyMatching = TextUtils.shouldApplySmartSelectionForCycle({ id = 5 }, 5)
            expect(shouldApplyMatching):toBe(true)
        end)

        it("rejects stale character normalization token", function()
            local shouldApply = TextUtils.shouldApplyCharacterNormalization(4, 5)
            expect(shouldApply):toBe(false)
        end)

        it("accepts matching character normalization token", function()
            local shouldApply = TextUtils.shouldApplyCharacterNormalization(5, 5)
            expect(shouldApply):toBe(true)
        end)

        it("rejects cached caret hover sample when selection is active", function()
            local shouldUseSample = TextUtils.shouldUseCachedHoverSample("caret", 180, 160)
            expect(shouldUseSample):toBe(false)
        end)

        it("allows cached non-caret hover sample when selection is active", function()
            local shouldUseSample = TextUtils.shouldUseCachedHoverSample("mouse-sample", 180, 160)
            expect(shouldUseSample):toBe(true)
        end)

        it("normalizes character click when native selection is active", function()
            local shouldNormalize = TextUtils.shouldNormalizeCharacterClickSelection(1, 441, 208, false)
            expect(shouldNormalize):toBe(true)
        end)

        it("does not normalize character click when shift is held", function()
            local shouldNormalize = TextUtils.shouldNormalizeCharacterClickSelection(1, 441, 208, true)
            expect(shouldNormalize):toBe(false)
        end)

        it("does not normalize character click when selection is collapsed", function()
            local shouldNormalize = TextUtils.shouldNormalizeCharacterClickSelection(208, 208, 208, false)
            expect(shouldNormalize):toBe(false)
        end)

        it("resolves shift-click anchor from active selection start", function()
            local anchor = TextUtils.resolveShiftSelectionAnchor("hello world", nil, 3, 9)
            expect(anchor):toBe(3)
        end)

        it("prefers persisted shift-click anchor over active selection", function()
            local anchor = TextUtils.resolveShiftSelectionAnchor("hello world", 8, 3, 9)
            expect(anchor):toBe(8)
        end)

        it("uses current cursor as shift-click anchor when no active selection", function()
            local anchor = TextUtils.resolveShiftSelectionAnchor("hello world", nil, -1, 6)
            expect(anchor):toBe(6)
        end)

        it("prefers pre-click caret as shift-click anchor when provided", function()
            local anchor = TextUtils.resolveShiftSelectionAnchor("hello world", nil, -1, 10, 4)
            expect(anchor):toBe(4)
        end)

        it("resolves shift-click selection endpoints with clamped cursors", function()
            local selectionStart, cursorPos = TextUtils.resolveShiftClickSelectionEndpoints("abc", 9, 2)
            expect(selectionStart):toBe(4)
            expect(cursorPos):toBe(2)
        end)

        it("returns nil shift-click endpoints when inputs are invalid", function()
            local selectionStart, cursorPos = TextUtils.resolveShiftClickSelectionEndpoints("abc", nil, 2)
            expect(selectionStart):toBeNil()
            expect(cursorPos):toBeNil()
        end)

        it("runs native retokenize fallback only inside recency window", function()
            local shouldRun = TextUtils.shouldRunNativeRetokenizeFallback(10.40, 10.0, 120, 120, 0.5, 24)
            expect(shouldRun):toBe(true)
        end)

        it("does not run native retokenize fallback when outside recency window", function()
            local shouldRun = TextUtils.shouldRunNativeRetokenizeFallback(13.0, 10.0, 120, 120, 0.5, 24)
            expect(shouldRun):toBe(false)
        end)

        it("does not run native retokenize fallback when cursor delta exceeds threshold", function()
            local shouldRun = TextUtils.shouldRunNativeRetokenizeFallback(10.40, 10.0, 369, 360, 0.5, 4)
            expect(shouldRun):toBe(false)
        end)

        it("prefers live cursor for selection when valid", function()
            local cursor = TextUtils.resolvePreferredSelectionCursor(10, 20, 40)
            expect(cursor):toBe(20)
        end)

        it("falls back to initial cursor when live cursor is invalid", function()
            local cursor = TextUtils.resolvePreferredSelectionCursor(10, 200, 40)
            expect(cursor):toBe(10)
        end)

        it("prefers initial cursor when explicitly requested", function()
            local cursor = TextUtils.resolvePreferredSelectionCursor(10, 20, 40, true)
            expect(cursor):toBe(10)
        end)

        it("falls back to live cursor when initial preference is invalid", function()
            local cursor = TextUtils.resolvePreferredSelectionCursor(200, 20, 40, true)
            expect(cursor):toBe(20)
        end)

        it("retokenize cursor prefers later valid candidate inside selection", function()
            local source = "game.GetService(\"Workspace\");"
            local servicePos = source:find("Service", 1, true)
            local cursor = TextUtils.resolveRetokenizeCursorForSelection(
                source,
                1,
                #source + 1,
                { #source, servicePos },
                { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true }
            )

            expect(cursor):toBe(servicePos)
        end)

        it("retokenize cursor keeps explicit click identifier over earlier method-bias candidate", function()
            local source = "var players = game.GetService(\"Workspace\");"
            local selectedStart = source:find("game", 1, true)
            local selectedEndExclusive = source:find(";", 1, true)
            local clickCursor = selectedStart + 1
            local methodBiasCursor = source:find("Service", 1, true)

            local cursor = TextUtils.resolveRetokenizeCursorForSelection(
                source,
                selectedStart,
                selectedEndExclusive,
                { methodBiasCursor, clickCursor },
                { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true }
            )

            expect(cursor):toBe(clickCursor)
        end)

        it("retokenize cursor honors sparse preferred cursor list after leading nil", function()
            local source = "game.GetService(\"Workspace\");"
            local servicePos = source:find("Service", 1, true)

            local cursor = TextUtils.resolveRetokenizeCursorForSelection(
                source,
                1,
                #source + 1,
                { nil, servicePos },
                { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true }
            )

            expect(cursor):toBe(servicePos)
        end)

        it("retokenize search range expands to line when preferred cursor is outside native selection", function()
            local source = "var players = game.GetService(\"Workspace\");\nnextLine();"
            local gameStart = source:find("game", 1, true)
            local gameEndExclusive = gameStart + #("game")
            local methodPos = source:find("GetService", 1, true)

            local rangeStart, rangeEndExclusive = TextUtils.resolveRetokenizeSearchRange(
                source,
                gameStart,
                gameEndExclusive,
                { methodPos }
            )

            expect(rangeStart):toBe(1)
            expect(rangeEndExclusive):toBe(source:find("\n", 1, true))
        end)

        it("retokenize cursor resolves to an identifier token when candidate lands on punctuation", function()
            local source = "game.GetService(\"Workspace\");"
            local stopChars = { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true }
            local cursor = TextUtils.resolveRetokenizeCursorForSelection(
                source,
                1,
                #source + 1,
                #source,
                stopChars
            )

            expect(cursor):toNotBeNil()
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursor, false, stopChars)
            expect(startPos):toNotBeNil()
            expect(endPosExclusive):toNotBeNil()
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("Workspace")
        end)

        it("retokenize post-processing prefers right-side member when candidate is on dot separator", function()
            local source = "game.GetService(\"Workspace\");"
            local dotPos = source:find(".", 1, true)

            expect(dotPos):toNotBeNil()

            -- This covers retokenize recovery after an initial delimiter-adjacent cursor,
            -- not direct dot-delimiter click behavior.
            local cursor = TextUtils.resolveRetokenizeCursorForSelection(
                source,
                1,
                #source + 1,
                dotPos,
                { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true }
            )

            expect(cursor):toNotBeNil()
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursor, false, {
                ["."] = true,
                ["("] = true,
                [")"] = true,
                ["\""] = true,
                [";"] = true,
            })
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("GetService")
        end)

        it("finds method identifier cursor in call range", function()
            local source = "game.GetService(\"Workspace\");"
            local cursor = TextUtils.findMethodIdentifierCursorInRange(source, 1, #source + 1)

            expect(cursor):toNotBeNil()
            local startPos, endPosExclusive = TextUtils.computeStringDoubleClickSelection(source, cursor, false, { ["."] = true, ["("] = true, [")"] = true, ["\""] = true, [";"] = true })
            expect(source:sub(startPos, endPosExclusive - 1)):toBe("GetService")
        end)

        it("returns nil method cursor when range has no call expression", function()
            local source = "game.Workspace;"
            local cursor = TextUtils.findMethodIdentifierCursorInRange(source, 1, #source + 1)
            expect(cursor):toBeNil()
        end)
    end)
end
