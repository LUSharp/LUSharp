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
        isAsync = modifiers.isAsync or false,
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

function Parser.parse(tokens, options)
    local pos = 1
    local diagnostics = {}

    options = type(options) == "table" and options or {}

    local yieldEvery = tonumber(options.yieldEvery) or 0
    if yieldEvery < 1 then
        yieldEvery = 0
    else
        yieldEvery = math.floor(yieldEvery)
    end

    local maxOperations = tonumber(options.maxOperations) or 0
    if maxOperations < 1 then
        maxOperations = 0
    else
        maxOperations = math.floor(maxOperations)
    end

    local canYield = type(task) == "table" and type(task.wait) == "function"
    local operationCount = 0
    local parseAborted = false
    local addedBudgetDiagnostic = false

    local function registerOperation()
        operationCount += 1

        if maxOperations > 0 and operationCount > maxOperations then
            if not addedBudgetDiagnostic then
                local cur = tokens[math.clamp(pos, 1, #tokens)] or tokens[#tokens] or { line = 1, column = 1, value = "" }
                local tokenLength = math.max(1, #(cur.value or ""))
                table.insert(diagnostics, {
                    severity = "warning",
                    message = "Parser operation budget exceeded; diagnostics may be incomplete.",
                    line = cur.line,
                    column = cur.column,
                    endLine = cur.line,
                    endColumn = cur.column + tokenLength,
                    length = tokenLength,
                })
                addedBudgetDiagnostic = true
            end

            parseAborted = true
            pos = #tokens
            return
        end

        if canYield and yieldEvery > 0 and (operationCount % yieldEvery == 0) then
            task.wait()
        end
    end

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
        registerOperation()
        local tok = current()
        pos += 1
        return tok
    end

    local function check(type, value)
        registerOperation()
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
            local tokenLength = math.max(1, #(cur.value or ""))
            table.insert(diagnostics, {
                severity = "error",
                message = msg,
                line = cur.line,
                column = cur.column,
                endLine = cur.line,
                endColumn = cur.column + tokenLength,
                length = tokenLength,
            })
            return { type = type, value = value or "", line = cur.line, column = cur.column }
        end
        return tok
    end

    local function addDiagnostic(severity, message, tok)
        tok = tok or current()
        local tokenLength = math.max(1, #(tok.value or ""))
        table.insert(diagnostics, {
            severity = severity,
            message = message,
            line = tok.line,
            column = tok.column,
            endLine = tok.line,
            endColumn = tok.column + tokenLength,
            length = tokenLength,
        })
    end

    -- Forward declarations for mutual recursion
    local parseExpression
    local parseStatement
    local parseBlock
    local parseTypeName
    local parseUnary

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
            isConst = false, isSealed = false, isAsync = false }
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
            elseif check("keyword", "async") then mods.isAsync = true; advance()
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

    local function isParenthesizedLambdaAt(startIndex)
        if not tokens[startIndex] then
            return false
        end

        if tokens[startIndex].type ~= "punctuation" or tokens[startIndex].value ~= "(" then
            return false
        end

        local isLambda = false
        local depth = 0
        local i = startIndex

        while i <= #tokens do
            local tok = tokens[i]
            if tok.type == "punctuation" and tok.value == "(" then
                depth += 1
            elseif tok.type == "punctuation" and tok.value == ")" then
                depth -= 1
                if depth == 0 then
                    if i + 1 <= #tokens and tokens[i + 1].type == "operator" and tokens[i + 1].value == "=>" then
                        local valid = true
                        for j = startIndex + 1, i - 1 do
                            local t = tokens[j]
                            if t.type ~= "identifier" and not (t.type == "punctuation" and t.value == ",")
                                and not (t.type == "keyword" and TYPE_KEYWORDS[t.value]) then
                                valid = false
                                break
                            end
                        end
                        isLambda = valid
                    end
                    break
                end
            elseif tok.type == "eof" then
                break
            end
            i += 1
        end

        return isLambda
    end

    -- Helper: check if current position is a lambda expression
    -- Supports: x => ..., (x, y) => ..., async x => ..., async (x, y) => ...
    local function isLambdaExpression()
        if check("keyword", "async") then
            if peek(1).type == "identifier" and peek(2).type == "operator" and peek(2).value == "=>" then
                return true
            end

            if peek(1).type == "punctuation" and peek(1).value == "(" then
                return isParenthesizedLambdaAt(pos + 1)
            end
        end

        if check("identifier") and peek(1).type == "operator" and peek(1).value == "=>" then
            return true
        end

        if check("punctuation", "(") then
            return isParenthesizedLambdaAt(pos)
        end

        return false
    end

    -- Parse lambda expression: x => expr  or  (x, y) => { stmts } / (x, y) => expr
    local function parseLambda()
        local params = {}
        local isAsync = false

        if check("keyword", "async") then
            isAsync = true
            advance() -- async
        end

        if check("punctuation", "(") then
            advance() -- (
            while not check("punctuation", ")") and not check("eof") do
                local paramName = expect("identifier").value
                table.insert(params, paramName)
                if not match("punctuation", ",") then break end
            end
            expect("punctuation", ")")
        else
            -- Single parameter
            local paramName = expect("identifier").value
            table.insert(params, paramName)
        end
        expect("operator", "=>") -- =>
        local body
        if check("punctuation", "{") then
            body = parseBlock()
        else
            body = parseExpression()
        end
        return { type = "lambda", parameters = params, body = body, isAsync = isAsync }
    end

    -- Parse a primary expression (atom): literal, identifier, new, parenthesized, lambda
    local function parsePrimaryExpression()
        -- Lambda: must check before parenthesized expression and identifier
        if isLambdaExpression() then
            return parseLambda()
        end

        -- Parenthesized expression (or cast)
        if check("punctuation", "(") then
            -- Try to detect cast: (TypeName)expr
            -- Heuristic: if contents is a single type keyword or identifier and closing ) is
            -- followed by something that looks like an expression start (not an operator)
            local savePos = pos
            advance() -- (
            local castCandidate = false
            if (check("keyword") and TYPE_KEYWORDS[current().value]) or check("identifier") then
                local typeTok = current()
                local typePos = pos
                -- Try parsing as type name
                local ok, typeName = pcall(function() return parseTypeName() end)
                if ok and check("punctuation", ")") then
                    advance() -- )
                    -- Check what follows: if it's an expression start, treat as cast
                    local next = current()
                    if next.type == "identifier" or next.type == "number" or next.type == "string"
                        or next.type == "interpolated_string"
                        or (next.type == "keyword" and (next.value == "new" or next.value == "true"
                            or next.value == "false" or next.value == "null" or next.value == "this"
                            or next.value == "base" or next.value == "typeof"))
                        or (next.type == "operator" and (next.value == "!" or next.value == "-"
                            or next.value == "++" or next.value == "--"))
                        or (next.type == "punctuation" and next.value == "(") then
                        -- It's a cast
                        local expression = parseUnary()
                        return { type = "cast", targetType = typeName, expression = expression }
                    end
                end
            end
            -- Not a cast; restore and parse as parenthesized expression
            pos = savePos
            advance() -- (
            local expr = parseExpression()
            expect("punctuation", ")")
            return expr
        end

        -- new ClassName(args) OR target-typed new(), optionally with collection initializer
        if check("keyword", "new") then
            advance() -- new

            local className = nil
            if not check("punctuation", "(") then
                className = parseTypeName()
            end

            local args = {}
            if check("punctuation", "(") then
                args = parseArguments()
            end

            local initializer = nil
            if check("punctuation", "{") then
                advance() -- {
                initializer = {}

                while not check("punctuation", "}") and not check("eof") do
                    table.insert(initializer, parseExpression())
                    if not match("punctuation", ",") then
                        break
                    end
                end

                expect("punctuation", "}")
            end

            return { type = "new", className = className, arguments = args, initializer = initializer }
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
            return { type = "this" }
        end

        -- base keyword
        if check("keyword", "base") then
            advance()
            return { type = "base" }
        end

        -- typeof
        if check("keyword", "typeof") then
            advance()
            expect("punctuation", "(")
            local typeName = parseTypeName()
            expect("punctuation", ")")
            return { type = "typeof", targetType = typeName }
        end

        -- Number literal
        if check("number") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "number" }
        end

        -- Interpolated string literal
        if check("interpolated_string") then
            local tok = advance()
            return { type = "interpolated_string", value = tok.value }
        end

        -- String literal
        if check("string") then
            local tok = advance()
            return { type = "literal", value = tok.value, literalType = "string" }
        end

        -- Identifier
        if check("identifier") then
            local tok = advance()
            return { type = "identifier", name = tok.value }
        end

        if check("eof") then
            local tok = current()
            addDiagnostic("error", "Expected expression", tok)
            return { type = "unknown", value = tok.value }
        end

        -- Fallback: skip the token and return a generic node
        local tok = advance()
        addDiagnostic("error", "Unexpected token in expression", tok)
        return { type = "unknown", value = tok.value }
    end

    local function buildCallNode(expr, args)
        local callNode = { type = "call", callee = expr, arguments = args }
        if expr.type == "identifier" then
            callNode.name = expr.name
        elseif expr.type == "member_access" then
            callNode.name = expr.member
            callNode.target = expr.object
        end
        return callNode
    end

    local function findGenericCloseIndex(startIndex)
        local depth = 0
        local cursor = startIndex

        while cursor <= #tokens do
            local tok = tokens[cursor]
            if not tok or tok.type == "eof" then
                return nil
            end

            if tok.type == "operator" and tok.value == "<" then
                depth += 1
            elseif tok.type == "operator" and tok.value == ">" then
                depth -= 1
                if depth == 0 then
                    return cursor
                elseif depth < 0 then
                    return nil
                end
            elseif tok.type == "punctuation" and (tok.value == ";" or tok.value == "{" or tok.value == "}") then
                return nil
            end

            cursor += 1
        end

        return nil
    end

    -- Parse postfix: member access (a.b), null-conditional (a?.b), method calls (a()), indexing (a[])
    local function parsePostfix(expr)
        while true do
            -- Null-conditional member access: expr?.member
            if check("operator", "?.") then
                local nullDotToken = advance() -- ?.
                local nextToken = current()
                if nextToken.type == "identifier" and tonumber(nextToken.line) == tonumber(nullDotToken.line) then
                    local memberName = advance().value
                    expr = { type = "null_conditional", object = expr, member = memberName }
                else
                    addDiagnostic("error", "Incomplete member access: expected member name after '?.'", nullDotToken)
                    break
                end
            -- Member access: expr.member
            elseif check("punctuation", ".") then
                local dotToken = advance() -- .
                local nextToken = current()
                if nextToken.type == "identifier" and tonumber(nextToken.line) == tonumber(dotToken.line) then
                    local memberName = advance().value
                    expr = { type = "member_access", object = expr, member = memberName }
                else
                    addDiagnostic("error", "Incomplete member access: expected member name after '.'", dotToken)
                    break
                end
            -- Generic method call: expr<T>(args)
            elseif check("operator", "<") then
                local genericCloseIndex = findGenericCloseIndex(pos)
                local afterGeneric = genericCloseIndex and tokens[genericCloseIndex + 1] or nil
                if not (afterGeneric and afterGeneric.type == "punctuation" and afterGeneric.value == "(") then
                    break
                end

                pos = genericCloseIndex + 1
                local args = parseArguments()
                expr = buildCallNode(expr, args)
            -- Method call: expr(args)
            elseif check("punctuation", "(") then
                local args = parseArguments()
                expr = buildCallNode(expr, args)
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

    -- Parse unary prefix: !, -, ++, --, await
    parseUnary = function()
        if check("keyword", "await") then
            advance() -- await
            local awaitedExpression = parseUnary()
            return { type = "await", expression = awaitedExpression }
        end

        if check("operator", "!") or check("operator", "-") then
            local op = advance().value
            local operand = parseUnary()
            return { type = "unary", operator = op, operand = operand, isPrefix = true }
        end
        if check("operator", "++") or check("operator", "--") then
            local op = advance().value
            local operand = parseUnary()
            return { type = "unary", operator = op, operand = operand, isPrefix = true }
        end
        local expr = parsePrimaryExpression()
        expr = parsePostfix(expr)
        -- Postfix ++ / --
        if check("operator", "++") or check("operator", "--") then
            local op = advance().value
            expr = { type = "unary", operator = op, operand = expr, isPrefix = false }
        end
        return expr
    end

    -- Operator precedence for binary operators
    local PRECEDENCE = {
        ["*"] = 12, ["/"] = 12, ["%"] = 12,
        ["+"] = 11, ["-"] = 11,
        ["<"] = 9, [">"] = 9, ["<="] = 9, [">="] = 9,
        ["is"] = 9, ["as"] = 9,
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
            local prec
            -- Check for operator tokens
            if tok.type == "operator" then
                prec = PRECEDENCE[tok.value]
            -- Check for keyword operators: is, as
            elseif tok.type == "keyword" and (tok.value == "is" or tok.value == "as") then
                prec = PRECEDENCE[tok.value]
            end
            if not prec or prec < minPrec then break end
            local op = advance().value
            -- 'is' and 'as' take a type name on the right, not an expression
            if op == "is" or op == "as" then
                local targetType = parseTypeName()
                left = { type = op, expression = left, targetType = targetType }
            else
                local right = parseBinaryExpression(prec + 1)
                left = { type = "binary", operator = op, left = left, right = right }
            end
        end
        return left
    end

    -- Parse a ternary expression: condition ? thenExpr : elseExpr
    local function parseTernary(minPrec)
        local expr = parseBinaryExpression(minPrec)
        -- Ternary: expr ? a : b
        if check("operator", "?") then
            advance() -- ?
            local thenExpr = parseExpression()
            expect("punctuation", ":")
            local elseExpr = parseExpression()
            return { type = "ternary", condition = expr, thenExpr = thenExpr, elseExpr = elseExpr }
        end
        return expr
    end

    -- Parse a full expression (including ternary and assignment)
    parseExpression = function()
        local expr = parseTernary(1)

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

    local function isValidExpressionStatement(expr)
        if type(expr) ~= "table" then
            return false
        end

        if expr.type == "assignment" or expr.type == "call" or expr.type == "new" or expr.type == "await" then
            return true
        end

        if expr.type == "unary" and (expr.operator == "++" or expr.operator == "--") then
            return true
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
            expect("punctuation", ";")
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
            expect("punctuation", ";")
            return { type = "local_var", name = name, varType = varType, initializer = initializer }
        end

        -- Expression statement (assignment, method call, etc.)
        local expr = parseExpression()
        local statementTerminator = current()
        expect("punctuation", ";")

        if not isValidExpressionStatement(expr) then
            addDiagnostic(
                "error",
                "Invalid expression statement: only assignment, call, increment/decrement, await, and object creation expressions are allowed",
                statementTerminator
            )
        end

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
            local memberStartPos = pos
            local kind, node = parseClassMember(name)

            -- Safety guard: ensure class-body parsing always makes progress.
            -- Unsupported tokens can cause parseClassMember to return without consuming input.
            if pos == memberStartPos then
                addDiagnostic("warning", "Unexpected token in class body: " .. tostring(current().value), current())
                advance()
            else
                if kind == "method" then table.insert(cls.methods, node)
                elseif kind == "field" then table.insert(cls.fields, node)
                elseif kind == "property" then table.insert(cls.properties, node)
                elseif kind == "constructor" then cls.constructor = node
                elseif kind == "event" then table.insert(cls.events, node)
                end
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
            local lastPartToken = expect("identifier")
            table.insert(parts, lastPartToken.value)
            while match("punctuation", ".") do
                lastPartToken = expect("identifier")
                table.insert(parts, lastPartToken.value)
            end
            if not match("punctuation", ";") then
                local anchorToken = {
                    line = lastPartToken.line,
                    column = lastPartToken.column + math.max(1, #tostring(lastPartToken.value or "")),
                    value = ";",
                }
                addDiagnostic("error", "Expected ';' after using directive", anchorToken)
            end
            table.insert(ast.usings, table.concat(parts, "."))

        -- namespace
        elseif check("keyword", "namespace") then
            advance()
            local parts = {}
            local lastPartToken = expect("identifier")
            table.insert(parts, lastPartToken.value)
            while match("punctuation", ".") do
                lastPartToken = expect("identifier")
                table.insert(parts, lastPartToken.value)
            end
            ast.namespace = table.concat(parts, ".")
            if not match("punctuation", "{") then
                local anchorToken = {
                    line = lastPartToken.line,
                    column = lastPartToken.column + math.max(1, #tostring(lastPartToken.value or "")),
                    value = "{",
                }
                addDiagnostic("error", "Expected '{' after namespace declaration", anchorToken)
            end

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

    ast.aborted = parseAborted
    return ast
end

return Parser
