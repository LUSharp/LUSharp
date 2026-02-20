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
    end)
end
