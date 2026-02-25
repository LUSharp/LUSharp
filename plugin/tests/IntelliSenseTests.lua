local IntelliSense = require("../src/IntelliSense")
local Lexer = require("../src/Lexer")
local Parser = require("../src/Parser")

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

local function parseSource(source)
    local tokens = Lexer.tokenize(source)
    return Parser.parse(tokens)
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

        it("keeps namespace/type members in namespace chain context", function()
            local source = "System."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "Collections")):toBe(true)
            expect(hasLabel(completions, "Console")):toBe(true)
            expect(hasLabel(completions, "print")):toBe(false)
        end)

        it("suppresses non-declaration suggestions while declaring class name", function()
            local source = "class "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "Main")):toBe(true)
            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
        end)

        it("suppresses unrelated suggestions while declaring value name", function()
            local source = "int "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for generic list declaration-name context", function()
            local source = "List<int> "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for generic dictionary declaration-name context", function()
            local source = "Dictionary<string,int> "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for nullable declaration-name context", function()
            local source = "string? "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for array declaration-name context", function()
            local source = "int[] "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for custom type declaration-name context", function()
            local source = "MyCustomType "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("suppresses unrelated suggestions for namespaced custom type declaration-name context", function()
            local source = "MyNamespace.MyType "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("keeps member completion in declaration initializer expression context", function()
            local source = "var players = game."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "GetService")):toBe(true)
        end)

        it("keeps member completion in typed declaration initializer expression context", function()
            local source = "List<int> players = game."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "GetService")):toBe(true)
        end)

        it("returns generic collection types under System.Collections.Generic", function()
            local source = "System.Collections.Generic."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "List")):toBe(true)
            expect(hasLabel(completions, "Dictionary")):toBe(true)
        end)

        it("restricts using-directive root suggestions to namespaces", function()
            local source = "using "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(#completions > 0):toBe(true)
            expect(hasLabel(completions, "System")):toBe(true)

            for _, item in ipairs(completions) do
                expect(item.kind):toBe("namespace")
            end

            expect(hasLabel(completions, "String")):toBe(false)
            expect(hasLabel(completions, "print")):toBe(false)
            expect(hasLabel(completions, "class")):toBe(false)
        end)

        it("restricts using-directive nested suggestions to namespaces", function()
            local source = "using System."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(#completions > 0):toBe(true)
            expect(hasLabel(completions, "Collections")):toBe(true)

            for _, item in ipairs(completions) do
                expect(item.kind):toBe("namespace")
            end

            expect(hasLabel(completions, "Console")):toBe(false)
            expect(hasLabel(completions, "String")):toBe(false)
            expect(hasLabel(completions, "Int32")):toBe(false)
        end)

        it("surfaces includable LUSharpAPI namespaces in using directives", function()
            local source = "using LUSharpAPI.Runtime.STL."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)

            expect(hasLabel(completions, "Classes")):toBe(true)
        end)

        it("filters already-included namespaces even when prior using lacks semicolon", function()
            local source = "using System\nusing "
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "System")):toBe(false)
        end)

        it("filters already-included nested namespaces when prior using lacks semicolon", function()
            local source = "using System.Collections\nusing System."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Collections")):toBe(false)
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

        it("normalizes malformed diagnostics to stable non-zero clamped ranges", function()
            local parseResult = {
                diagnostics = {
                    {
                        severity = "warning",
                        message = "Unexpected token in class body: [",
                        line = 0,
                        column = 0,
                        endLine = -3,
                        endColumn = -8,
                        length = 0,
                    },
                },
            }

            local diagnostics = IntelliSense.getDiagnostics(parseResult)
            local first = diagnostics[1]

            expect(#diagnostics):toBe(1)
            expect(first.severity):toBe("warning")
            expect(first.message):toBe("Unexpected token in class body: [")
            expect(first.line):toBe(1)
            expect(first.column):toBe(1)
            expect(first.endLine):toBe(1)
            expect(first.endColumn):toBe(2)
            expect(first.length):toBe(1)
            expect(first.endColumn > first.column):toBe(true)
        end)

        it("normalizes inverted spans while preserving severity and message", function()
            local parseResult = {
                diagnostics = {
                    {
                        severity = "error",
                        message = "Expected punctuation ')'",
                        line = 3,
                        column = 12,
                        endLine = 3,
                        endColumn = 9,
                        length = -4,
                    },
                },
            }

            local diagnostics = IntelliSense.getDiagnostics(parseResult)
            local first = diagnostics[1]

            expect(#diagnostics):toBe(1)
            expect(first.severity):toBe("error")
            expect(first.message):toBe("Expected punctuation ')'")
            expect(first.line):toBe(3)
            expect(first.column):toBe(12)
            expect(first.endLine):toBe(3)
            expect(first.endColumn > first.column):toBe(true)
            expect(first.length):toBe(1)
        end)

        it("carries severity and message through parser diagnostics for invalid constructs", function()
            local parseResultError = parseSource("class Foo { void M( { }")
            local diagnosticsError = IntelliSense.getDiagnostics(parseResultError)
            expect(#diagnosticsError > 0):toBe(true)
            expect(diagnosticsError[1].severity):toBe("error")
            expect(diagnosticsError[1].message:find("Expected", 1, true) ~= nil):toBe(true)
            expect(diagnosticsError[1].length >= 1):toBe(true)
            expect(diagnosticsError[1].line >= 1):toBe(true)
            expect(diagnosticsError[1].column >= 1):toBe(true)
            expect(diagnosticsError[1].endLine >= diagnosticsError[1].line):toBe(true)

            local parseResultWarning = parseSource("class C { [ }")
            local diagnosticsWarning = IntelliSense.getDiagnostics(parseResultWarning)
            expect(#diagnosticsWarning > 0):toBe(true)

            local sawUnexpectedWarning = false
            for _, diagnostic in ipairs(diagnosticsWarning) do
                if diagnostic.severity == "warning" and diagnostic.message:find("Unexpected token", 1, true) ~= nil then
                    sawUnexpectedWarning = true
                    expect(diagnostic.length >= 1):toBe(true)
                    expect(diagnostic.line >= 1):toBe(true)
                    expect(diagnostic.column >= 1):toBe(true)
                    expect(diagnostic.endLine >= diagnostic.line):toBe(true)
                    break
                end
            end

            expect(sawUnexpectedWarning):toBe(true)
        end)

        it("adds diagnostics for unknown using namespace include", function()
            local source = "using NotAReal.Namespace;\nclass Main { }"
            local parseResult = parseSource(source)
            local diagnostics = IntelliSense.getDiagnostics(parseResult, source)

            local unknownUsingDiagnostic = nil
            for _, diagnostic in ipairs(diagnostics) do
                if tostring(diagnostic.severity) == "error"
                    and tostring(diagnostic.message):find("Unknown using namespace", 1, true) ~= nil then
                    unknownUsingDiagnostic = diagnostic
                    break
                end
            end

            expect(unknownUsingDiagnostic):toNotBeNil()
            expect(unknownUsingDiagnostic.line):toBe(1)
            expect(unknownUsingDiagnostic.column):toBe(source:find("NotAReal.Namespace", 1, true))
            expect(unknownUsingDiagnostic.endColumn > unknownUsingDiagnostic.column):toBe(true)
        end)

        it("adds diagnostics for undeclared identifiers", function()
            local source = "class Main { void GameEntry() { missingValue = 1; } }"
            local parseResult = parseSource(source)
            local diagnostics = IntelliSense.getDiagnostics(parseResult, source)

            local undeclaredIdentifierDiagnostic = nil
            for _, diagnostic in ipairs(diagnostics) do
                if tostring(diagnostic.severity) == "error"
                    and tostring(diagnostic.message):find("Undeclared identifier", 1, true) ~= nil
                    and tostring(diagnostic.message):find("missingValue", 1, true) ~= nil then
                    undeclaredIdentifierDiagnostic = diagnostic
                    break
                end
            end

            expect(undeclaredIdentifierDiagnostic):toNotBeNil()
            expect(undeclaredIdentifierDiagnostic.line):toBe(1)
            expect(undeclaredIdentifierDiagnostic.column):toBe(source:find("missingValue", 1, true))
            expect(undeclaredIdentifierDiagnostic.endColumn > undeclaredIdentifierDiagnostic.column):toBe(true)
        end)

        it("adds diagnostics for using loop variable outside scope", function()
            local source = "class Main { void GameEntry() { for (int i = 0; i < 1; i++) { } i = 3; } }"
            local parseResult = parseSource(source)
            local diagnostics = IntelliSense.getDiagnostics(parseResult, source)

            local outOfScopeIdentifierDiagnostic = nil
            for _, diagnostic in ipairs(diagnostics) do
                if tostring(diagnostic.severity) == "error"
                    and tostring(diagnostic.message):find("out of scope", 1, true) ~= nil
                    and tostring(diagnostic.message):find("i", 1, true) ~= nil then
                    outOfScopeIdentifierDiagnostic = diagnostic
                    break
                end
            end

            expect(outOfScopeIdentifierDiagnostic):toNotBeNil()
            expect(outOfScopeIdentifierDiagnostic.line):toBe(1)
            expect(outOfScopeIdentifierDiagnostic.column):toBe(source:find("i = 3", 1, true))
            expect(outOfScopeIdentifierDiagnostic.endColumn > outOfScopeIdentifierDiagnostic.column):toBe(true)
        end)

        it("does not mark print inside lambda callback as undeclared", function()
            local source = "class Main { void GameEntry() { Players players = game.GetService(\"Players\"); players.DescendantAdded.Connect(() => { print(\"hi\"); }); print(\"ok\"); } }"
            local parseResult = parseSource(source)
            local diagnostics = IntelliSense.getDiagnostics(parseResult, source)

            local sawUndeclaredPrint = false
            for _, diagnostic in ipairs(diagnostics) do
                if tostring(diagnostic.severity) == "error"
                    and tostring(diagnostic.message):find("Undeclared identifier", 1, true) ~= nil
                    and tostring(diagnostic.message):find("print", 1, true) ~= nil then
                    sawUndeclaredPrint = true
                    break
                end
            end

            expect(sawUndeclaredPrint):toBe(false)
        end)

        it("returns diagnostic hover info when cursor is inside diagnostic span", function()
            local source = "class Main { void GameEntry() { missingValue = 1; } }"
            local parseResult = parseSource(source)
            local diagnostics = IntelliSense.getDiagnostics(parseResult, source)
            local hoverPos = source:find("missingValue", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, {
                diagnostics = diagnostics,
            })

            expect(info):toNotBeNil()
            expect(info.kind):toBe("error")
            expect(info.label):toContain("Error")
            expect(info.detail):toContain("missingValue")
        end)

        it("uses friendly fallback detail when semantic diagnostic message is missing", function()
            local source = "class Main { }"
            local column = source:find("Main", 1, true)
            local info = IntelliSense.getHoverInfo(source, column + 1, {
                diagnostics = {
                    {
                        severity = "error",
                        code = "semantic.undeclared_identifier",
                        line = 1,
                        column = column,
                        endLine = 1,
                        endColumn = column + #"Main",
                        length = #"Main",
                    },
                },
            })

            expect(info):toNotBeNil()
            expect(info.kind):toBe("error")
            expect(info.detail):toBe("Undeclared identifier.")
            expect(info.documentation):toContain("semantic.undeclared_identifier")
        end)

        it("keeps human-readable diagnostic messages as primary hover detail", function()
            local source = "class Main { }"
            local column = source:find("Main", 1, true)
            local info = IntelliSense.getHoverInfo(source, column + 1, {
                diagnostics = {
                    {
                        severity = "error",
                        message = "Identifier 'i' is out of scope",
                        code = "semantic.out_of_scope_identifier",
                        line = 1,
                        column = column,
                        endLine = 1,
                        endColumn = column + #"Main",
                        length = #"Main",
                    },
                },
            })

            expect(info):toNotBeNil()
            expect(info.detail):toBe("Identifier 'i' is out of scope")
            expect(info.documentation):toContain("semantic.out_of_scope_identifier")
        end)

        it("uses generic fallback detail for unknown diagnostic codes", function()
            local source = "class Main { }"
            local column = source:find("Main", 1, true)
            local info = IntelliSense.getHoverInfo(source, column + 1, {
                diagnostics = {
                    {
                        severity = "warning",
                        code = "semantic.some_future_code",
                        line = 1,
                        column = column,
                        endLine = 1,
                        endColumn = column + #"Main",
                        length = #"Main",
                    },
                },
            })

            expect(info):toNotBeNil()
            expect(info.kind):toBe("warning")
            expect(info.detail):toBe("Issue detected.")
            expect(info.documentation):toContain("semantic.some_future_code")
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

        it("prefers declaration method name over parameter type at method boundary", function()
            local source = "class Main { public void OnPlayerJoined(Player p) { } }"
            local boundaryPos = source:find("OnPlayerJoined", 1, true) + #"OnPlayerJoined"
            local info = IntelliSense.getHoverInfo(source, boundaryPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("OnPlayerJoined")
            expect(info.kind):toBe("method")
            expect(info.detail):toContain("OnPlayerJoined(Player p)")
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

        it("does not show hover info when cursor is on a closing brace line", function()
            local source = "class Main {\n    public void GameEntry() { }\n    }"
            local hoverPos = source:find("    }", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toBeNil()
        end)

        it("does not show hover info after trailing newline following class closing brace", function()
            local source = "using LUSharpAPI.Runtime.STL;\nclass Main {\n}\n"
            local hoverPos = #source + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toBeNil()
        end)

        it("does not show hover info when cursor is on an empty line", function()
            local source = "class Main {\n\n    public void GameEntry() { }\n}"
            local hoverPos = source:find("\n\n", 1, true) + 1
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

        it("uses inferred template local type for member suggestions from chained generic call", function()
            local source = "var part = Factories.Scene.Create<Part>(); part."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "Anchored")):toBe(true)
        end)

        it("keeps chained generic local inference generic across non-Part types", function()
            local source = "var players = Services.Registry.Create<Players>(); players."
            local completions = IntelliSense.getCompletions(source, #source + 1, nil)
            expect(hasLabel(completions, "PlayerAdded")):toBe(true)
        end)

        it("shows hover docs for local inferred from GetService generic", function()
            local source = "var players = game.GetService<Players>(); players"
            local hoverPos = #source - 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("variable")
            expect(info.label):toBe("players")
            expect(info.detail):toContain("Players")
            expect(info.detail):toContain("LUSharpAPI.Runtime.STL.Services.Players")
            expect(info.documentation):toContain("Roblox")
            expect(info.documentation):toContain("create.roblox.com/docs/reference/engine/classes/Players")
        end)

        it("shows inferred service hover on declaration variable name", function()
            local source = "var players = game.GetService(\"Players\");"
            local hoverPos = source:find("players", 1, true) + 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("variable")
            expect(info.label):toBe("players")
            expect(info.detail):toContain("Players")
            expect(info.detail):toContain("LUSharpAPI.Runtime.STL.Services.Players")
            expect(info.documentation):toContain("create.roblox.com/docs/reference/engine/classes/Players")
        end)

        it("shows hover docs for local inferred from GetService string call", function()
            local source = "var players = game.GetService(\"Players\"); players"
            local hoverPos = #source - 2
            local info = IntelliSense.getHoverInfo(source, hoverPos, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("variable")
            expect(info.label):toBe("players")
            expect(info.detail):toContain("Players")
            expect(info.detail):toContain("LUSharpAPI.Runtime.STL.Services.Players")
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
            expect(info.detail):toContain("LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance.Part")
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
            expect(info.detail):toContain("String name")
            expect(info.detail):toContain("-> Instance")
        end)

        it("provides built-in Roblox type documentation on hover", function()
            local source = "Part"
            local info = IntelliSense.getHoverInfo(source, 2, nil)
            expect(info):toNotBeNil()
            expect(info.kind):toBe("type")
            expect(info.documentation):toContain("Roblox")
        end)

        it("returns type hover when hovering declaration type token", function()
            local source = "Part p;"
            local hoverPos = source:find("Part", 1, true) + 1
            local info = IntelliSense.getHoverInfo(source, hoverPos, { searchNearby = true, nearbyRadius = 20 })
            expect(info):toNotBeNil()
            expect(info.label):toBe("Part")
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
