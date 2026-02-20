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

    -- Parse a type name (including generics: List<int>, Dictionary<string, int>)
    local function parseTypeName()
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

    -- Parse a block { ... } -- placeholder, filled in Task 4
    parseBlock = function()
        local stmts = {}
        expect("punctuation", "{")
        local depth = 1
        while depth > 0 and not check("eof") do
            if check("punctuation", "{") then depth += 1
            elseif check("punctuation", "}") then depth -= 1
                if depth == 0 then break end
            end
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
