local Lexer = require("../src/Lexer")
local Parser = require("../src/Parser")
local Lowerer = require("../src/Lowerer")

local function lower(source)
    local tokens = Lexer.tokenize(source)
    local ast = Parser.parse(tokens)
    return Lowerer.lower(ast)
end

local function run(describe, it, expect)
    describe("Lowerer", function()
        it("lowers a simple class to a table", function()
            local ir = lower("class Foo { }")
            expect(ir.modules[1].classes[1].name):toBe("Foo")
        end)

        it("lowers static field", function()
            local ir = lower("class Foo { public static int X = 42; }")
            local field = ir.modules[1].classes[1].staticFields[1]
            expect(field.name):toBe("X")
        end)

        it("lowers method to function", function()
            local ir = lower([[
class Foo {
    public void DoStuff() { print("hello"); }
}]])
            local method = ir.modules[1].classes[1].methods[1]
            expect(method.name):toBe("DoStuff")
            expect(method.isStatic):toBe(false)
        end)

        it("maps Console.WriteLine to print", function()
            local ir = lower([[
class Foo {
    public void M() { Console.WriteLine("test"); }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("expr_statement")
            expect(stmt.expression.type):toBe("call")
            expect(stmt.expression.callee.name):toBe("print")
        end)

        it("lowers string interpolation to template_string", function()
            local ir = lower([[
class Foo {
    public void M() { var s = $"Hello {name}"; }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("local_decl")
            expect(stmt.value.type):toBe("template_string")
        end)

        it("lowers foreach to for_in", function()
            local ir = lower([[
class Foo {
    public void M() { foreach (var x in list) { } }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("for_in")
        end)

        it("preserves for-loop initializer in fallback lowering", function()
            local ir = lower([[
class Foo {
    public void M() { for (var i = 0; i < 10; i += 2) { } }
}]])
            local body = ir.modules[1].classes[1].methods[1].body
            expect(body[1].type):toBe("local_decl")
            expect(body[2].type):toBe("while_stmt")
        end)

        it("uses assignment statement when fallback init is assignment", function()
            local ir = lower([[
class Foo {
    public void M() { for (i = 0; i < 10; i += 2) { } }
}]])
            local body = ir.modules[1].classes[1].methods[1].body
            expect(body[1].type):toBe("assignment")
            expect(body[2].type):toBe("while_stmt")
        end)

        it("does not lower mismatched for variables as numeric loop", function()
            local ir = lower([[
class Foo {
    public void M() { for (var i = 0; j < 10; i++) { } }
}]])
            local body = ir.modules[1].classes[1].methods[1].body
            expect(body[1].type):toBe("local_decl")
            expect(body[2].type):toBe("while_stmt")
        end)

        it("lowers increment statement to assignment", function()
            local ir = lower([[
class Foo {
    public void M() { i++; }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("assignment")
            expect(stmt.value.type):toBe("binary_op")
            expect(stmt.value.op):toBe("+")
        end)

        it("lowers null-conditional access safely", function()
            local ir = lower([[
class Foo {
    public void M() { var x = player?.Name; }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("local_decl")
            expect(stmt.value.type):toBe("null_conditional")
            expect(stmt.value.field):toBe("Name")
        end)

        it("lowers is operator to boolean comparison", function()
            local ir = lower([[
class Foo {
    public void M() { var ok = x is int; }
}]])
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.value.type):toBe("binary_op")
            expect(stmt.value.op):toBe("==")
            expect(stmt.value.right.value):toBe('"number"')
        end)

        it("drops switch-case break and preserves default body", function()
            local ir = lower([[
class Foo {
    public void M() { switch (x) { default: Foo(); Bar(); break; } }
}]])
            local body = ir.modules[1].classes[1].methods[1].body
            expect(body[1].type):toBe("local_decl")
            expect(body[2].type):toBe("expr_statement")
            expect(body[3].type):toBe("expr_statement")
        end)

        it("determines ScriptType from base class", function()
            local ir = lower("class Foo : RobloxScript { public void GameEntry() { } }")
            expect(ir.modules[1].scriptType):toBe("Script")
        end)

        it("determines ModuleScript for shared classes", function()
            local ir = lower("class Foo : ModuleScript { }")
            expect(ir.modules[1].scriptType):toBe("ModuleScript")
        end)

        it("lowers primary constructor parameters into ctor and fields", function()
            local ir = lower("class Foo(int health) { }")
            local cls = ir.modules[1].classes[1]
            expect(cls.constructor):toNotBeNil()
            expect(cls.constructor.params[1]):toBe("health")
            expect(cls.instanceFields[1].name):toBe("health")
            expect(cls.instanceFields[1].value.type):toBe("identifier")
            expect(cls.instanceFields[1].value.name):toBe("health")
        end)

        it("lowers collection expressions to array literals", function()
            local ir = lower("class Foo { void M() { var xs = [1, 2, 3]; } }")
            local stmt = ir.modules[1].classes[1].methods[1].body[1]
            expect(stmt.type):toBe("local_decl")
            expect(stmt.value.type):toBe("array_literal")
            expect(#(stmt.value.elements or {})):toBe(3)
        end)

        it("lowers lambda default parameters to nil guards", function()
            local ir = lower("class Foo { void M() { var f = (int x = 5) => x; } }")
            local fn = ir.modules[1].classes[1].methods[1].body[1].value
            expect(fn.type):toBe("function_expr")
            expect(fn.params[1]):toBe("x")
            expect(fn.body[1].type):toBe("if_stmt")
        end)
    end)
end

return run
