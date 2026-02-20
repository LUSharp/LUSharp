local Lexer = require("../src/Lexer")
local Parser = require("../src/Parser")

local function parse(source)
    local tokens = Lexer.tokenize(source)
    return Parser.parse(tokens)
end

local function run(describe, it, expect)
    describe("Parser: Declarations", function()
        it("parses an empty class", function()
            local ast = parse("public class Foo { }")
            expect(#ast.classes):toBe(1)
            expect(ast.classes[1].name):toBe("Foo")
            expect(ast.classes[1].accessModifier):toBe("public")
        end)

        it("parses class with base class", function()
            local ast = parse("class Foo : Bar { }")
            expect(ast.classes[1].baseClass):toBe("Bar")
        end)

        it("parses a method", function()
            local ast = parse([[
class Foo {
    public void DoStuff(int x, string y) { }
}]])
            local method = ast.classes[1].methods[1]
            expect(method.name):toBe("DoStuff")
            expect(method.returnType):toBe("void")
            expect(#method.parameters):toBe(2)
            expect(method.parameters[1].name):toBe("x")
            expect(method.parameters[1].type):toBe("int")
        end)

        it("parses a static method", function()
            local ast = parse([[
class Foo {
    public static int Add(int a, int b) { }
}]])
            local method = ast.classes[1].methods[1]
            expect(method.isStatic):toBe(true)
            expect(method.returnType):toBe("int")
        end)

        it("parses a field with initializer", function()
            local ast = parse([[
class Foo {
    private int health = 100;
}]])
            local field = ast.classes[1].fields[1]
            expect(field.name):toBe("health")
            expect(field.fieldType):toBe("int")
            expect(field.accessModifier):toBe("private")
        end)

        it("parses an auto-property", function()
            local ast = parse([[
class Foo {
    public string Name { get; set; } = "Default";
}]])
            local prop = ast.classes[1].properties[1]
            expect(prop.name):toBe("Name")
            expect(prop.propType):toBe("string")
            expect(prop.hasGet):toBe(true)
            expect(prop.hasSet):toBe(true)
        end)

        it("parses a constructor", function()
            local ast = parse([[
class Foo {
    public Foo(int x) { }
}]])
            expect(ast.classes[1].constructor):toNotBeNil()
            expect(#ast.classes[1].constructor.parameters):toBe(1)
        end)

        it("parses using directives", function()
            local ast = parse([[
using System;
using System.Collections.Generic;
class Foo { }]])
            expect(#ast.usings):toBe(2)
            expect(ast.usings[1]):toBe("System")
        end)

        it("parses generic type in field", function()
            local ast = parse([[
class Foo {
    private List<int> items = new List<int>();
}]])
            local field = ast.classes[1].fields[1]
            expect(field.fieldType):toBe("List<int>")
        end)

        it("parses enum declaration", function()
            local ast = parse([[
enum Direction { North, South, East, West }]])
            expect(ast.enums[1].name):toBe("Direction")
            expect(#ast.enums[1].values):toBe(4)
        end)

        it("parses interface declaration", function()
            local ast = parse([[
interface IMovable {
    void Move(float speed);
}]])
            expect(ast.interfaces[1].name):toBe("IMovable")
            expect(#ast.interfaces[1].methods):toBe(1)
        end)

        it("parses override method", function()
            local ast = parse([[
class Foo : Bar {
    public override void GameEntry() { }
}]])
            local method = ast.classes[1].methods[1]
            expect(method.isOverride):toBe(true)
        end)
    end)

    describe("Parser: Statements", function()
        it("parses variable declaration with var", function()
            local ast = parse("class C { void M() { var x = 42; } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("local_var")
            expect(stmt.name):toBe("x")
        end)

        it("parses typed variable declaration", function()
            local ast = parse("class C { void M() { int x = 42; } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("local_var")
            expect(stmt.varType):toBe("int")
        end)

        it("parses if/else", function()
            local ast = parse("class C { void M() { if (x > 0) { } else { } } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("if")
            expect(stmt.elseBody):toNotBeNil()
        end)

        it("parses for loop", function()
            local ast = parse("class C { void M() { for (int i = 0; i < 10; i++) { } } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("for")
        end)

        it("parses foreach loop", function()
            local ast = parse("class C { void M() { foreach (var item in list) { } } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("foreach")
            expect(stmt.variable):toBe("item")
        end)

        it("parses while loop", function()
            local ast = parse("class C { void M() { while (true) { } } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("while")
        end)

        it("parses return", function()
            local ast = parse("class C { int M() { return 42; } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("return")
        end)

        it("parses try/catch", function()
            local ast = parse("class C { void M() { try { } catch (Exception e) { } } }")
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("try_catch")
        end)

        it("parses switch", function()
            local ast = parse([[class C { void M() {
                switch (x) { case 1: break; default: break; }
            } }]])
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("switch")
        end)

        it("parses break and continue", function()
            local ast = parse("class C { void M() { break; continue; } }")
            expect(ast.classes[1].methods[1].body[1].type):toBe("break")
            expect(ast.classes[1].methods[1].body[2].type):toBe("continue")
        end)

        it("parses throw", function()
            local ast = parse('class C { void M() { throw new Exception("err"); } }')
            local stmt = ast.classes[1].methods[1].body[1]
            expect(stmt.type):toBe("throw")
        end)
    end)
end

return run
