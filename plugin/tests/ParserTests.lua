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
end

return run
