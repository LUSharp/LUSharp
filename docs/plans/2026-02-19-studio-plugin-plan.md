# LUSharp Studio Plugin Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a self-contained Roblox Studio plugin with a Luau-native C# transpiler, code editor, and IntelliSense — no external server required.

**Architecture:** The plugin is a Luau codebase built into a `.rbxmx` model via Rojo. The transpiler pipeline (Lexer → Parser → Lowerer → Emitter) runs entirely in Luau. The editor is a dock widget with a hidden TextBox + RichText overlay. IntelliSense is powered by a static type database auto-generated from LUSharpAPI.

**Tech Stack:** Luau (Roblox), Rojo (build tool), C# (type database generator)

**Testing:** Transpiler components (Lexer, Parser, Lowerer, Emitter) are tested via standalone Luau scripts runnable with the `luau` CLI. UI components tested manually in Studio.

---

### Task 1: Plugin Project Scaffold

**Files:**
- Create: `plugin/default.project.json` (Rojo config for plugin build)
- Create: `plugin/src/init.lua` (plugin entry point stub)
- Create: `plugin/tests/run.lua` (test runner)

**Step 1: Create plugin directory structure**

```
plugin/
├── default.project.json
├── src/
│   ├── init.lua
│   ├── Lexer.lua
│   ├── Parser.lua
│   ├── Lowerer.lua
│   ├── Emitter.lua
│   ├── SyntaxHighlighter.lua
│   ├── IntelliSense.lua
│   ├── TypeDatabase.lua
│   ├── ScriptManager.lua
│   ├── Editor.lua
│   ├── ProjectView.lua
│   └── Settings.lua
└── tests/
    ├── run.lua
    ├── LexerTests.lua
    ├── ParserTests.lua
    ├── LowererTests.lua
    └── EmitterTests.lua
```

**Step 2: Create Rojo project config**

Create `plugin/default.project.json`:

```json
{
  "name": "LUSharp",
  "tree": {
    "$className": "Script",
    "$path": "src"
  }
}
```

**Step 3: Create plugin entry point stub**

Create `plugin/src/init.lua`:

```lua
-- LUSharp Studio Plugin
-- Entry point

local toolbar = plugin:CreateToolbar("LUSharp")
local buildButton = toolbar:CreateButton(
    "Build", "Compile C# to Luau", "rbxassetid://0", "Build"
)
local buildAllButton = toolbar:CreateButton(
    "BuildAll", "Compile all C# scripts", "rbxassetid://0", "Build All"
)
local newScriptButton = toolbar:CreateButton(
    "NewScript", "Create a new C# script", "rbxassetid://0", "New C# Script"
)

print("[LUSharp] Plugin loaded")
```

**Step 4: Create test runner**

Create `plugin/tests/run.lua`:

```lua
-- LUSharp Test Runner
-- Run with: luau plugin/tests/run.lua

local passed = 0
local failed = 0
local errors = {}

function describe(name, fn)
    print("\n" .. name)
    fn()
end

function it(name, fn)
    local ok, err = pcall(fn)
    if ok then
        passed += 1
        print("  PASS  " .. name)
    else
        failed += 1
        table.insert(errors, { name = name, err = err })
        print("  FAIL  " .. name)
        print("        " .. tostring(err))
    end
end

function expect(value)
    return {
        toBe = function(_, expected)
            if value ~= expected then
                error("Expected " .. tostring(expected) .. ", got " .. tostring(value), 2)
            end
        end,
        toEqual = function(_, expected)
            -- Deep table comparison
            local function deepEqual(a, b)
                if type(a) ~= type(b) then return false end
                if type(a) ~= "table" then return a == b end
                for k, v in pairs(a) do
                    if not deepEqual(v, b[k]) then return false end
                end
                for k, v in pairs(b) do
                    if a[k] == nil then return false end
                end
                return true
            end
            if not deepEqual(value, expected) then
                error("Tables not equal", 2)
            end
        end,
        toContain = function(_, substring)
            if type(value) ~= "string" or not string.find(value, substring, 1, true) then
                error("Expected string to contain '" .. tostring(substring) .. "'", 2)
            end
        end,
        toBeNil = function(_)
            if value ~= nil then
                error("Expected nil, got " .. tostring(value), 2)
            end
        end,
        toNotBeNil = function(_)
            if value == nil then
                error("Expected non-nil value", 2)
            end
        end,
        toHaveLength = function(_, len)
            if #value ~= len then
                error("Expected length " .. len .. ", got " .. #value, 2)
            end
        end,
    }
end

-- Load and run test files
local testFiles = {
    "LexerTests",
    "ParserTests",
    "LowererTests",
    "EmitterTests",
}

for _, name in ipairs(testFiles) do
    local ok, err = pcall(function()
        require("plugin/tests/" .. name)
    end)
    if not ok then
        print("ERROR loading " .. name .. ": " .. tostring(err))
    end
end

print("\n---")
print(passed .. " passed, " .. failed .. " failed")
if #errors > 0 then
    print("\nFailures:")
    for _, e in ipairs(errors) do
        print("  " .. e.name .. ": " .. e.err)
    end
end
```

**Step 5: Verify Rojo builds**

Run: `cd plugin && rojo build -o LUSharp.rbxmx`
Expected: Produces `LUSharp.rbxmx` file

**Step 6: Commit**

```bash
git add plugin/
git commit -m "feat: scaffold Studio plugin project structure"
```

---

### Task 2: C# Lexer

**Files:**
- Create: `plugin/src/Lexer.lua`
- Create: `plugin/tests/LexerTests.lua`

**Step 1: Write Lexer tests**

Create `plugin/tests/LexerTests.lua`:

```lua
local Lexer = require("plugin/src/Lexer")

describe("Lexer", function()
    it("tokenizes keywords", function()
        local tokens = Lexer.tokenize("class void public static")
        expect(#tokens):toBe(5) -- 4 keywords + EOF
        expect(tokens[1].type):toBe("keyword")
        expect(tokens[1].value):toBe("class")
        expect(tokens[2].type):toBe("keyword")
        expect(tokens[2].value):toBe("void")
    end)

    it("tokenizes identifiers", function()
        local tokens = Lexer.tokenize("myVar _foo Bar123")
        expect(tokens[1].type):toBe("identifier")
        expect(tokens[1].value):toBe("myVar")
    end)

    it("tokenizes numbers", function()
        local tokens = Lexer.tokenize("42 3.14 0xFF 1.5f")
        expect(tokens[1].type):toBe("number")
        expect(tokens[1].value):toBe("42")
        expect(tokens[2].type):toBe("number")
        expect(tokens[2].value):toBe("3.14")
    end)

    it("tokenizes strings", function()
        local tokens = Lexer.tokenize('"hello world"')
        expect(tokens[1].type):toBe("string")
        expect(tokens[1].value):toBe('"hello world"')
    end)

    it("tokenizes interpolated strings", function()
        local tokens = Lexer.tokenize('$"Hello {name}"')
        expect(tokens[1].type):toBe("interpolated_string")
    end)

    it("tokenizes operators", function()
        local tokens = Lexer.tokenize("+ - * / == != <= >= && || ??")
        expect(tokens[1].type):toBe("operator")
        expect(tokens[1].value):toBe("+")
        expect(tokens[5].value):toBe("==")
    end)

    it("tokenizes punctuation", function()
        local tokens = Lexer.tokenize("( ) { } [ ] ; , . :")
        expect(tokens[1].type):toBe("punctuation")
        expect(tokens[1].value):toBe("(")
    end)

    it("skips single-line comments", function()
        local tokens = Lexer.tokenize("x // this is a comment\ny")
        expect(tokens[1].type):toBe("identifier")
        expect(tokens[1].value):toBe("x")
        expect(tokens[2].type):toBe("identifier")
        expect(tokens[2].value):toBe("y")
    end)

    it("skips multi-line comments", function()
        local tokens = Lexer.tokenize("x /* comment\n spans lines */ y")
        expect(tokens[1].value):toBe("x")
        expect(tokens[2].value):toBe("y")
    end)

    it("preserves comments when requested", function()
        local tokens = Lexer.tokenize("x // comment\ny", { preserveComments = true })
        expect(tokens[2].type):toBe("comment")
        expect(tokens[2].value):toBe("// comment")
    end)

    it("tracks line and column", function()
        local tokens = Lexer.tokenize("foo\nbar")
        expect(tokens[1].line):toBe(1)
        expect(tokens[1].column):toBe(1)
        expect(tokens[2].line):toBe(2)
        expect(tokens[2].column):toBe(1)
    end)

    it("tokenizes a full class declaration", function()
        local tokens = Lexer.tokenize([[
public class ServerMain : RobloxScript {
    public void GameEntry() {
        print("Hello");
    }
}]])
        -- Should produce tokens without errors
        local lastToken = tokens[#tokens]
        expect(lastToken.type):toBe("eof")
    end)

    it("handles arrow operator", function()
        local tokens = Lexer.tokenize("x => x + 1")
        expect(tokens[2].type):toBe("operator")
        expect(tokens[2].value):toBe("=>")
    end)

    it("handles generic angle brackets", function()
        local tokens = Lexer.tokenize("List<int>")
        expect(tokens[1].value):toBe("List")
        expect(tokens[2].value):toBe("<")
        expect(tokens[3].value):toBe("int")
        expect(tokens[4].value):toBe(">")
    end)
end)
```

**Step 2: Run tests to verify they fail**

Run: `luau plugin/tests/run.lua`
Expected: All FAIL — Lexer module not found

**Step 3: Implement the Lexer**

Create `plugin/src/Lexer.lua`:

```lua
-- LUSharp Lexer: Tokenizes C# source into a stream of typed tokens

local Lexer = {}

local KEYWORDS = {
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
    "char", "class", "const", "continue", "decimal", "default", "delegate",
    "do", "double", "else", "enum", "event", "false", "finally", "float",
    "for", "foreach", "get", "if", "in", "int", "interface", "internal",
    "is", "long", "namespace", "new", "null", "object", "operator", "out",
    "override", "params", "private", "protected", "public", "readonly",
    "ref", "return", "sealed", "set", "short", "static", "string", "struct",
    "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
    "unsafe", "ushort", "using", "var", "virtual", "void", "while",
}

local KEYWORD_SET = {}
for _, kw in ipairs(KEYWORDS) do
    KEYWORD_SET[kw] = true
end

local function isAlpha(c)
    return (c >= "a" and c <= "z") or (c >= "A" and c <= "Z") or c == "_"
end

local function isDigit(c)
    return c >= "0" and c <= "9"
end

local function isAlphaNumeric(c)
    return isAlpha(c) or isDigit(c)
end

local function makeToken(type, value, line, column)
    return { type = type, value = value, line = line, column = column }
end

function Lexer.tokenize(source, options)
    options = options or {}
    local preserveComments = options.preserveComments or false

    local tokens = {}
    local pos = 1
    local line = 1
    local column = 1
    local len = #source

    local function peek(offset)
        offset = offset or 0
        local i = pos + offset
        if i > len then return "\0" end
        return string.sub(source, i, i)
    end

    local function advance()
        local c = peek()
        pos += 1
        if c == "\n" then
            line += 1
            column = 1
        else
            column += 1
        end
        return c
    end

    local function addToken(type, value, startLine, startCol)
        table.insert(tokens, makeToken(type, value, startLine, startCol))
    end

    local function readWhile(predicate)
        local start = pos
        while pos <= len and predicate(peek()) do
            advance()
        end
        return string.sub(source, start, pos - 1)
    end

    local function readString(quote)
        local startLine, startCol = line, column
        advance() -- skip opening quote
        local result = quote
        while pos <= len do
            local c = peek()
            if c == "\\" then
                result ..= advance() -- backslash
                if pos <= len then
                    result ..= advance() -- escaped char
                end
            elseif c == quote then
                result ..= advance()
                break
            elseif c == "\n" then
                break -- unterminated string
            else
                result ..= advance()
            end
        end
        return result, startLine, startCol
    end

    local function readInterpolatedString()
        local startLine, startCol = line, column
        advance() -- skip $
        advance() -- skip "
        local result = '$"'
        local depth = 0
        while pos <= len do
            local c = peek()
            if c == "\\" then
                result ..= advance()
                if pos <= len then result ..= advance() end
            elseif c == "{" then
                depth += 1
                result ..= advance()
            elseif c == "}" then
                depth -= 1
                result ..= advance()
            elseif c == '"' and depth == 0 then
                result ..= advance()
                break
            elseif c == "\n" then
                break
            else
                result ..= advance()
            end
        end
        return result, startLine, startCol
    end

    local TWO_CHAR_OPS = {
        ["=="] = true, ["!="] = true, ["<="] = true, [">="] = true,
        ["&&"] = true, ["||"] = true, ["??"] = true, ["?."] = true,
        ["=>"] = true, ["+="] = true, ["-="] = true, ["*="] = true,
        ["/="] = true, ["%="] = true, ["++"] = true, ["--"] = true,
    }

    local SINGLE_OPS = {
        ["+"] = true, ["-"] = true, ["*"] = true, ["/"] = true,
        ["%"] = true, ["="] = true, ["!"] = true, ["<"] = true,
        [">"] = true, ["&"] = true, ["|"] = true, ["^"] = true,
        ["~"] = true, ["?"] = true,
    }

    local PUNCTUATION = {
        ["("] = true, [")"] = true, ["{"] = true, ["}"] = true,
        ["["] = true, ["]"] = true, [";"] = true, [","] = true,
        ["."] = true, [":"] = true,
    }

    while pos <= len do
        local c = peek()

        -- Skip whitespace
        if c == " " or c == "\t" or c == "\r" or c == "\n" then
            advance()

        -- Comments
        elseif c == "/" and peek(1) == "/" then
            local startLine, startCol = line, column
            advance() -- /
            advance() -- /
            local comment = "//"
            while pos <= len and peek() ~= "\n" do
                comment ..= advance()
            end
            if preserveComments then
                addToken("comment", comment, startLine, startCol)
            end

        elseif c == "/" and peek(1) == "*" then
            local startLine, startCol = line, column
            advance() -- /
            advance() -- *
            local comment = "/*"
            while pos <= len do
                if peek() == "*" and peek(1) == "/" then
                    comment ..= advance() -- *
                    comment ..= advance() -- /
                    break
                else
                    comment ..= advance()
                end
            end
            if preserveComments then
                addToken("comment", comment, startLine, startCol)
            end

        -- Interpolated strings
        elseif c == "$" and peek(1) == '"' then
            local value, sl, sc = readInterpolatedString()
            addToken("interpolated_string", value, sl, sc)

        -- Verbatim strings
        elseif c == "@" and peek(1) == '"' then
            local startLine, startCol = line, column
            advance() -- @
            advance() -- "
            local result = '@"'
            while pos <= len do
                local ch = peek()
                if ch == '"' then
                    if peek(1) == '"' then
                        result ..= advance() .. advance() -- escaped ""
                    else
                        result ..= advance()
                        break
                    end
                else
                    result ..= advance()
                end
            end
            addToken("string", result, startLine, startCol)

        -- Regular strings
        elseif c == '"' then
            local value, sl, sc = readString('"')
            addToken("string", value, sl, sc)

        -- Char literals
        elseif c == "'" then
            local value, sl, sc = readString("'")
            addToken("string", value, sl, sc)

        -- Numbers
        elseif isDigit(c) then
            local startLine, startCol = line, column
            local num = ""

            -- Hex
            if c == "0" and (peek(1) == "x" or peek(1) == "X") then
                num = advance() .. advance() -- 0x
                num ..= readWhile(function(ch)
                    return isDigit(ch) or (ch >= "a" and ch <= "f") or (ch >= "A" and ch <= "F")
                end)
            else
                num = readWhile(isDigit)
                if peek() == "." and isDigit(peek(1)) then
                    num ..= advance() -- .
                    num ..= readWhile(isDigit)
                end
            end

            -- Suffix (f, d, m, L, UL, etc.)
            local suffix = peek()
            if suffix == "f" or suffix == "F" or suffix == "d" or suffix == "D"
                or suffix == "m" or suffix == "M" or suffix == "L" or suffix == "U" then
                num ..= advance()
                if peek() == "L" then num ..= advance() end
            end

            addToken("number", num, startLine, startCol)

        -- Identifiers and keywords
        elseif isAlpha(c) then
            local startLine, startCol = line, column
            local word = readWhile(isAlphaNumeric)
            if KEYWORD_SET[word] then
                addToken("keyword", word, startLine, startCol)
            else
                addToken("identifier", word, startLine, startCol)
            end

        -- Two-char operators (check before single-char)
        elseif SINGLE_OPS[c] then
            local startLine, startCol = line, column
            local twoChar = c .. peek(1)
            if TWO_CHAR_OPS[twoChar] then
                advance()
                advance()
                addToken("operator", twoChar, startLine, startCol)
            else
                addToken("operator", advance(), startLine, startCol)
            end

        -- Punctuation
        elseif PUNCTUATION[c] then
            local startLine, startCol = line, column
            addToken("punctuation", advance(), startLine, startCol)

        else
            -- Unknown character — skip
            advance()
        end
    end

    addToken("eof", "", line, column)
    return tokens
end

return Lexer
```

**Step 4: Run tests to verify they pass**

Run: `luau plugin/tests/run.lua`
Expected: All PASS

**Step 5: Commit**

```bash
git add plugin/src/Lexer.lua plugin/tests/LexerTests.lua
git commit -m "feat(plugin): implement C# lexer with tokenizer"
```

---

### Task 3: C# Parser — Declarations

**Files:**
- Create: `plugin/src/Parser.lua`
- Create: `plugin/tests/ParserTests.lua`

The parser is the largest component. We'll build it incrementally across Tasks 3-5:
- Task 3: Top-level declarations (class, method, field, property)
- Task 4: Statements (if, for, while, return, variable declarations)
- Task 5: Expressions (binary, unary, member access, method calls, lambdas)

**Step 1: Write Parser tests for declarations**

Create `plugin/tests/ParserTests.lua`:

```lua
local Lexer = require("plugin/src/Lexer")
local Parser = require("plugin/src/Parser")

local function parse(source)
    local tokens = Lexer.tokenize(source)
    return Parser.parse(tokens)
end

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
        expect(field.type):toBe("int")
        expect(field.accessModifier):toBe("private")
    end)

    it("parses an auto-property", function()
        local ast = parse([[
class Foo {
    public string Name { get; set; } = "Default";
}]])
        local prop = ast.classes[1].properties[1]
        expect(prop.name):toBe("Name")
        expect(prop.type):toBe("string")
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
        expect(field.type):toBe("List<int>")
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
```

**Step 2: Run tests to verify they fail**

Run: `luau plugin/tests/run.lua`
Expected: All FAIL

**Step 3: Implement Parser (declarations only)**

Create `plugin/src/Parser.lua`. This is the largest module — the full implementation is ~2000-3500 lines. Start with declarations; statements and expressions are added in Tasks 4-5.

```lua
-- LUSharp Parser: Recursive descent C# parser
-- Produces a C# AST from a token stream

local Parser = {}

-- AST node constructors
local function ClassNode(name, baseClass, accessModifier, isStatic, isAbstract)
    return {
        type = "class",
        name = name,
        baseClass = baseClass,
        accessModifier = accessModifier or "internal",
        isStatic = isStatic or false,
        isAbstract = isAbstract or false,
        fields = {},
        properties = {},
        methods = {},
        constructor = nil,
        events = {},
    }
end

local function MethodNode(name, returnType, parameters, body, modifiers)
    modifiers = modifiers or {}
    return {
        type = "method",
        name = name,
        returnType = returnType,
        parameters = parameters,
        body = body or {},
        accessModifier = modifiers.access or "private",
        isStatic = modifiers.isStatic or false,
        isOverride = modifiers.isOverride or false,
        isVirtual = modifiers.isVirtual or false,
        isAbstract = modifiers.isAbstract or false,
    }
end

local function FieldNode(name, fieldType, accessModifier, isStatic, isReadonly, initializer)
    return {
        type = "field",
        name = name,
        fieldType = fieldType,
        accessModifier = accessModifier or "private",
        isStatic = isStatic or false,
        isReadonly = isReadonly or false,
        initializer = initializer,
    }
end

local function PropertyNode(name, propType, accessModifier, hasGet, hasSet, initializer)
    return {
        type = "property",
        name = name,
        propType = propType,
        accessModifier = accessModifier or "public",
        hasGet = hasGet or false,
        hasSet = hasSet or false,
        initializer = initializer,
    }
end

local function ParameterNode(name, paramType, defaultValue)
    return { name = name, type = paramType, defaultValue = defaultValue }
end

local function EnumNode(name, values, accessModifier)
    return {
        type = "enum",
        name = name,
        values = values,
        accessModifier = accessModifier or "public",
    }
end

local function InterfaceNode(name, methods, accessModifier)
    return {
        type = "interface",
        name = name,
        methods = methods,
        accessModifier = accessModifier or "public",
    }
end

function Parser.parse(tokens)
    local pos = 1
    local diagnostics = {}

    local function peek(offset)
        offset = offset or 0
        local i = pos + offset
        if i > #tokens then return tokens[#tokens] end -- EOF
        return tokens[i]
    end

    local function current()
        return peek(0)
    end

    local function advance()
        local tok = current()
        pos += 1
        return tok
    end

    local function check(type, value)
        local tok = current()
        if value then
            return tok.type == type and tok.value == value
        end
        return tok.type == type
    end

    local function match(type, value)
        if check(type, value) then
            return advance()
        end
        return nil
    end

    local function expect(type, value)
        local tok = match(type, value)
        if not tok then
            local cur = current()
            local msg = "Expected " .. type
            if value then msg ..= " '" .. value .. "'" end
            msg ..= ", got " .. cur.type .. " '" .. cur.value .. "'"
            msg ..= " at line " .. cur.line .. ":" .. cur.column
            table.insert(diagnostics, {
                severity = "error",
                message = msg,
                line = cur.line,
                column = cur.column,
            })
            -- Error recovery: skip to next semicolon or brace
            return { type = type, value = value or "", line = cur.line, column = cur.column }
        end
        return tok
    end

    local function addDiagnostic(severity, message, tok)
        tok = tok or current()
        table.insert(diagnostics, {
            severity = severity,
            message = message,
            line = tok.line,
            column = tok.column,
        })
    end

    -- Forward declarations for mutual recursion
    local parseExpression
    local parseStatement
    local parseBlock

    -- Parse a type name (including generics: List<int>, Dictionary<string, int>)
    local function parseTypeName()
        local name = expect("identifier").value
        if check("punctuation", "<") then
            advance() -- <
            name ..= "<"
            name ..= parseTypeName()
            while match("punctuation", ",") do
                name ..= ", "
                name ..= parseTypeName()
            end
            expect("punctuation", ">")
            name ..= ">"
        end
        -- Nullable
        if check("operator", "?") then
            advance()
            name ..= "?"
        end
        -- Array
        if check("punctuation", "[") and peek(1).value == "]" then
            advance() -- [
            advance() -- ]
            name ..= "[]"
        end
        return name
    end

    -- Parse modifiers (public, private, static, override, etc.)
    local function parseModifiers()
        local mods = { access = nil, isStatic = false, isOverride = false,
            isVirtual = false, isAbstract = false, isReadonly = false,
            isConst = false, isSealed = false }
        while true do
            if check("keyword", "public") then mods.access = "public"; advance()
            elseif check("keyword", "private") then mods.access = "private"; advance()
            elseif check("keyword", "protected") then mods.access = "protected"; advance()
            elseif check("keyword", "internal") then mods.access = "internal"; advance()
            elseif check("keyword", "static") then mods.isStatic = true; advance()
            elseif check("keyword", "override") then mods.isOverride = true; advance()
            elseif check("keyword", "virtual") then mods.isVirtual = true; advance()
            elseif check("keyword", "abstract") then mods.isAbstract = true; advance()
            elseif check("keyword", "readonly") then mods.isReadonly = true; advance()
            elseif check("keyword", "const") then mods.isConst = true; advance()
            elseif check("keyword", "sealed") then mods.isSealed = true; advance()
            else break
            end
        end
        return mods
    end

    -- Parse parameter list
    local function parseParameterList()
        local params = {}
        expect("punctuation", "(")
        while not check("punctuation", ")") and not check("eof") do
            local paramType = parseTypeName()
            local paramName = expect("identifier").value
            local default = nil
            if match("operator", "=") then
                -- Simple default value — parse a single expression token
                default = advance().value
            end
            table.insert(params, ParameterNode(paramName, paramType, default))
            if not match("punctuation", ",") then break end
        end
        expect("punctuation", ")")
        return params
    end

    -- Parse a block { ... } — placeholder, filled in Task 4
    parseBlock = function()
        local stmts = {}
        expect("punctuation", "{")
        local depth = 1
        while depth > 0 and not check("eof") do
            if check("punctuation", "{") then depth += 1
            elseif check("punctuation", "}") then depth -= 1
                if depth == 0 then break end
            end
            -- For now, collect raw tokens — replaced with real parsing in Task 4
            table.insert(stmts, advance())
        end
        expect("punctuation", "}")
        return stmts
    end

    -- Parse a class member (method, field, property, constructor, event)
    local function parseClassMember(className)
        local mods = parseModifiers()

        -- Event: event Action<T> Name;
        if check("keyword", "event") then
            advance() -- event
            local eventType = parseTypeName()
            local eventName = expect("identifier").value
            match("punctuation", ";")
            return "event", { name = eventName, eventType = eventType }
        end

        -- Constructor: ClassName(...)
        if check("identifier") and current().value == className and peek(1).value == "(" then
            advance() -- class name
            local params = parseParameterList()
            local body = parseBlock()
            return "constructor", MethodNode(className, nil, params, body, mods)
        end

        -- Could be method or field/property
        -- Need to determine: type + name + ( means method, type + name + { means property, type + name + ; or = means field
        local typeName
        if check("keyword", "void") then
            typeName = "void"
            advance()
        else
            typeName = parseTypeName()
        end

        local memberName = expect("identifier").value

        -- Method: name(...)
        if check("punctuation", "(") then
            local params = parseParameterList()
            local body = {}
            if check("punctuation", "{") then
                body = parseBlock()
            else
                match("punctuation", ";") -- abstract/interface method
            end
            local method = MethodNode(memberName, typeName, params, body, mods)
            return "method", method
        end

        -- Property: name { get; set; }
        if check("punctuation", "{") then
            advance() -- {
            local hasGet, hasSet = false, false
            while not check("punctuation", "}") and not check("eof") do
                if match("keyword", "get") then hasGet = true; match("punctuation", ";")
                elseif match("keyword", "set") then hasSet = true; match("punctuation", ";")
                else advance() end
            end
            expect("punctuation", "}")
            local initializer = nil
            if match("operator", "=") then
                -- Parse initializer expression — simplified for now
                local initTokens = {}
                while not check("punctuation", ";") and not check("eof") do
                    table.insert(initTokens, advance())
                end
                if #initTokens > 0 then
                    initializer = initTokens
                end
            end
            match("punctuation", ";")
            return "property", PropertyNode(memberName, typeName, mods.access, hasGet, hasSet, initializer)
        end

        -- Field: name = value; or name;
        local initializer = nil
        if match("operator", "=") then
            local initTokens = {}
            while not check("punctuation", ";") and not check("eof") do
                table.insert(initTokens, advance())
            end
            if #initTokens > 0 then
                initializer = initTokens
            end
        end
        match("punctuation", ";")
        return "field", FieldNode(memberName, typeName, mods.access, mods.isStatic, mods.isReadonly, initializer)
    end

    -- Parse a class declaration
    local function parseClass(mods)
        expect("keyword", "class")
        local name = expect("identifier").value
        local baseClass = nil
        if match("punctuation", ":") then
            baseClass = parseTypeName()
        end
        local cls = ClassNode(name, baseClass, mods.access, mods.isStatic, mods.isAbstract)
        expect("punctuation", "{")
        while not check("punctuation", "}") and not check("eof") do
            local kind, node = parseClassMember(name)
            if kind == "method" then table.insert(cls.methods, node)
            elseif kind == "field" then table.insert(cls.fields, node)
            elseif kind == "property" then table.insert(cls.properties, node)
            elseif kind == "constructor" then cls.constructor = node
            elseif kind == "event" then table.insert(cls.events, node)
            end
        end
        expect("punctuation", "}")
        return cls
    end

    -- Parse an enum declaration
    local function parseEnum(mods)
        expect("keyword", "enum")
        local name = expect("identifier").value
        local values = {}
        expect("punctuation", "{")
        while not check("punctuation", "}") and not check("eof") do
            local valueName = expect("identifier").value
            local numValue = nil
            if match("operator", "=") then
                numValue = advance().value
            end
            table.insert(values, { name = valueName, value = numValue })
            match("punctuation", ",")
        end
        expect("punctuation", "}")
        return EnumNode(name, values, mods.access)
    end

    -- Parse an interface declaration
    local function parseInterface(mods)
        expect("keyword", "interface")
        local name = expect("identifier").value
        local methods = {}
        expect("punctuation", "{")
        while not check("punctuation", "}") and not check("eof") do
            local retType
            if check("keyword", "void") then
                retType = "void"
                advance()
            else
                retType = parseTypeName()
            end
            local methodName = expect("identifier").value
            local params = parseParameterList()
            match("punctuation", ";")
            table.insert(methods, { name = methodName, returnType = retType, parameters = params })
        end
        expect("punctuation", "}")
        return InterfaceNode(name, methods, mods.access)
    end

    -- Parse top-level
    local ast = {
        usings = {},
        namespace = nil,
        classes = {},
        enums = {},
        interfaces = {},
        diagnostics = diagnostics,
    }

    while not check("eof") do
        -- using directives
        if check("keyword", "using") then
            advance()
            local parts = {}
            table.insert(parts, expect("identifier").value)
            while match("punctuation", ".") do
                table.insert(parts, expect("identifier").value)
            end
            match("punctuation", ";")
            table.insert(ast.usings, table.concat(parts, "."))

        -- namespace
        elseif check("keyword", "namespace") then
            advance()
            local parts = {}
            table.insert(parts, expect("identifier").value)
            while match("punctuation", ".") do
                table.insert(parts, expect("identifier").value)
            end
            ast.namespace = table.concat(parts, ".")
            match("punctuation", "{")
            -- Continue parsing inside namespace

        else
            local mods = parseModifiers()
            if check("keyword", "class") then
                table.insert(ast.classes, parseClass(mods))
            elseif check("keyword", "enum") then
                table.insert(ast.enums, parseEnum(mods))
            elseif check("keyword", "interface") then
                table.insert(ast.interfaces, parseInterface(mods))
            elseif check("punctuation", "}") then
                advance() -- close namespace
            else
                -- Skip unrecognized token
                local tok = advance()
                addDiagnostic("warning", "Unexpected token: " .. tok.value, tok)
            end
        end
    end

    return ast
end

return Parser
```

**Step 4: Run tests to verify they pass**

Run: `luau plugin/tests/run.lua`
Expected: All PASS

**Step 5: Commit**

```bash
git add plugin/src/Parser.lua plugin/tests/ParserTests.lua
git commit -m "feat(plugin): implement C# parser for declarations"
```

---

### Task 4: Parser — Statements

**Files:**
- Modify: `plugin/src/Parser.lua` (replace `parseBlock` and add `parseStatement`)
- Modify: `plugin/tests/ParserTests.lua` (add statement tests)

This task replaces the placeholder `parseBlock` with real statement parsing: variable declarations, if/else, for, foreach, while, return, break, continue, try/catch, throw, switch, assignments, expression statements.

**Step 1: Write statement tests**

Add to `plugin/tests/ParserTests.lua`:

```lua
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
```

**Step 2: Run tests to verify they fail**

Run: `luau plugin/tests/run.lua`
Expected: FAIL — `parseBlock` returns raw tokens, not AST nodes

**Step 3: Replace `parseBlock` and implement `parseStatement` in `Parser.lua`**

Replace the placeholder `parseBlock` function with the real implementation. Add `parseStatement` that handles all statement types. Add `parseExpression` as a placeholder that collects tokens (filled in Task 5).

This is a substantial code addition (~400-600 lines). The key statement AST nodes:

- `{ type = "local_var", name, varType, initializer }`
- `{ type = "assignment", target, operator, value }`
- `{ type = "if", condition, body, elseIfs, elseBody }`
- `{ type = "for", init, condition, increment, body }`
- `{ type = "foreach", variable, varType, iterable, body }`
- `{ type = "while", condition, body }`
- `{ type = "do_while", condition, body }`
- `{ type = "return", value }`
- `{ type = "break" }`
- `{ type = "continue" }`
- `{ type = "try_catch", tryBody, catchVar, catchType, catchBody, finallyBody }`
- `{ type = "throw", expression }`
- `{ type = "switch", expression, cases }`
- `{ type = "expression_statement", expression }`

**Step 4: Run tests to verify they pass**

Run: `luau plugin/tests/run.lua`
Expected: All PASS

**Step 5: Commit**

```bash
git add plugin/src/Parser.lua plugin/tests/ParserTests.lua
git commit -m "feat(plugin): add statement parsing to C# parser"
```

---

### Task 5: Parser — Expressions

**Files:**
- Modify: `plugin/src/Parser.lua` (implement `parseExpression`)
- Modify: `plugin/tests/ParserTests.lua` (add expression tests)

Implements Pratt parsing (operator precedence) for expressions: binary, unary, member access, method calls, object creation, lambdas, ternary, null-conditional, string interpolation.

**Step 1: Write expression tests**

Add to `plugin/tests/ParserTests.lua`:

```lua
describe("Parser: Expressions", function()
    it("parses binary expression", function()
        local ast = parse("class C { void M() { var x = 1 + 2; } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("binary")
        expect(init.operator):toBe("+")
    end)

    it("parses method call", function()
        local ast = parse("class C { void M() { print(42); } }")
        local stmt = ast.classes[1].methods[1].body[1]
        expect(stmt.expression.type):toBe("call")
        expect(stmt.expression.name):toBe("print")
    end)

    it("parses member access", function()
        local ast = parse("class C { void M() { var x = player.Name; } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("member_access")
        expect(init.member):toBe("Name")
    end)

    it("parses object creation", function()
        local ast = parse("class C { void M() { var p = new Part(); } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("new")
        expect(init.className):toBe("Part")
    end)

    it("parses lambda expression", function()
        local ast = parse("class C { void M() { var f = x => x + 1; } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("lambda")
    end)

    it("parses string interpolation", function()
        local ast = parse('class C { void M() { var s = $"Hello {name}"; } }')
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("interpolated_string")
    end)

    it("parses ternary", function()
        local ast = parse("class C { void M() { var x = a > b ? a : b; } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("ternary")
    end)

    it("parses null coalescing", function()
        local ast = parse('class C { void M() { var x = a ?? "default"; } }')
        local init = ast.classes[1].methods[1].body[1].initializer
        expect(init.type):toBe("binary")
        expect(init.operator):toBe("??")
    end)

    it("parses chained member access + method call", function()
        local ast = parse("class C { void M() { game.GetService(\"Players\").LocalPlayer.Name; } }")
        local expr = ast.classes[1].methods[1].body[1].expression
        expect(expr.type):toBe("member_access")
    end)

    it("respects operator precedence", function()
        local ast = parse("class C { void M() { var x = 1 + 2 * 3; } }")
        local init = ast.classes[1].methods[1].body[1].initializer
        -- Should be +(1, *(2, 3)) not *(+(1, 2), 3)
        expect(init.type):toBe("binary")
        expect(init.operator):toBe("+")
        expect(init.right.operator):toBe("*")
    end)
end)
```

**Step 2: Run tests, verify fail, implement Pratt expression parser**

The expression parser uses Pratt parsing with precedence levels. Key expression AST nodes:

- `{ type = "literal", value, literalType }` — numbers, strings, booleans, null
- `{ type = "identifier", name }`
- `{ type = "binary", left, operator, right }`
- `{ type = "unary", operator, operand, isPrefix }`
- `{ type = "member_access", object, member }`
- `{ type = "call", target, arguments }`
- `{ type = "method_call", object, method, arguments }`
- `{ type = "new", className, arguments, initializer }`
- `{ type = "lambda", parameters, body }`
- `{ type = "ternary", condition, thenExpr, elseExpr }`
- `{ type = "cast", targetType, expression }`
- `{ type = "is", expression, targetType, variable }`
- `{ type = "as", expression, targetType }`
- `{ type = "interpolated_string", parts }`
- `{ type = "index", object, key }`
- `{ type = "null_conditional", object, member }`
- `{ type = "this" }`
- `{ type = "base" }`
- `{ type = "typeof", targetType }`

**Step 3: Run tests, verify pass**

**Step 4: Commit**

```bash
git add plugin/src/Parser.lua plugin/tests/ParserTests.lua
git commit -m "feat(plugin): add Pratt expression parsing to C# parser"
```

---

### Task 6: Lowerer (C# AST → Lua IR)

**Files:**
- Create: `plugin/src/Lowerer.lua`
- Create: `plugin/tests/LowererTests.lua`

**Step 1: Write Lowerer tests**

```lua
local Lexer = require("plugin/src/Lexer")
local Parser = require("plugin/src/Parser")
local Lowerer = require("plugin/src/Lowerer")

local function lower(source)
    local tokens = Lexer.tokenize(source)
    local ast = Parser.parse(tokens)
    return Lowerer.lower(ast)
end

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
        -- The lowered call should be print, not Console.WriteLine
        local stmt = ir.modules[1].classes[1].methods[1].body[1]
        expect(stmt.type):toBe("expr_statement")
    end)

    it("lowers string interpolation to backtick template", function()
        local ir = lower([[
class Foo {
    public void M() { var s = $"Hello {name}"; }
}]])
        local stmt = ir.modules[1].classes[1].methods[1].body[1]
        expect(stmt.type):toBe("local_decl")
    end)

    it("lowers foreach to for-in pairs", function()
        local ir = lower([[
class Foo {
    public void M() { foreach (var x in list) { } }
}]])
        local stmt = ir.modules[1].classes[1].methods[1].body[1]
        expect(stmt.type):toBe("for_in")
    end)

    it("determines ScriptType from base class", function()
        local ir = lower("class Foo : RobloxScript { public void GameEntry() { } }")
        expect(ir.modules[1].scriptType):toBe("Script")
    end)

    it("determines ModuleScript for shared classes", function()
        local ir = lower("class Foo : ModuleScript { }")
        expect(ir.modules[1].scriptType):toBe("ModuleScript")
    end)
end)
```

**Step 2: Implement Lowerer**

The Lowerer walks the C# AST and produces Lua IR tables. Key mappings are defined in the design doc. The output IR structure mirrors `LuaModule` / `LuaClassDef` / `LuaMethodDef` from the .NET transpiler but represented as Luau tables.

**Step 3: Run tests, verify pass, commit**

```bash
git add plugin/src/Lowerer.lua plugin/tests/LowererTests.lua
git commit -m "feat(plugin): implement C# to Lua IR lowerer"
```

---

### Task 7: Emitter (Lua IR → Luau text)

**Files:**
- Create: `plugin/src/Emitter.lua`
- Create: `plugin/tests/EmitterTests.lua`

**Step 1: Write Emitter tests**

```lua
local Lexer = require("plugin/src/Lexer")
local Parser = require("plugin/src/Parser")
local Lowerer = require("plugin/src/Lowerer")
local Emitter = require("plugin/src/Emitter")

local function compile(source)
    local tokens = Lexer.tokenize(source)
    local ast = Parser.parse(tokens)
    local ir = Lowerer.lower(ast)
    return Emitter.emit(ir.modules[1])
end

describe("Emitter", function()
    it("emits a simple class", function()
        local lua = compile("class Foo { }")
        expect(lua):toContain("local Foo = {}")
    end)

    it("emits static fields", function()
        local lua = compile("class Foo { public static int X = 42; }")
        expect(lua):toContain("Foo.X = 42")
    end)

    it("emits instance method with colon syntax", function()
        local lua = compile([[
class Foo {
    public void DoStuff() { }
}]])
        expect(lua):toContain("function Foo:DoStuff()")
    end)

    it("emits static method with dot syntax", function()
        local lua = compile([[
class Foo {
    public static void DoStuff() { }
}]])
        expect(lua):toContain("function Foo.DoStuff()")
    end)

    it("emits GameEntry call for Script types", function()
        local lua = compile([[
class Foo : RobloxScript {
    public override void GameEntry() { print("hi"); }
}]])
        expect(lua):toContain("Foo:GameEntry()")
    end)

    it("emits return for ModuleScript types", function()
        local lua = compile("class Foo : ModuleScript { }")
        expect(lua):toContain("return Foo")
    end)

    it("emits print for Console.WriteLine", function()
        local lua = compile([[
class Foo {
    public void M() { Console.WriteLine("test"); }
}]])
        expect(lua):toContain('print("test")')
    end)

    it("emits backtick string for interpolation", function()
        local lua = compile([[
class Foo {
    public void M() { var s = $"Hello {name}"; }
}]])
        expect(lua):toContain("`Hello {name}`")
    end)

    it("emits proper indentation", function()
        local lua = compile([[
class Foo {
    public void M() {
        if (true) {
            print("yes");
        }
    }
}]])
        expect(lua):toContain("    if true then")
        expect(lua):toContain("        print")
    end)
end)
```

**Step 2: Implement Emitter**

The Emitter walks the Lua IR and produces indented Luau text. Uses an indent-tracking writer (same pattern as the .NET `LuaWriter`).

**Step 3: Run tests, verify pass, commit**

```bash
git add plugin/src/Emitter.lua plugin/tests/EmitterTests.lua
git commit -m "feat(plugin): implement Lua IR to Luau text emitter"
```

---

### Task 8: Type Database Generator

**Files:**
- Create: `LUSharp/TypeDatabaseGenerator.cs` (C# tool)
- Create: `plugin/src/TypeDatabase.lua` (generated output)

A C# tool that scrapes `LUSharpAPI` via reflection and generates the `TypeDatabase.lua` file with all Roblox types, members, and signatures.

**Step 1: Create the generator**

The generator uses reflection to walk all public types in `LUSharpAPI.dll`, extract their members, and output a Luau table.

**Step 2: Run the generator**

Run: `dotnet run --project LUSharp -- generate-types > plugin/src/TypeDatabase.lua`
Expected: Produces a Luau file with the full type database

**Step 3: Commit**

```bash
git add LUSharp/TypeDatabaseGenerator.cs plugin/src/TypeDatabase.lua
git commit -m "feat: add type database generator for IntelliSense"
```

---

### Task 9: Syntax Highlighter

**Files:**
- Create: `plugin/src/SyntaxHighlighter.lua`

Reuses the Lexer (with `preserveComments = true`) to tokenize C# source and generate RichText markup. Maps each token type to a color. Returns a formatted string for the TextLabel overlay.

**Step 1: Implement SyntaxHighlighter**

```lua
local Lexer = require(script.Parent.Lexer)

local SyntaxHighlighter = {}

local COLORS = {
    keyword = "#569CD6",
    identifier = "#D4D4D4",
    number = "#B5CEA8",
    string = "#CE9178",
    interpolated_string = "#CE9178",
    comment = "#6A9955",
    operator = "#D4D4D4",
    punctuation = "#D4D4D4",
    type = "#4EC9B0",
}

local TYPE_NAMES = {
    string = true, int = true, float = true, double = true, bool = true,
    void = true, object = true, var = true, byte = true, short = true,
    long = true, char = true, decimal = true,
    -- Roblox types
    Instance = true, Part = true, Model = true, Vector3 = true,
    CFrame = true, Color3 = true, Players = true, Workspace = true,
}

function SyntaxHighlighter.highlight(source)
    local tokens = Lexer.tokenize(source, { preserveComments = true })
    local result = ""
    local lastPos = 1

    for _, token in ipairs(tokens) do
        if token.type == "eof" then break end

        local color = COLORS[token.type] or COLORS.identifier
        -- Override color for known type names
        if token.type == "identifier" and TYPE_NAMES[token.value] then
            color = COLORS.type
        end

        local escaped = token.value
            :gsub("&", "&amp;")
            :gsub("<", "&lt;")
            :gsub(">", "&gt;")
            :gsub('"', "&quot;")

        result ..= '<font color="' .. color .. '">' .. escaped .. "</font>"
    end

    return result
end

return SyntaxHighlighter
```

**Step 2: Commit**

```bash
git add plugin/src/SyntaxHighlighter.lua
git commit -m "feat(plugin): implement syntax highlighter with RichText coloring"
```

---

### Task 10: Editor Dock Widget

**Files:**
- Create: `plugin/src/Editor.lua`

The core dock widget with: hidden TextBox + RichText TextLabel overlay, line numbers, scrolling, Tab intercept, auto-indent, status bar.

**Step 1: Implement Editor**

Creates the dock widget GUI hierarchy, wires up TextBox input to the RichText overlay via the SyntaxHighlighter, handles Tab/Enter interception, and tracks cursor position for the status bar.

Key elements:
- `DockWidgetPluginGui` with title "LUSharp Editor"
- `ScrollingFrame` → `Frame` → `TextLabel` (line numbers) + `Frame` → `TextBox` (hidden) + `TextLabel` (RichText overlay)
- `TextBox.Changed` → re-highlight → update TextLabel RichText
- `UserInputService.InputBegan` → intercept Tab key → insert spaces
- Enter → auto-indent based on previous line + `{` detection
- Status bar TextLabel showing `filename | Ln X, Col Y`

**Step 2: Test manually in Studio**

Install plugin via Rojo, open Studio, verify the dock widget appears and responds to input.

**Step 3: Commit**

```bash
git add plugin/src/Editor.lua
git commit -m "feat(plugin): implement code editor dock widget"
```

---

### Task 11: Script Manager

**Files:**
- Create: `plugin/src/ScriptManager.lua`

Discovers ModuleScripts tagged `"LUSharp"` via CollectionService. Manages CSharpSource StringValue children. Provides create/delete/rename operations.

**Step 1: Implement ScriptManager**

```lua
local CollectionService = game:GetService("CollectionService")

local ScriptManager = {}

function ScriptManager.getAll()
    return CollectionService:GetTagged("LUSharp")
end

function ScriptManager.getSource(script)
    local sv = script:FindFirstChild("CSharpSource")
    if sv and sv:IsA("StringValue") then
        return sv.Value
    end
    return nil
end

function ScriptManager.setSource(script, source)
    local sv = script:FindFirstChild("CSharpSource")
    if not sv then
        sv = Instance.new("StringValue")
        sv.Name = "CSharpSource"
        sv.Parent = script
    end
    sv.Value = source
end

function ScriptManager.createScript(name, parent, context)
    local mod = Instance.new("ModuleScript")
    mod.Name = name
    mod.Source = "-- Compiled by LUSharp (do not edit)\nreturn {}"
    mod.Parent = parent

    CollectionService:AddTag(mod, "LUSharp")

    local sv = Instance.new("StringValue")
    sv.Name = "CSharpSource"
    sv.Value = "public class " .. name .. " {\n    \n}\n"
    sv.Parent = mod

    return mod
end

function ScriptManager.deleteScript(script)
    CollectionService:RemoveTag(script, "LUSharp")
    script:Destroy()
end

return ScriptManager
```

**Step 2: Commit**

```bash
git add plugin/src/ScriptManager.lua
git commit -m "feat(plugin): implement script manager with CollectionService tagging"
```

---

### Task 12: IntelliSense

**Files:**
- Create: `plugin/src/IntelliSense.lua`

Provides autocomplete suggestions and parameter hints based on the TypeDatabase and a cached symbol table from previous compiles.

**Step 1: Implement IntelliSense**

Key functions:
- `getCompletions(source, cursorPos)` → list of `{ label, kind, detail, documentation }`
- `getParameterHints(source, cursorPos)` → `{ methodName, parameters, activeParam }`
- `getDiagnostics(parseResult)` → list of diagnostic entries for the editor

Autocomplete logic:
1. Tokenize up to cursor position
2. Look at the token before cursor:
   - After `.` → resolve the object type, return its members from TypeDatabase
   - After `new ` → return constructable types
   - After `(` → return parameter hints
   - Otherwise → return matching keywords, types, and local variables

**Step 2: Commit**

```bash
git add plugin/src/IntelliSense.lua
git commit -m "feat(plugin): implement IntelliSense with autocomplete and diagnostics"
```

---

### Task 13: Project View

**Files:**
- Create: `plugin/src/ProjectView.lua`

Dock widget showing all C# scripts as a tree organized by location (Server/Client/Shared). Click to open in editor. Right-click menu for create/rename/delete.

**Step 1: Implement ProjectView**

Creates a second `DockWidgetPluginGui`. Uses a `ScrollingFrame` with `UIListLayout` containing `TextButton` entries for each tagged script. Groups by parent service. Wires click to open in the Editor widget.

**Step 2: Commit**

```bash
git add plugin/src/ProjectView.lua
git commit -m "feat(plugin): implement project view dock widget"
```

---

### Task 14: Settings

**Files:**
- Create: `plugin/src/Settings.lua`

Configuration panel using `plugin:GetSetting()` / `plugin:SetSetting()`. Exposes: theme (Dark/Light), font size, tab width, auto-indent toggle, line numbers toggle.

**Step 1: Implement Settings**

**Step 2: Commit**

```bash
git add plugin/src/Settings.lua
git commit -m "feat(plugin): implement settings panel with persistence"
```

---

### Task 15: Init — Wire Everything Together

**Files:**
- Modify: `plugin/src/init.lua`

Replace the stub with the full plugin entry point. Creates toolbar buttons, dock widgets, wires build flow:

1. "New C# Script" → prompts for name, calls `ScriptManager.createScript()`
2. "Build" → reads CSharpSource from selected script → Lexer → Parser → Lowerer → Emitter → writes to Source
3. "Build All" → iterates all tagged scripts
4. Selection change → loads script into Editor widget
5. Wires IntelliSense to the Editor for autocomplete popups

**Step 1: Implement full init**

**Step 2: Build plugin**

Run: `cd plugin && rojo build -o LUSharp.rbxmx`

**Step 3: Test in Studio**

- Install `LUSharp.rbxmx` into Studio's plugins folder
- Create a new C# script via toolbar
- Write C# in the editor
- Click Build
- Verify compiled Luau appears in the ModuleScript's Source

**Step 4: Commit**

```bash
git add plugin/src/init.lua
git commit -m "feat(plugin): wire all modules into plugin entry point"
```

---

### Task 16: End-to-End Integration Test

**Step 1: Create test C# scripts in Studio**

Create three tagged ModuleScripts:
- `ServerMain` in ServerScriptService with C#:
  ```csharp
  public class ServerMain : RobloxScript {
      public override void GameEntry() {
          print("Server started!");
      }
  }
  ```
- `ClientMain` in StarterPlayerScripts
- `SharedModule` in ReplicatedStorage

**Step 2: Build All**

Click "Build All". Verify:
- ServerMain.Source contains valid Luau with `ServerMain:GameEntry()` call
- ClientMain.Source contains valid Luau with `ClientMain:GameEntry()` call
- SharedModule.Source contains `return SharedModule`

**Step 3: Playtest**

Enter Play mode in Studio. Verify:
- Server prints "Server started!"
- Client prints its message
- No runtime errors

**Step 4: Test IntelliSense**

- Type `game.` → verify Roblox service completions appear
- Type `player.` → verify Player member completions
- Introduce a syntax error → verify red underline diagnostic appears

**Step 5: Final commit**

```bash
git add -A
git commit -m "feat: LUSharp Studio Plugin v0.1 — complete"
```
