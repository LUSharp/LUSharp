local IntelliSense = require("../src/IntelliSense")

local function hasLabel(completions, label)
    for _, item in ipairs(completions or {}) do
        if item.label == label then
            return true
        end
    end
    return false
end

local function firstByLabel(completions, label)
    for _, item in ipairs(completions or {}) do
        if item.label == label then
            return item
        end
    end
    return nil
end

local function run(describe, it, expect)
    describe("IntelliSense", function()
        it("returns keyword completions by prefix", function()
            local source = "cl"
            local completions = IntelliSense.getCompletions(source, #source + 1)
            expect(hasLabel(completions, "class")):toBe(true)
        end)

        it("returns member completions after dot from inferred local type", function()
            local source = [[
class Foo {
    public void M() {
        Globals g;
        g.
    }
}
]]
            local completions = IntelliSense.getCompletions(source, #source + 1)
            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("returns constructable type completions after new", function()
            local source = "new Vec"
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local sawType = false
            for _, item in ipairs(completions) do
                if item.kind == "type" then
                    sawType = true
                    break
                end
            end

            expect(sawType):toBe(true)
        end)

        it("includes type completions in default context", function()
            local source = ""
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local sawType = false
            for _, item in ipairs(completions) do
                if item.kind == "type" then
                    sawType = true
                    expect(item.documentation):toNotBeNil()
                    break
                end
            end

            expect(sawType):toBe(true)
        end)

        it("returns parameter hints for global methods", function()
            local source = "print(\"hello\", "
            local hints = IntelliSense.getParameterHints(source, #source + 1)
            expect(hints):toNotBeNil()
            expect(hints.methodName):toBe("print")
            expect(hints.activeParam):toBe(2)
            expect(#hints.parameters >= 1):toBe(true)
        end)

        it("does not count commas inside strings for active parameter", function()
            local source = "print(\"a,b\", "
            local hints = IntelliSense.getParameterHints(source, #source + 1)
            expect(hints):toNotBeNil()
            expect(hints.activeParam):toBe(2)
        end)

        it("returns diagnostics from parse results", function()
            local parseResult = {
                diagnostics = {
                    {
                        severity = "error",
                        message = "Unexpected token",
                        line = 4,
                        column = 12,
                    },
                },
            }

            local diagnostics = IntelliSense.getDiagnostics(parseResult)
            expect(#diagnostics):toBe(1)
            expect(diagnostics[1].severity):toBe("error")
            expect(diagnostics[1].message):toBe("Unexpected token")
            expect(diagnostics[1].line):toBe(4)
            expect(diagnostics[1].column):toBe(12)
        end)

        it("supports manual symbol type caching", function()
            IntelliSense.resetSymbolTable()
            expect(IntelliSense.setSymbolType("player", "Player")):toBe(true)
            expect(IntelliSense.getSymbolType("player")):toBe("Player")

            local source = "player."
            local completions = IntelliSense.getCompletions(source, #source + 1)
            expect(#completions > 0):toBe(true)
        end)

        it("exposes detail field for completion entries", function()
            local source = "print"
            local completions = IntelliSense.getCompletions(source, #source + 1)
            local printEntry = firstByLabel(completions, "print")
            expect(printEntry):toNotBeNil()
            expect(printEntry.detail):toNotBeNil()
        end)
    end)
end

return run
