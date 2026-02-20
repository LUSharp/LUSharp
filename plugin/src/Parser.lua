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
    local parseTypeName

    -- Parse a type name (including generics: List<int>, Dictionary<string, int>)
    parseTypeName = function()
        local tok = current()
        local name
        -- Handle keyword types (string, int, void, bool, float, double, etc.)
        if tok.type == "keyword" then
            name = advance().value
        else
            name = expect("identifier").value
        end
        if check("operator", "<") then
            advance() -- <
            name ..= "<"
            name ..= parseTypeName()
            while match("punctuation", ",") do
                name ..= ", "
                name ..= parseTypeName()
            end
            expect("operator", ">")
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
                default = advance().value
            end
            table.insert(params, ParameterNode(paramName, paramType, default))
            if not match("punctuation", ",") then break end
        end
        expect("punctuation", ")")
        return params
    end

    -- Set of C# keyword types that start a variable declaration
    local TYPE_KEYWORDS = {
        int = true, string = true, float = true, double = true, bool = true,
        byte = true, short = true, long = true, char = true, decimal = true,
        uint = true, ulong = true, ushort = true, object = true, void = true,
    }

    -- Parse a parenthesized argument list: (expr, expr, ...)
    local function parseArguments()
        local args = {}
        expect("punctuation", "(")
        while not check("punctuation", ")") and not check("eof") do
            table.insert(args, parseExpression())
            if not match("punctuation", ",") then break end
        end
        expect("punctuation", ")")
        return args
    end

    -- Parse a primary expression (atom): literal, identifier, new, parenthesized
    local function parsePrimaryExpression()
        -- Parenthesized expression
        if check("punctuation", "(") then
            advance() -- (
            local expr = parseExpression()
            expect("punctuation", ")")
            return expr
        end

        -- new ClassName(args)
        if check("keyword", "new") then
            advance() -- new
            local className = parseTypeName()
            local args = {}
            if check("punctuation", "(") then
                args = parseArguments()
            end
            return { type = "new", className = className, arguments = args }
        end

        -- Boolean literals
        if check("keyword", "true") or check("keyword", "false") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "bool" }
        end

        -- null literal
        if check("keyword", "null") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "null" }
        end

        -- this keyword
        if check("keyword", "this") then
            advance()
            return { type = "identifier", name = "this" }
        end

        -- typeof
        if check("keyword", "typeof") then
            advance()
            expect("punctuation", "(")
            local typeName = parseTypeName()
            expect("punctuation", ")")
            return { type = "typeof", typeName = typeName }
        end

        -- Number literal
        if check("number") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "number" }
        end

        -- String literal
        if check("string") or check("interpolated_string") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "string" }
        end

        -- Identifier
        if check("identifier") then
            local tok = advance()
            return { type = "identifier", name = tok.value }
        end

        -- Fallback: skip the token and return a generic node
        local tok = advance()
        return { type = "unknown", value = tok.value }
    end

    -- Parse postfix: member access (a.b), method calls (a()), indexing (a[])
    local function parsePostfix(expr)
        while true do
            -- Member access: expr.member
            if check("punctuation", ".") then
                advance() -- .
                local memberName = expect("identifier").value
                expr = { type = "member_access", object = expr, member = memberName }
            -- Method call: expr(args)
            elseif check("punctuation", "(") then
                local args = parseArguments()
                expr = { type = "call", callee = expr, arguments = args }
            -- Indexing: expr[index]
            elseif check("punctuation", "[") then
                advance() -- [
                local index = parseExpression()
                expect("punctuation", "]")
                expr = { type = "index", object = expr, index = index }
            else
                break
            end
        end
        return expr
    end

    -- Parse unary prefix: !, -, ++, --
    local function parseUnary()
        if check("operator", "!") or check("operator", "-") then
            local op = advance().value
            local operand = parseUnary()
            return { type = "unary", operator = op, operand = operand }
        end
        if check("operator", "++") or check("operator", "--") then
            local op = advance().value
            local operand = parseUnary()
            return { type = "unary_prefix", operator = op, operand = operand }
        end
        local expr = parsePrimaryExpression()
        expr = parsePostfix(expr)
        -- Postfix ++ / --
        if check("operator", "++") or check("operator", "--") then
            local op = advance().value
            expr = { type = "unary_postfix", operator = op, operand = expr }
        end
        return expr
    end

    -- Operator precedence for binary operators
    local PRECEDENCE = {
        ["*"] = 12, ["/"] = 12, ["%"] = 12,
        ["+"] = 11, ["-"] = 11,
        ["<"] = 9, [">"] = 9, ["<="] = 9, [">="] = 9,
        ["=="] = 8, ["!="] = 8,
        ["&&"] = 4,
        ["||"] = 3,
        ["??"] = 2,
    }

    -- Parse binary expression with precedence climbing
    local function parseBinaryExpression(minPrec)
        local left = parseUnary()
        while true do
            local tok = current()
            if tok.type ~= "operator" then break end
            local prec = PRECEDENCE[tok.value]
            if not prec or prec < minPrec then break end
            local op = advance().value
            local right = parseBinaryExpression(prec + 1)
            left = { type = "binary", operator = op, left = left, right = right }
        end
        return left
    end

    -- Parse a full expression (including assignment)
    parseExpression = function()
        local expr = parseBinaryExpression(1)

        -- Assignment operators: =, +=, -=, *=, /=, %=
        if check("operator", "=") or check("operator", "+=") or check("operator", "-=")
            or check("operator", "*=") or check("operator", "/=") or check("operator", "%=") then
            local op = advance().value
            local right = parseExpression()
            return { type = "assignment", target = expr, operator = op, value = right }
        end

        return expr
    end

    -- Parse a block { ... }
    parseBlock = function()
        local stmts = {}
        expect("punctuation", "{")
        while not check("punctuation", "}") and not check("eof") do
            local stmt = parseStatement()
            if stmt then
                table.insert(stmts, stmt)
            end
        end
        expect("punctuation", "}")
        return stmts
    end

    -- Determine if current position looks like a variable declaration:
    -- TypeKeyword identifier  OR  identifier identifier
    local function isVariableDeclaration()
        local tok = current()
        -- var keyword is always a declaration
        if tok.type == "keyword" and tok.value == "var" then
            return true
        end
        -- Keyword types: int x, string y, etc.
        if tok.type == "keyword" and TYPE_KEYWORDS[tok.value] then
            -- Next token should be identifier (the variable name)
            local next = peek(1)
            if next.type == "identifier" then
                return true
            end
            -- Could be generic: List<int> x
            if next.type == "operator" and next.value == "<" then
                return true
            end
            -- Could be array: int[] x
            if next.type == "punctuation" and next.value == "[" then
                return true
            end
        end
        -- Identifier followed by identifier: MyType myVar
        if tok.type == "identifier" then
            local next = peek(1)
            -- Simple: MyType myVar
            if next.type == "identifier" then
                return true
            end
            -- Generic: List<int> myVar  — identifier followed by <
            if next.type == "operator" and next.value == "<" then
                -- Heuristic: if after closing >, there's an identifier, it's a declaration
                -- This is approximate; we just check the pattern
                -- Save position, try to parse type, see if next is identifier
                local savePos = pos
                pcall(function()
                    parseTypeName()
                end)
                local isDecl = check("identifier")
                pos = savePos
                return isDecl
            end
            -- Array: MyType[] myVar
            if next.type == "punctuation" and next.value == "[" and peek(2).value == "]" then
                return true
            end
        end
        return false
    end

    -- Parse a single statement
    parseStatement = function()
        -- var declaration: var x = expr;
        if check("keyword", "var") then
            advance() -- var
            local name = expect("identifier").value
            local initializer = nil
            if match("operator", "=") then
                initializer = parseExpression()
            end
            match("punctuation", ";")
            return { type = "local_var", name = name, varType = "var", initializer = initializer }
        end

        -- if statement
        if check("keyword", "if") then
            advance() -- if
            expect("punctuation", "(")
            local condition = parseExpression()
            expect("punctuation", ")")
            local body = parseBlock()
            local elseIfs = {}
            local elseBody = nil
            while check("keyword", "else") do
                advance() -- else
                if check("keyword", "if") then
                    advance() -- if
                    expect("punctuation", "(")
                    local elifCond = parseExpression()
                    expect("punctuation", ")")
                    local elifBody = parseBlock()
                    table.insert(elseIfs, { condition = elifCond, body = elifBody })
                else
                    elseBody = parseBlock()
                    break
                end
            end
            return { type = "if", condition = condition, body = body, elseIfs = elseIfs, elseBody = elseBody }
        end

        -- for loop
        if check("keyword", "for") then
            advance() -- for
            expect("punctuation", "(")
            -- Init part: could be variable declaration or expression
            local init = nil
            if not check("punctuation", ";") then
                if isVariableDeclaration() then
                    local varType = parseTypeName()
                    local name = expect("identifier").value
                    local initializer = nil
                    if match("operator", "=") then
                        initializer = parseExpression()
                    end
                    init = { type = "local_var", name = name, varType = varType, initializer = initializer }
                else
                    init = parseExpression()
                end
            end
            expect("punctuation", ";")
            -- Condition
            local condition = nil
            if not check("punctuation", ";") then
                condition = parseExpression()
            end
            expect("punctuation", ";")
            -- Increment
            local increment = nil
            if not check("punctuation", ")") then
                increment = parseExpression()
            end
            expect("punctuation", ")")
            local body = parseBlock()
            return { type = "for", init = init, condition = condition, increment = increment, body = body }
        end

        -- foreach loop
        if check("keyword", "foreach") then
            advance() -- foreach
            expect("punctuation", "(")
            local varType
            if check("keyword", "var") then
                varType = "var"
                advance()
            else
                varType = parseTypeName()
            end
            local variable = expect("identifier").value
            expect("keyword", "in")
            local iterable = parseExpression()
            expect("punctuation", ")")
            local body = parseBlock()
            return { type = "foreach", variable = variable, varType = varType, iterable = iterable, body = body }
        end

        -- while loop
        if check("keyword", "while") then
            advance() -- while
            expect("punctuation", "(")
            local condition = parseExpression()
            expect("punctuation", ")")
            local body = parseBlock()
            return { type = "while", condition = condition, body = body }
        end

        -- do-while loop
        if check("keyword", "do") then
            advance() -- do
            local body = parseBlock()
            expect("keyword", "while")
            expect("punctuation", "(")
            local condition = parseExpression()
            expect("punctuation", ")")
            match("punctuation", ";")
            return { type = "do_while", condition = condition, body = body }
        end

        -- return statement
        if check("keyword", "return") then
            advance() -- return
            local value = nil
            if not check("punctuation", ";") and not check("punctuation", "}") then
                value = parseExpression()
            end
            match("punctuation", ";")
            return { type = "return", value = value }
        end

        -- break
        if check("keyword", "break") then
            advance()
            match("punctuation", ";")
            return { type = "break" }
        end

        -- continue
        if check("keyword", "continue") then
            advance()
            match("punctuation", ";")
            return { type = "continue" }
        end

        -- try/catch/finally
        if check("keyword", "try") then
            advance() -- try
            local tryBody = parseBlock()
            local catchType = nil
            local catchVar = nil
            local catchBody = nil
            local finallyBody = nil
            if check("keyword", "catch") then
                advance() -- catch
                if check("punctuation", "(") then
                    advance() -- (
                    catchType = parseTypeName()
                    if check("identifier") then
                        catchVar = advance().value
                    end
                    expect("punctuation", ")")
                end
                catchBody = parseBlock()
            end
            if check("keyword", "finally") then
                advance() -- finally
                finallyBody = parseBlock()
            end
            return { type = "try_catch", tryBody = tryBody, catchType = catchType, catchVar = catchVar, catchBody = catchBody, finallyBody = finallyBody }
        end

        -- throw
        if check("keyword", "throw") then
            advance() -- throw
            local expression = nil
            if not check("punctuation", ";") then
                expression = parseExpression()
            end
            match("punctuation", ";")
            return { type = "throw", expression = expression }
        end

        -- switch
        if check("keyword", "switch") then
            advance() -- switch
            expect("punctuation", "(")
            local expression = parseExpression()
            expect("punctuation", ")")
            expect("punctuation", "{")
            local cases = {}
            while not check("punctuation", "}") and not check("eof") do
                if check("keyword", "case") then
                    advance() -- case
                    local caseValue = parseExpression()
                    expect("punctuation", ":")
                    local stmts = {}
                    while not check("keyword", "case") and not check("keyword", "default")
                        and not check("punctuation", "}") and not check("eof") do
                        local stmt = parseStatement()
                        if stmt then table.insert(stmts, stmt) end
                    end
                    table.insert(cases, { type = "case", value = caseValue, body = stmts })
                elseif check("keyword", "default") then
                    advance() -- default
                    expect("punctuation", ":")
                    local stmts = {}
                    while not check("keyword", "case") and not check("keyword", "default")
                        and not check("punctuation", "}") and not check("eof") do
                        local stmt = parseStatement()
                        if stmt then table.insert(stmts, stmt) end
                    end
                    table.insert(cases, { type = "default", body = stmts })
                else
                    advance() -- skip unexpected token
                end
            end
            expect("punctuation", "}")
            return { type = "switch", expression = expression, cases = cases }
        end

        -- Variable declaration: Type name = expr;
        if isVariableDeclaration() then
            local varType = parseTypeName()
            local name = expect("identifier").value
            local initializer = nil
            if match("operator", "=") then
                initializer = parseExpression()
            end
            match("punctuation", ";")
            return { type = "local_var", name = name, varType = varType, initializer = initializer }
        end

        -- Expression statement (assignment, method call, etc.)
        local expr = parseExpression()
        match("punctuation", ";")
        return { type = "expression_statement", expression = expr }
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
                local tok = advance()
                addDiagnostic("warning", "Unexpected token: " .. tok.value, tok)
            end
        end
    end

    return ast
end

return Parser
