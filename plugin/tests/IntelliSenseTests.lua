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
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "class")):toBe(true)
        end)

        it("prioritizes Main when naming a class declaration", function()
            local source = "class Ma"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(#completions > 0):toBe(true)
            expect(completions[1].label):toBe("Main")
        end)

        it("prioritizes GameEntry for Main entry method declaration", function()
            local source = "class Main {\n    public void Ga"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(#completions > 0):toBe(true)
            expect(completions[1].label):toBe("GameEntry")
        end)

        it("prioritizes Console.WriteLine for static member access", function()
            local source = "Console.Wri"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "WriteLine")):toBe(true)
            expect(completions[1].label):toBe("WriteLine")
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
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("returns constructable type completions after new", function()
            local source = "new Vec"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

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
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

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

        it("includes baseline dotnet namespaces and types in default context", function()
            local source = ""
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "System")):toBe(true)
            expect(hasLabel(completions, "String")):toBe(true)
            expect(hasLabel(completions, "Int32")):toBe(true)
            expect(hasLabel(completions, "List")):toBe(true)
            expect(hasLabel(completions, "Dictionary")):toBe(true)
        end)

        it("returns namespace-aware completions after System dot prefix", function()
            local source = "System."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "String")):toBe(true)
            expect(hasLabel(completions, "Int32")):toBe(true)
            expect(hasLabel(completions, "Collections")):toBe(true)
        end)

        it("returns generic collection types under System.Collections.Generic", function()
            local source = "System.Collections.Generic."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "List")):toBe(true)
            expect(hasLabel(completions, "Dictionary")):toBe(true)
        end)

        it("accepts context options without changing baseline results", function()
            local source = "cl"

            local defaultCompletions = IntelliSense.getCompletions(source, #source + 1, nil)
            local ctxCompletions = IntelliSense.getCompletions(source, #source + 1, { context = "Client" })

            expect(hasLabel(defaultCompletions, "class")):toBe(true)
            expect(hasLabel(ctxCompletions, "class")):toBe(true)
        end)

        it("suggests service names inside GetService string", function()
            local source = "game.GetService(\"Pla"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Players")):toBe(true)
        end)

        it("limits GetService suggestions to default visible services", function()
            local source = "game.GetService(\"Wor"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Workspace")):toBe(true)

            local hiddenSource = "game.GetService(\"Http"
            local hiddenCompletions = IntelliSense.getCompletions(hiddenSource, #hiddenSource + 1, nil)
            expect(hasLabel(hiddenCompletions, "HttpService")):toBe(false)
        end)

        it("includes additional services when explicitly enabled", function()
            local source = "game.GetService(\"Http"
            local completions = IntelliSense.getCompletions(source, #source + 1, {
                visibleServices = {
                    "Workspace",
                    "Players",
                    "HttpService",
                },
            })
            expect(hasLabel(completions, "HttpService")):toBe(true)
        end)

        it("does not suggest completions inside comments", function()
            local source = "// cl"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(#completions):toBe(0)
        end)

        it("does not suggest completions inside strings unless provider exists", function()
            local source = "var s = \"cl"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(#completions):toBe(0)
        end)

        it("suggests class names inside Instance.new string", function()
            local source = "Instance.new(\"Par"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Part")):toBe(true)
        end)

        it("suggests members after GetService call when using dot chain", function()
            local source = "game.GetService(\"Players\")."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "PlayerAdded")):toBe(true)
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
                        endLine = 4,
                        endColumn = 17,
                        length = 5,
                    },
                },
            }

            local diagnostics = IntelliSense.getDiagnostics(parseResult)
            expect(#diagnostics):toBe(1)
            expect(diagnostics[1].severity):toBe("error")
            expect(diagnostics[1].message):toBe("Unexpected token")
            expect(diagnostics[1].line):toBe(4)
            expect(diagnostics[1].column):toBe(12)
            expect(diagnostics[1].endLine):toBe(4)
            expect(diagnostics[1].endColumn):toBe(17)
            expect(diagnostics[1].length):toBe(5)
        end)

        it("supports manual symbol type caching", function()
            IntelliSense.resetSymbolTable()
            expect(IntelliSense.setSymbolType("player", "Player")):toBe(true)
            expect(IntelliSense.getSymbolType("player")):toBe("Player")

            local source = "player."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(#completions > 0):toBe(true)
        end)

        it("exposes detail field for completion entries", function()
            local source = "print"
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            local printEntry = firstByLabel(completions, "print")
            expect(printEntry):toNotBeNil()
            expect(printEntry.detail):toNotBeNil()
        end)

        it("returns hover info for member methods", function()
            local source = "game.GetService"
            local hoverPos = source:find("GetService", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.label):toBe("GetService")
            expect(info.kind):toBe("method")
        end)

        it("returns hover info for local variable symbols", function()
            local source = "Globals g; g"
            local hoverPos = #source
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.label):toBe("g")
            expect(info.kind):toBe("variable")
            expect(info.detail):toContain("Globals")
        end)

        it("finds nearby hover symbol when cursor is offset", function()
            local source = "game.GetService     "
            local hoverPos = #source
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("GetService")
            expect(info.kind):toBe("method")
        end)

        it("finds hover symbol via token-nearby fallback when char radius is too small", function()
            local source = "game.GetService" .. string.rep(" ", 80)
            local hoverPos = #source
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("GetService")
            expect(info.kind):toBe("method")
        end)

        it("returns method hover for declaration name", function()
            local source = "class Main { public void GameEntry() { } }"
            local hoverPos = source:find("GameEntry", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("GameEntry")
            expect(info.kind):toBe("method")
            expect(info.detail):toContain("void")
        end)

        it("prefers method name over return type when hovering declaration gap", function()
            local source = "class Main { public void GameEntry() { } }"
            local hoverPos = source:find(" GameEntry", 1, true)
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("GameEntry")
            expect(info.kind):toBe("method")
        end)

        it("prefers static field name over type token in declaration", function()
            local source = "class Main { public static string PlayerName = \"x\"; }"
            local hoverPos = source:find(" string", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("PlayerName")
            expect(info.kind):toBe("variable")
            expect(info.detail):toContain("string")
        end)

        it("returns keyword hover when cursor is on class keyword", function()
            local source = "class Main { }"
            local hoverPos = source:find("class", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("class")
            expect(info.kind):toBe("keyword")
        end)

        it("returns keyword hover when cursor is on namespace keyword", function()
            local source = "namespace My.Game { class Main { } }"
            local hoverPos = source:find("namespace", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("namespace")
            expect(info.kind):toBe("keyword")
        end)

        it("infers local service type from generic GetService call", function()
            local source = "var players = game.GetService<Players>(); players."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "PlayerAdded")):toBe(true)
        end)

        it("does not show hover info for public modifier token", function()
            local source = "class Main { public void GameEntry() { } }"
            local hoverPos = source:find("public", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toBeNil()
        end)

        it("shows override hover as base method definition", function()
            local source = "class BaseMain { public virtual void GameEntry() { } } class Main : BaseMain { public override void GameEntry() { } }"
            local hoverPos = source:find("override", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.kind):toBe("method")
            expect(info.label):toBe("GameEntry")
            expect(info.detail):toContain("override")
            expect(info.detail):toContain("BaseMain")
        end)

        it("shows override hover when modifiers follow override", function()
            local source = "class BaseMain { public virtual Task GameEntry() { } } class Main : BaseMain { public override async Task GameEntry() { } }"
            local hoverPos = source:find("override", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.kind):toBe("method")
            expect(info.label):toBe("GameEntry")
            expect(info.detail):toContain("override")
            expect(info.detail):toContain("Task")
            expect(info.detail):toContain("BaseMain")
        end)

        it("shows full declaration for method name hover", function()
            local source = "class Main { public override void GameEntry() { } }"
            local hoverPos = source:find("GameEntry", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.kind):toBe("method")
            expect(info.detail):toBe("public override void GameEntry()")
        end)

        it("shows class declaration for class name hover", function()
            local source = "namespace My.Game { public class Main : BaseMain { } }"
            local hoverPos = source:find("Main", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.kind):toBe("class")
            expect(info.label):toBe("Main")
            expect(info.detail):toContain("class Main : BaseMain")
            expect(info.documentation):toBeNil()
        end)

        it("does not show hover info for static modifier token", function()
            local source = "class Main { public static void GameEntry() { } }"
            local hoverPos = source:find("static", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toBeNil()
        end)

        it("does not show hover info when cursor is on an opening brace", function()
            local source = "class Main { public void GameEntry() { } }"
            local hoverPos = source:find(") {", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toBeNil()
        end)

        it("recovers nearby method hover when cursor is on whitespace before opening brace", function()
            local source = "class Main { public void GameEntry() { } }"
            local hoverPos = source:find(") {", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("GameEntry")
            expect(info.kind):toBe("method")
        end)

        it("infers local type from generic template method call", function()
            local source = "var part = Factory.Create<Part>(); part."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Anchored")):toBe(true)
        end)

        it("shows hover docs for local inferred from GetService generic", function()
            local source = "var players = game.GetService<Players>(); players"
            local hoverPos = #source - 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("variable")
            expect(info.label):toBe("players")
            expect(info.detail):toContain("Players")
            expect(info.documentation):toContain("Roblox")
            expect(info.documentation):toContain("create.roblox.com/docs/reference/engine/classes/Players")
        end)

        it("shows hover docs for local inferred from template call", function()
            local source = "var part = Factory.Create<Part>(); part"
            local hoverPos = #source - 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("variable")
            expect(info.label):toBe("part")
            expect(info.detail):toContain("Part")
            expect(info.documentation):toContain("Roblox")
            expect(info.documentation):toContain("create.roblox.com/docs/reference/engine/classes/Part")
        end)

        it("shows method hover signature with params and return type", function()
            local source = "var part = Factory.Create<Part>(); part.FindFirstChild"
            local hoverPos = source:find("FindFirstChild", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("method")
            expect(info.detail):toContain("FindFirstChild(")
            expect(info.detail):toContain("->")
        end)

        it("provides built-in Roblox type documentation on hover", function()
            local source = "Part"
            local info = IntelliSense.getHoverInfo(source, 2, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("type")
            expect(info.documentation):toContain("Roblox")
        end)

        it("provides extensible Roblox documentation for non-curated supported types", function()
            local source = "Attachment"
            local info = IntelliSense.getHoverInfo(source, 2, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("type")
            expect(info.documentation):toContain("Roblox")
            expect(info.documentation):toContain("Attachment")
        end)
    end)
end

return run
