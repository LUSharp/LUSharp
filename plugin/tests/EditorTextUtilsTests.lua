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

        it("resolves tab action as acceptCompletion when completion is visible and text is selected", function()
            local action = TextUtils.resolveTabAction(true, true)
            expect(action):toBe("acceptCompletion")
        end)

        it("resolves tab action as insertIndent when completion is visible and there is no selection", function()
            local action = TextUtils.resolveTabAction(true, false)
            expect(action):toBe("insertIndent")
        end)

        it("resolves tab action as insertIndent when completion is hidden and text is selected", function()
            local action = TextUtils.resolveTabAction(false, true)
            expect(action):toBe("insertIndent")
        end)

        it("resolves tab action as insertIndent when completion is hidden and there is no selection", function()
            local action = TextUtils.resolveTabAction(false, false)
            expect(action):toBe("insertIndent")
        end)

        it("normalizes snippet placeholders into plain text", function()
            local snippet = "for (int ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++)\n{\n    $0\n}"
            local normalized = TextUtils.normalizeSnippetText(snippet)
            expect(normalized):toBe("for (int i = 0; i < length; i++)\n{\n    \n}")
        end)

        it("chooses dotted replace prefix for dotted completion insertText", function()
            local before = "using System."
            local prefix = TextUtils.getCompletionReplacePrefix(before, "System.Collections")
            expect(prefix):toBe("System.")
        end)

        it("chooses identifier replace prefix for non-dotted insertText", function()
            local before = "Console.Wri"
            local prefix = TextUtils.getCompletionReplacePrefix(before, "WriteLine")
            expect(prefix):toBe("Wri")
        end)
    end)
end
