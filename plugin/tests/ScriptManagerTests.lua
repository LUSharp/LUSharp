local ScriptManager = require("../src/ScriptManager")

local function run(describe, it, expect)
    describe("ScriptManager", function()
        it("builds namespaced template for known context", function()
            local source = ScriptManager.buildSourceTemplate("Foo", "Server", nil)
            expect(source):toContain("namespace Game.Server {")
            expect(source):toContain("public class Foo {")
        end)

        it("falls back to root namespace for Other context", function()
            local source = ScriptManager.buildSourceTemplate("Bar", "Other", nil)
            expect(source):toContain("namespace Game {")
            expect(source):toContain("public class Bar {")
        end)

        it("uses explicit namespace override with sanitization", function()
            local source = ScriptManager.buildSourceTemplate("Baz", "Client", "My Game.Core-1")
            expect(source):toContain("namespace My_Game.Core_1 {")
            expect(source):toContain("public class Baz {")
        end)

        it("keeps class-only template when no namespace can be resolved", function()
            local source = ScriptManager.buildSourceTemplate("Qux", nil, nil)
            expect(string.find(source, "namespace ", 1, true) == nil):toBe(true)
            expect(source):toContain("public class Qux {")
        end)
    end)
end

return run
