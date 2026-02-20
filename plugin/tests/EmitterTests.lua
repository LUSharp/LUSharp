local Lexer = require("../src/Lexer")
local Parser = require("../src/Parser")
local Lowerer = require("../src/Lowerer")
local Emitter = require("../src/Emitter")

local function emit(source)
    local tokens = Lexer.tokenize(source)
    local ast = Parser.parse(tokens)
    local ir = Lowerer.lower(ast)
    return Emitter.emit(ir.modules[1])
end

local function run(describe, it, expect)
    describe("Emitter", function()
        it("emits simple class table", function()
            local out = emit("class Foo { }")
            expect(out):toContain("local Foo = {}")
        end)

        it("emits static fields", function()
            local out = emit("class Foo { public static int X = 42; }")
            expect(out):toContain("Foo.X = 42")
        end)

        it("emits instance method with colon syntax", function()
            local out = emit([[
class Foo {
    public void DoStuff(int x) { }
}]])
            expect(out):toContain("function Foo:DoStuff(x)")
        end)

        it("emits static method with dot syntax", function()
            local out = emit([[
class Foo {
    public static void DoStuff(int x) { }
}]])
            expect(out):toContain("function Foo.DoStuff(x)")
        end)

        it("emits constructor helper when constructor is present", function()
            local out = emit([[
class Foo {
    private int health = 100;
    public Foo() { }
}]])
            expect(out):toContain("function Foo.new()")
            expect(out):toContain("self.health = 100")
            expect(out):toContain("return self")
        end)

        it("emits GameEntry call for Script types", function()
            local out = emit([[
class Main : RobloxScript {
    public void GameEntry() { }
}]])
            expect(out):toContain("Main:GameEntry()")
        end)

        it("emits return for ModuleScript types", function()
            local out = emit("class Foo { }")
            expect(out):toContain("return Foo")
        end)

        it("emits print for Console.WriteLine mapping", function()
            local out = emit([[
class Foo {
    public void M() { Console.WriteLine("test"); }
}]])
            expect(out):toContain("print(\"test\")")
        end)

        it("emits backtick string for interpolation", function()
            local out = emit([[
class Foo {
    public void M() { var s = $"Hello {name}"; }
}]])
            expect(out):toContain("`Hello {name}`")
        end)

        it("emits proper indentation for nested if", function()
            local out = emit([[
class Foo {
    public void M() {
        if (x > 0) {
            if (y > 0) {
                print("ok");
            }
        }
    }
}]])
            expect(out):toContain("if (x > 0) then")
            expect(out):toContain("        if (y > 0) then")
            expect(out):toContain("            print(\"ok\")")
        end)
    end)
end

return run
