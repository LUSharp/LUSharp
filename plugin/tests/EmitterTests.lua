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

        it("calls other instance methods via self", function()
            local out = emit([[
class Foo {
    public void A() { B(); }
    public void B() { }
}]])
            expect(out):toContain("function Foo:A()\n    self:B()\nend")
        end)

        it("calls other static methods via class table", function()
            local out = emit([[
class Foo {
    public static void A() { B(); }
    public static void B() { }
}]])
            expect(out):toContain("function Foo.A()\n    Foo.B()\nend")
        end)

        it("emits target-typed new() from declared type", function()
            local out = emit([[
using System.Collections.Generic;
class Foo {
    public void M() {
        List<int> xs = new();
    }
}]])
            expect(out):toContain("local xs = List.new()")
        end)

        it("emits target-typed collection initializer items", function()
            local out = emit([[
using System.Collections.Generic;
class Foo {
    public void M() {
        List<int> xd = new(){3,3,3,3,3};
    }
}]])
            expect(out):toContain("local xd = List.new(3, 3, 3, 3, 3)")
        end)

        it("emits target-typed new for nullable declared types", function()
            local out = emit([[
using System.Collections.Generic;
class Foo {
    public void M() {
        List<int>? xs = new(){1};
    }
}]])
            expect(out):toContain("local xs = List.new(1)")
        end)

        it("emits primary constructor parameters in constructor helper", function()
            local out = emit("class Foo(int health) { }")
            expect(out):toContain("function Foo.new(health)")
            expect(out):toContain("self.health = health")
        end)

        it("emits collection expressions as table literals", function()
            local out = emit("class Foo { public void M() { var xs = [1, 2, 3]; } }")
            expect(out):toContain("local xs = {1, 2, 3}")
        end)

        it("emits lambda default parameter nil guards", function()
            local out = emit("class Foo { public void M() { var f = (int x = 5) => x; } }")
            expect(out):toContain("if (x == nil) then")
            expect(out):toContain("x = 5")
        end)

        it("lowers event += and -= using Connect/Disconnect cache", function()
            local out = emit([[
class Foo {
    public static void GameEntry() {
        game.GetService("Players").PlayerAdded += OnPlayerJoined;
        game.GetService("Players").PlayerAdded -= OnPlayerJoined;
    }

    public static void OnPlayerJoined(Player p) { }
}
]])
            expect(out):toContain(":Connect")
            expect(out):toContain(":Disconnect")
        end)
    end)
end

return run
