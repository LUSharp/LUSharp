local IntelliSense = require("../src/IntelliSense")
local SystemCatalog = require("../src/SystemCatalog")
local TypeDatabase = require("../src/TypeDatabase")

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

local function firstIndexByLabel(completions, label)
    for index, item in ipairs(completions or {}) do
        if item.label == label then
            return index
        end
    end

    return nil
end

local function contextMode(source)
    local context = IntelliSense._analyzeContextForTests(source, #source + 1)
    if not context then
        return nil
    end
    return context.mode
end

local function cursorAfter(source, literal)
    local startIndex = string.find(source, literal, 1, true)
    if not startIndex then
        return #source + 1
    end
    return startIndex + #literal
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

        it("detects explicit context modes", function()
            expect(contextMode("Globals g; g.")):toBe("memberAccess")
            expect(contextMode("new Vec")):toBe("newExpression")
            expect(contextMode("class Foo : Bar")):toBe("typePosition")
            expect(contextMode("using Sys")):toBe("usingNamespace")
            expect(contextMode("print(\"x\", ")):toBe("invocationArgs")
            expect(contextMode("ret")):toBe("statementStart")
        end)

        it("includes method parameters in nearest visible scope", function()
            local source = [[
class Foo {
    public void M(Globals ctx) {
        ctx.
    }
}
]]
            local completions = IntelliSense.getCompletions(source, cursorAfter(source, "ctx."))
            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("includes method parameters for methods with generic return types", function()
            local source = [[
class Foo {
    public List<int> M(Globals ctx) {
        ctx.
    }
}
]]
            local completions = IntelliSense.getCompletions(source, cursorAfter(source, "ctx."))
            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("prefers nearest shadowed local type for member access", function()
            local source = [[
class Foo {
    public void M() {
        Workspace value;
        {
            Globals value;
            value.
        }
    }
}
]]
            local completions = IntelliSense.getCompletions(source, cursorAfter(source, "value."))
            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("removes block locals after scope exit", function()
            local source = [[
class Foo {
    public void M() {
        {
            Globals inner;
        }
        inner.
    }
}
]]
            local completions = IntelliSense.getCompletions(source, cursorAfter(source, "inner."))
            expect(#completions):toBe(0)
        end)

        it("ranks expression-context globals ahead of declaration keywords", function()
            IntelliSense.resetSymbolTable()

            local source = "pri"
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local printIndex = firstIndexByLabel(completions, "print")
            local privateIndex = firstIndexByLabel(completions, "private")

            expect(printIndex ~= nil):toBe(true)
            expect(privateIndex ~= nil):toBe(true)
            expect(printIndex < privateIndex):toBe(true)
        end)

        it("suppresses keyword suggestions during member access", function()
            IntelliSense.resetSymbolTable()

            local source = [[
class Foo {
    public void M() {
        Globals g;
        g.pr
    }
}
]]
            local completions = IntelliSense.getCompletions(source, cursorAfter(source, "g.pr"))
            local printEntry = firstByLabel(completions, "print")
            local privateEntry = firstByLabel(completions, "private")

            expect(printEntry):toNotBeNil()
            expect(privateEntry):toBeNil()
            expect(completions[1].kind == "method" or completions[1].kind == "property" or completions[1].kind == "field"):toBe(true)
        end)

        it("prioritizes type and namespace providers in type positions", function()
            IntelliSense.resetSymbolTable()

            local source = "class Foo : Co"
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local consoleIndex = firstIndexByLabel(completions, "Console")
            local continueIndex = firstIndexByLabel(completions, "continue")

            expect(consoleIndex ~= nil):toBe(true)
            expect(continueIndex ~= nil):toBe(true)
            expect(consoleIndex < continueIndex):toBe(true)
        end)

        it("gates new-expression completions to constructable types", function()
            IntelliSense.resetSymbolTable()

            local source = "new "
            local completions = IntelliSense.getCompletions(source, #source + 1)

            expect(#completions > 0):toBe(true)
            expect(hasLabel(completions, "private")):toBe(false)

            for _, item in ipairs(completions) do
                expect(item.kind):toBe("type")
            end
        end)

        it("uses deterministic tie-break ordering", function()
            IntelliSense.resetSymbolTable()

            local source = "using System."
            local first = IntelliSense.getCompletions(source, #source + 1)
            local second = IntelliSense.getCompletions(source, #source + 1)

            expect(#first):toBe(#second)

            local limit = math.min(#first, 10)
            for index = 1, limit do
                expect(first[index].label):toBe(second[index].label)
                expect(first[index].kind):toBe(second[index].kind)
            end
        end)

        it("keeps later-provider exact matches through ranking under large pools", function()
            IntelliSense.resetSymbolTable()

            local aliasLabel = "Task6LateProviderWinner"
            local fullName = "Regression.Task6LateProviderWinner"

            local previousAlias = TypeDatabase.aliases[aliasLabel]
            local previousType = TypeDatabase.types[fullName]
            local originalGetTypeCompletions = SystemCatalog.getTypeCompletions

            local ok, failure = pcall(function()
                TypeDatabase.aliases[aliasLabel] = fullName
                TypeDatabase.types[fullName] = {
                    fullName = fullName,
                    kind = "class",
                    members = {},
                }

                SystemCatalog.getTypeCompletions = function(prefix)
                    local completions = {}

                    for i = 1, 150 do
                        local label = string.format("%sFiller%03d", aliasLabel, i)
                        completions[i] = {
                            label = label,
                            kind = "type",
                            detail = "class",
                            documentation = "System." .. label,
                            source = "system",
                            insertMode = "replacePrefix",
                            insertText = label,
                        }
                    end

                    return completions
                end

                local source = "new " .. aliasLabel
                local completions = IntelliSense.getCompletions(source, #source + 1)

                local winnerIndex = firstIndexByLabel(completions, aliasLabel)
                local fillerIndex = firstIndexByLabel(completions, aliasLabel .. "Filler001")

                expect(#completions):toBe(150)
                expect(winnerIndex ~= nil):toBe(true)
                expect(fillerIndex ~= nil):toBe(true)
                expect(winnerIndex < fillerIndex):toBe(true)
            end)

            SystemCatalog.getTypeCompletions = originalGetTypeCompletions

            if previousAlias == nil then
                TypeDatabase.aliases[aliasLabel] = nil
            else
                TypeDatabase.aliases[aliasLabel] = previousAlias
            end

            if previousType == nil then
                TypeDatabase.types[fullName] = nil
            else
                TypeDatabase.types[fullName] = previousType
            end

            if not ok then
                error(failure)
            end
        end)

        it("includes template suggestions for for/foreach/if/try", function()
            IntelliSense.resetSymbolTable()

            local source = ""
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local function templateByLabel(label)
                for _, item in ipairs(completions) do
                    if item.label == label and item.kind == "template" then
                        return item
                    end
                end
                return nil
            end

            local forTemplate = templateByLabel("for")
            local foreachTemplate = templateByLabel("foreach")
            local ifTemplate = templateByLabel("if")
            local tryTemplate = templateByLabel("try")

            expect(forTemplate):toNotBeNil()
            expect(foreachTemplate):toNotBeNil()
            expect(ifTemplate):toNotBeNil()
            expect(tryTemplate):toNotBeNil()

            expect(forTemplate.source):toBe("template")
            expect(forTemplate.insertMode):toBe("snippetExpand")
            expect(forTemplate.insertText):toNotBeNil()
        end)

        it("prefers template entries over duplicate statement-start keywords", function()
            IntelliSense.resetSymbolTable()

            local source = ""
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local function entriesByLabel(label)
                local entries = {}

                for _, item in ipairs(completions) do
                    if item.label == label then
                        table.insert(entries, item)
                    end
                end

                return entries
            end

            for _, label in ipairs({ "for", "foreach", "if", "try" }) do
                local entries = entriesByLabel(label)
                expect(#entries):toBe(1)
                expect(entries[1].kind):toBe("template")
            end
        end)

        it("recognizes System namespace context for member completions", function()
            IntelliSense.resetSymbolTable()

            local source = "System."
            local completions = IntelliSense.getCompletions(source, #source + 1)

            local consoleEntry = firstByLabel(completions, "Console")
            local mathEntry = firstByLabel(completions, "Math")
            local collectionsEntry = firstByLabel(completions, "Collections")

            expect(consoleEntry):toNotBeNil()
            expect(mathEntry):toNotBeNil()
            expect(collectionsEntry):toNotBeNil()
            expect(consoleEntry.source):toBe("system")
            expect(mathEntry.source):toBe("system")
        end)

        it("supports using System namespace progression", function()
            IntelliSense.resetSymbolTable()

            local source = "using System."
            local completions = IntelliSense.getCompletions(source, #source + 1)

            expect(hasLabel(completions, "System.Collections")):toBe(true)
            expect(hasLabel(completions, "System.Collections.Generic")):toBe(true)
            expect(hasLabel(completions, "System.Linq")):toBe(true)
            expect(hasLabel(completions, "System.Text")):toBe(true)
        end)

        it("includes common System types in type-position completions", function()
            IntelliSense.resetSymbolTable()

            local source = "List<"
            local completions = IntelliSense.getCompletions(source, #source + 1)

            expect(hasLabel(completions, "List")):toBe(true)
            expect(hasLabel(completions, "Dictionary")):toBe(true)
        end)

        it("returns System type member suggestions with system source", function()
            IntelliSense.resetSymbolTable()

            local consoleCompletions = IntelliSense.getCompletions("Console.", #"Console." + 1)
            local mathCompletions = IntelliSense.getCompletions("Math.", #"Math." + 1)

            local writeLineEntry = firstByLabel(consoleCompletions, "WriteLine")
            local sqrtEntry = firstByLabel(mathCompletions, "Sqrt")

            expect(writeLineEntry):toNotBeNil()
            expect(sqrtEntry):toNotBeNil()
            expect(writeLineEntry.source):toBe("system")
            expect(sqrtEntry.source):toBe("system")
        end)

        it("does not resolve unknown System paths to catalog types", function()
            IntelliSense.resetSymbolTable()

            local source = "System.Foo.List."
            local completions = IntelliSense.getCompletions(source, #source + 1)

            expect(hasLabel(completions, "Add")):toBe(false)
            expect(hasLabel(completions, "Count")):toBe(false)
            expect(#completions):toBe(0)
        end)

        it("does not leak system catalog members when System/Console are shadowed", function()
            IntelliSense.resetSymbolTable()

            local sourceShadowSystem = [[
class Foo {
    public void M() {
        Globals System;
        System.
    }
}
]]
            local systemCompletions = IntelliSense.getCompletions(sourceShadowSystem, cursorAfter(sourceShadowSystem, "System."))
            expect(hasLabel(systemCompletions, "game")):toBe(true)
            expect(hasLabel(systemCompletions, "Console")):toBe(false)

            local sourceShadowConsole = [[
class Foo {
    public void M() {
        Globals Console;
        Console.
    }
}
]]
            local consoleCompletions = IntelliSense.getCompletions(sourceShadowConsole, cursorAfter(sourceShadowConsole, "Console."))
            expect(hasLabel(consoleCompletions, "game")):toBe(true)
            expect(hasLabel(consoleCompletions, "WriteLine")):toBe(false)
        end)

        it("preserves Roblox/global completion behavior", function()
            IntelliSense.resetSymbolTable()

            local source = "ga"
            local completions = IntelliSense.getCompletions(source, #source + 1)

            expect(hasLabel(completions, "game")):toBe(true)
        end)

        it("emits insertText, insertMode, and source completion metadata", function()
            IntelliSense.resetSymbolTable()

            local keywordCompletions = IntelliSense.getCompletions("pri", #"pri" + 1)
            local printEntry = firstByLabel(keywordCompletions, "print")
            local privateEntry = firstByLabel(keywordCompletions, "private")

            local systemCompletions = IntelliSense.getCompletions("Console.", #"Console." + 1)
            local writeLineEntry = firstByLabel(systemCompletions, "WriteLine")

            expect(printEntry):toNotBeNil()
            expect(printEntry.insertText):toBe("print")
            expect(printEntry.insertMode):toBe("replacePrefix")
            expect(printEntry.source):toBe("global")

            expect(privateEntry):toNotBeNil()
            expect(privateEntry.insertText):toBe("private")
            expect(privateEntry.insertMode):toBe("replacePrefix")
            expect(privateEntry.source):toBe("keyword")

            expect(writeLineEntry):toNotBeNil()
            expect(writeLineEntry.insertText):toBe("WriteLine")
            expect(writeLineEntry.insertMode):toBe("replacePrefix")
            expect(writeLineEntry.source):toBe("system")
        end)
    end)
end

return run
