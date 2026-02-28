-- LUSharp Lowerer: Transforms C# AST into Lua IR
-- Input: Parser AST (classes, enums, interfaces, etc.)
-- Output: Lua IR (modules with classes, methods, fields as Lua-compatible tables)

local Lowerer = {}

local TypeDatabase = require("./TypeDatabase")

-- C# type to Lua type mapping
local TYPE_MAP = {
    string = "string",
    int = "number",
    float = "number",
    double = "number",
    decimal = "number",
    long = "number",
    short = "number",
    byte = "number",
    uint = "number",
    ulong = "number",
    ushort = "number",
    bool = "boolean",
    boolean = "boolean",
    void = "nil",
    object = "table",
}

-- C# operator to Lua operator mapping
local OP_MAP = {
    ["!="] = "~=",
    ["&&"] = "and",
    ["||"] = "or",
    ["=="] = "==",
    ["+"] = "+",
    ["-"] = "-",
    ["*"] = "*",
    ["/"] = "/",
    ["%"] = "%",
    ["<"] = "<",
    [">"] = ">",
    ["<="] = "<=",
    [">="] = ">=",
}

-- Unary operator mapping
local UNARY_OP_MAP = {
    ["!"] = "not ",
    ["-"] = "-",
}

-- Collection types that map to plain Lua tables (no wrappers)
local COLLECTION_TYPES = {
    List = "array",
    ["IList"] = "array",
    ["ICollection"] = "array",
    ["IEnumerable"] = "array",
    Queue = "array",
    Stack = "array",
    Dictionary = "dict",
    ["IDictionary"] = "dict",
    HashSet = "set",
}

-- Forward declarations
local lowerExpression
local lowerStatement
local lowerBlock

local needsEventConnectionCache = false
local loweringContext = nil

local function normalizeTypeName(typeName)
    typeName = tostring(typeName or "")
    typeName = typeName:gsub("<.*>", "")
    typeName = typeName:gsub("%?", "")
    typeName = typeName:gsub("%[%]$", "")
    return typeName:match("([%w_]+)$") or typeName
end

local function getCollectionKind(typeName)
    if type(typeName) ~= "string" then
        return nil
    end
    local normalized = normalizeTypeName(typeName)
    return COLLECTION_TYPES[normalized]
end

local function tryUnquoteStringLiteral(astExpr)
    if not astExpr or astExpr.type ~= "literal" or astExpr.literalType ~= "string" then
        return nil
    end

    local v = tostring(astExpr.value or "")
    if #v >= 2 and v:sub(1, 1) == '"' and v:sub(-1) == '"' then
        return v:sub(2, -2)
    end

    if v == '"' then
        return ""
    end

    return nil
end

local function resolveTypeInfo(typeName)
    if type(typeName) ~= "string" or typeName == "" then
        return nil
    end

    local full = (TypeDatabase.aliases and TypeDatabase.aliases[typeName]) or typeName
    return TypeDatabase.types and TypeDatabase.types[full] or nil
end

local function getMemberType(objectTypeName, memberName)
    local typeInfo = resolveTypeInfo(objectTypeName)
    if not typeInfo or type(typeInfo.members) ~= "table" then
        return nil
    end

    for _, member in typeInfo.members do
        if member.name == memberName then
            return member.type
        end
    end

    return nil
end

local BUILTIN_TYPES = {
    game = "DataModel",
    workspace = "Workspace",
    script = "LuaSourceContainer",
    Enum = "Enums",
}

local function inferAstType(astExpr)
    if not astExpr or type(astExpr) ~= "table" then
        return nil
    end

    if astExpr.type == "identifier" then
        return BUILTIN_TYPES[astExpr.name]
    end

    if astExpr.type == "call" and astExpr.target then
        local objectType = inferAstType(astExpr.target)
        if objectType == "DataModel" and astExpr.name == "GetService" then
            local firstArg = astExpr.arguments and astExpr.arguments[1]
            local service = tryUnquoteStringLiteral(firstArg)
            if service and service ~= "" then
                return normalizeTypeName(service)
            end
        end

        if objectType then
            local memberReturn = getMemberType(objectType, astExpr.name)
            if type(memberReturn) == "string" then
                return normalizeTypeName(memberReturn)
            end
        end

        return nil
    end

    if astExpr.type == "member_access" then
        local objectType = inferAstType(astExpr.object)
        if not objectType then
            return nil
        end

        local memberType = getMemberType(objectType, astExpr.member)
        if type(memberType) == "string" then
            return normalizeTypeName(memberType)
        end

        return nil
    end

    return nil
end

local function isSignalType(typeName)
    if type(typeName) ~= "string" then
        return false
    end

    return typeName:sub(1, #"RBXScriptSignal") == "RBXScriptSignal"
end

local function rewriteCollectionCall(methodName, objectExpr, args)
    if methodName == "Add" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end
    if methodName == "Remove" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = {
                objectExpr,
                {
                    type = "call",
                    callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                    args = { objectExpr, args[1] },
                },
            },
        }
    end
    if methodName == "RemoveAt" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = {
                objectExpr,
                { type = "binary_op", op = "+", left = args[1], right = { type = "literal", value = 1 } },
            },
        }
    end
    if methodName == "Insert" and #args == 2 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = {
                objectExpr,
                { type = "binary_op", op = "+", left = args[1], right = { type = "literal", value = 1 } },
                args[2],
            },
        }
    end
    if methodName == "Contains" and #args == 1 then
        return {
            type = "binary_op",
            op = "~=",
            left = {
                type = "call",
                callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                args = { objectExpr, args[1] },
            },
            right = { type = "literal", value = "nil" },
        }
    end
    if methodName == "Clear" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "clear" },
            args = { objectExpr },
        }
    end
    if methodName == "IndexOf" and #args == 1 then
        return {
            type = "binary_op",
            op = "-",
            left = {
                type = "binary_op",
                op = "or",
                left = {
                    type = "call",
                    callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "find" },
                    args = { objectExpr, args[1] },
                },
                right = { type = "literal", value = 0 },
            },
            right = { type = "literal", value = 1 },
        }
    end
    return nil
end

local function rewriteDictCall(methodName, objectExpr, args)
    if methodName == "Add" and #args == 2 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = args[2],
        }
    end
    if methodName == "Remove" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "nil" },
        }
    end
    if methodName == "ContainsKey" and #args == 1 then
        return "expression", {
            type = "binary_op",
            op = "~=",
            left = { type = "index_access", object = objectExpr, key = args[1] },
            right = { type = "literal", value = "nil" },
        }
    end
    return nil, nil
end

local function rewriteSetCall(methodName, objectExpr, args)
    if methodName == "Add" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "true" },
        }
    end
    if methodName == "Remove" and #args == 1 then
        return "assignment", {
            type = "assignment",
            target = { type = "index_access", object = objectExpr, key = args[1] },
            value = { type = "literal", value = "nil" },
        }
    end
    if methodName == "Contains" and #args == 1 then
        return "expression", {
            type = "binary_op",
            op = "~=",
            left = { type = "index_access", object = objectExpr, key = args[1] },
            right = { type = "literal", value = "nil" },
        }
    end
    return nil, nil
end

local function rewriteQueueCall(methodName, objectExpr, args)
    if methodName == "Enqueue" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end
    if methodName == "Dequeue" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = { objectExpr, { type = "literal", value = 1 } },
        }
    end
    if methodName == "Peek" and #args == 0 then
        return { type = "index_access", object = objectExpr, key = { type = "literal", value = 1 } }
    end
    return nil
end

local function rewriteStackCall(methodName, objectExpr, args)
    if methodName == "Push" and #args == 1 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "insert" },
            args = { objectExpr, args[1] },
        }
    end
    if methodName == "Pop" and #args == 0 then
        return {
            type = "call",
            callee = { type = "dot_access", object = { type = "identifier", name = "table" }, field = "remove" },
            args = { objectExpr },
        }
    end
    if methodName == "Peek" and #args == 0 then
        return {
            type = "index_access",
            object = objectExpr,
            key = { type = "unary_op", op = "#", operand = objectExpr },
        }
    end
    return nil
end

-- Lower an expression AST node to IR expression node
lowerExpression = function(expr)
    if not expr then
        return { type = "literal", value = "nil", literalType = "nil" }
    end

    local t = expr.type

    -- Literal values
    if t == "literal" then
        local litType = expr.literalType
        local value = expr.value
        if litType == "null" then
            return { type = "literal", value = "nil", literalType = "nil" }
        elseif litType == "bool" then
            return { type = "literal", value = value, literalType = "boolean" }
        elseif litType == "number" then
            return { type = "literal", value = value, literalType = "number" }
        elseif litType == "string" then
            return { type = "literal", value = value, literalType = "string" }
        end
        return { type = "literal", value = value, literalType = litType }
    end

    -- Identifier reference
    if t == "identifier" then
        return { type = "identifier", name = expr.name }
    end

    -- this → self
    if t == "this" then
        return { type = "identifier", name = "self" }
    end

    -- await expr → just lower the inner expression (async/await not supported, strip it)
    if t == "await" then
        return lowerExpression(expr.expression)
    end

    -- base → self (Lua doesn't have base, handled by method resolution)
    if t == "base" then
        return { type = "identifier", name = "self" }
    end

    -- Binary expression
    if t == "binary" then
        local op = OP_MAP[expr.operator] or expr.operator
        return {
            type = "binary_op",
            left = lowerExpression(expr.left),
            op = op,
            right = lowerExpression(expr.right),
        }
    end

    -- Unary expression
    if t == "unary" then
        -- Preserve ++/-- as explicit unary operations here; statement lowering
        -- handles side-effecting assignment semantics when used as statements.
        if expr.operator == "++" or expr.operator == "--" then
            return {
                type = "incdec_expr",
                operator = expr.operator,
                operand = lowerExpression(expr.operand),
                isPrefix = expr.isPrefix,
            }
        end

        local op = UNARY_OP_MAP[expr.operator] or expr.operator
        return {
            type = "unary_op",
            op = op,
            operand = lowerExpression(expr.operand),
        }
    end

    -- Method/function call
    if t == "call" then
        -- Check for Console.WriteLine → print
        if expr.target and expr.target.type == "identifier" and expr.target.name == "Console"
            and expr.name == "WriteLine" then
            local args = {}
            for _, arg in expr.arguments do
                table.insert(args, lowerExpression(arg))
            end
            return { type = "call", callee = { type = "identifier", name = "print" }, args = args }
        end

        -- Collection method rewrites (before generic method_call)
        if expr.target and expr.name then
            local objectExpr = lowerExpression(expr.target)
            local args = {}
            for _, arg in expr.arguments do
                table.insert(args, lowerExpression(arg))
            end

            local arrayResult = rewriteCollectionCall(expr.name, objectExpr, args)
            if arrayResult then return arrayResult end

            local queueResult = rewriteQueueCall(expr.name, objectExpr, args)
            if queueResult then return queueResult end

            local stackResult = rewriteStackCall(expr.name, objectExpr, args)
            if stackResult then return stackResult end

            local dictKind, dictResult = rewriteDictCall(expr.name, objectExpr, args)
            if dictResult then return dictResult end

            local setKind, setResult = rewriteSetCall(expr.name, objectExpr, args)
            if setResult then return setResult end
        end

        -- Regular method call on an object: obj.method(args)
        if expr.target then
            local args = {}
            for _, arg in expr.arguments do
                table.insert(args, lowerExpression(arg))
            end

            local targetName = expr.target.type == "identifier" and expr.target.name or nil
            local methodName = expr.name

            if loweringContext and targetName then
                -- Check if target is a class name (static access)
                if loweringContext.moduleSymbols[targetName] then
                    local classSym = loweringContext.moduleSymbols[targetName]
                    local methodSym = classSym.methods[methodName]
                    if methodSym then
                        if methodSym.isStatic then
                            return {
                                type = "call",
                                callee = {
                                    type = "dot_access",
                                    object = { type = "identifier", name = targetName },
                                    field = methodName,
                                },
                                args = args,
                            }
                        else
                            table.insert(loweringContext.diagnostics, {
                                severity = "error",
                                code = "CS0120",
                                message = "An object reference is required for the non-static field, method, or property '" .. methodName .. "'",
                                target = targetName,
                                member = methodName,
                            })
                        end
                    else
                        -- Method doesn't exist on this class
                        table.insert(loweringContext.diagnostics, {
                            severity = "error",
                            code = "CS1061",
                            message = "'" .. targetName .. "' does not contain a definition for '" .. methodName .. "'",
                            target = targetName,
                            member = methodName,
                        })
                    end
                -- Check if target is a local variable with known type
                elseif loweringContext.localTypes[targetName] then
                    local varType = loweringContext.localTypes[targetName]
                    local classSym = loweringContext.moduleSymbols[varType]
                    if classSym then
                        local methodSym = classSym.methods[methodName]
                        if methodSym then
                            if methodSym.isStatic then
                                table.insert(loweringContext.diagnostics, {
                                    severity = "error",
                                    code = "CS0176",
                                    message = "Static member '" .. methodName .. "' cannot be accessed with an instance reference; qualify it with a type name instead",
                                    target = targetName,
                                    member = methodName,
                                })
                            else
                                return {
                                    type = "method_call",
                                    object = lowerExpression(expr.target),
                                    method = methodName,
                                    args = args,
                                }
                            end
                        else
                            -- Method doesn't exist on the resolved type
                            table.insert(loweringContext.diagnostics, {
                                severity = "error",
                                code = "CS1061",
                                message = "'" .. varType .. "' does not contain a definition for '" .. methodName .. "'",
                                target = targetName,
                                member = methodName,
                            })
                        end
                    end
                end
            end

            -- Fallback: unknown target type → default to method_call (colon)
            return {
                type = "method_call",
                object = lowerExpression(expr.target),
                method = expr.name or methodName,
                args = args,
            }
        end

        -- Simple function call: name(args)
        local args = {}
        for _, arg in expr.arguments do
            table.insert(args, lowerExpression(arg))
        end
        local callee
        if expr.callee then
            callee = lowerExpression(expr.callee)
        else
            callee = { type = "identifier", name = expr.name or "unknown" }
        end
        return { type = "call", callee = callee, args = args }
    end

    if t == "member_access" then
        if expr.member == "Count" or expr.member == "Length" then
            return {
                type = "unary_op",
                op = "#",
                operand = lowerExpression(expr.object),
            }
        end

        return {
            type = "dot_access",
            object = lowerExpression(expr.object),
            field = expr.member,
        }
    end

    -- Null-conditional: preserve as dedicated IR node for single-evaluation emission
    if t == "null_conditional" then
        return {
            type = "null_conditional",
            object = lowerExpression(expr.object),
            field = expr.member,
        }
    end

    -- new ClassName(args)
    if t == "new" then
        -- Collection types → plain empty table or table with initializer
        local collectionKind = getCollectionKind(expr.className)
        if collectionKind then
            local elements = {}
            for _, item in expr.initializer or {} do
                table.insert(elements, lowerExpression(item))
            end
            return {
                type = "array_literal",
                elements = elements,
            }
        end

        local args = {}
        for _, arg in expr.arguments or {} do
            table.insert(args, lowerExpression(arg))
        end
        for _, item in expr.initializer or {} do
            table.insert(args, lowerExpression(item))
        end
        return {
            type = "new_object",
            class = expr.className,
            args = args,
        }
    end

    -- Interpolated string: $"Hello {name}" → template_string
    if t == "interpolated_string" then
        local raw = expr.value
        -- Strip the $" prefix and trailing "
        local inner = raw
        if string.sub(raw, 1, 2) == '$"' then
            inner = string.sub(raw, 3)
        end
        if string.sub(inner, #inner, #inner) == '"' then
            inner = string.sub(inner, 1, #inner - 1)
        end
        return {
            type = "template_string",
            value = inner,
        }
    end

    -- C# 12 collection expression: [a, b, c] → array literal
    if t == "collection_expression" then
        local elements = {}
        for _, element in expr.elements or {} do
            table.insert(elements, lowerExpression(element))
        end

        return {
            type = "array_literal",
            elements = elements,
        }
    end

    -- Lambda: (x, y) => body
    if t == "lambda" then
        local params = {}
        local defaultGuards = {}

        for _, p in expr.parameters or {} do
            local paramName
            local defaultValue = nil

            if type(p) == "table" then
                paramName = p.name
                defaultValue = p.defaultValue
            else
                paramName = p
            end

            if type(paramName) == "string" and paramName ~= "" then
                table.insert(params, paramName)

                if defaultValue ~= nil then
                    table.insert(defaultGuards, {
                        type = "if_stmt",
                        condition = {
                            type = "binary_op",
                            left = { type = "identifier", name = paramName },
                            op = "==",
                            right = { type = "literal", value = "nil", literalType = "nil" },
                        },
                        body = {
                            {
                                type = "assignment",
                                target = { type = "identifier", name = paramName },
                                value = lowerExpression(defaultValue),
                            },
                        },
                        elseBody = nil,
                    })
                end
            end
        end

        local body
        if type(expr.body) == "table" and expr.body.type then
            -- Single expression body → wrap in return
            body = { { type = "return_stmt", value = lowerExpression(expr.body) } }
        else
            -- Block body
            body = lowerBlock(expr.body)
        end

        if #defaultGuards > 0 then
            local guardedBody = {}
            for _, guard in defaultGuards do
                table.insert(guardedBody, guard)
            end
            for _, stmt in body do
                table.insert(guardedBody, stmt)
            end
            body = guardedBody
        end

        return {
            type = "function_expr",
            params = params,
            body = body,
        }
    end

    -- Assignment expression
    if t == "assignment" then
        return {
            type = "assignment",
            target = lowerExpression(expr.target),
            value = lowerExpression(expr.value),
        }
    end

    -- Index access: obj[key]
    if t == "index" then
        return {
            type = "index_access",
            object = lowerExpression(expr.object),
            key = lowerExpression(expr.index),
        }
    end

    -- Ternary: a ? b : c → if a then b else c (represented as expression)
    if t == "ternary" then
        return {
            type = "ternary",
            condition = lowerExpression(expr.condition),
            thenExpr = lowerExpression(expr.thenExpr),
            elseExpr = lowerExpression(expr.elseExpr),
        }
    end

    -- Cast: (Type)expr → just the expression (Lua is dynamically typed)
    if t == "cast" then
        return lowerExpression(expr.expression)
    end

    -- typeof → type() call
    if t == "typeof" then
        return {
            type = "call",
            callee = { type = "identifier", name = "typeof" },
            args = { { type = "literal", value = '"' .. (expr.targetType or "unknown") .. '"', literalType = "string" } },
        }
    end

    -- 'is' type check
    if t == "is" then
        local mappedTargetType = TYPE_MAP[expr.targetType] or expr.targetType or "unknown"
        return {
            type = "binary_op",
            left = {
                type = "call",
                callee = { type = "identifier", name = "typeof" },
                args = { lowerExpression(expr.expression) },
            },
            op = "==",
            right = { type = "literal", value = '"' .. mappedTargetType .. '"', literalType = "string" },
        }
    end

    -- 'as' type cast (just pass through)
    if t == "as" then
        return lowerExpression(expr.expression)
    end

    -- Unknown expression type: wrap as identifier
    return { type = "identifier", name = expr.value or "unknown" }
end

-- Lower a field initializer (which may be raw tokens from the Parser)
local function lowerFieldInitializer(initializer)
    if not initializer then return nil end
    -- If initializer is a table of tokens (Parser stores field/property initializers as token arrays)
    if #initializer > 0 and initializer[1].type then
        -- Check if it's a token array (has .type and .value on first element in token style)
        local first = initializer[1]
        if first.line ~= nil then
            -- It's a token array: extract simple values
            if #initializer == 1 then
                local tok = initializer[1]
                if tok.type == "number" then
                    return { type = "literal", value = tok.value, literalType = "number" }
                elseif tok.type == "string" then
                    return { type = "literal", value = tok.value, literalType = "string" }
                elseif tok.type == "keyword" and (tok.value == "true" or tok.value == "false") then
                    return { type = "literal", value = tok.value, literalType = "boolean" }
                elseif tok.type == "keyword" and tok.value == "null" then
                    return { type = "literal", value = "nil", literalType = "nil" }
                elseif tok.type == "identifier" then
                    return { type = "identifier", name = tok.value }
                end
            end
            -- Multi-token initializer: concatenate values as a simple representation
            local parts = {}
            for _, tok in initializer do
                table.insert(parts, tok.value)
            end
            return { type = "literal", value = table.concat(parts, " "), literalType = "unknown" }
        end
    end
    -- If it's already an expression AST node, lower it
    if initializer.type then
        return lowerExpression(initializer)
    end
    return nil
end

-- Lower a statement AST node to IR statement node
lowerStatement = function(stmt)
    if not stmt then return nil end

    local t = stmt.type

    -- Local variable declaration
    if t == "local_var" then
        local init = stmt.initializer
        if init and init.type == "new" and init.className == nil and stmt.varType and stmt.varType ~= "var" then
            init = {
                type = "new",
                className = normalizeTypeName(stmt.varType),
                arguments = init.arguments,
                initializer = init.initializer,
            }
        end

        -- Track variable type for call resolution
        if loweringContext then
            if stmt.varType and stmt.varType ~= "var" then
                loweringContext.localTypes[stmt.name] = normalizeTypeName(stmt.varType)
            elseif stmt.varType == "var" and init and init.type == "new" and init.className then
                loweringContext.localTypes[stmt.name] = normalizeTypeName(init.className)
            end
        end

        return {
            type = "local_decl",
            name = stmt.name,
            value = init and lowerExpression(init) or nil,
        }
    end

    -- Expression statement
    if t == "expression_statement" then
        -- Check for assignment expressions inside
        if stmt.expression and stmt.expression.type == "assignment" then
            local op = stmt.expression.operator
            local target = lowerExpression(stmt.expression.target)
            local value
            if op == "+=" or op == "-=" then
                local targetType = inferAstType(stmt.expression.target)
                if isSignalType(targetType) then
                    needsEventConnectionCache = true

                    local signalExpr = lowerExpression(stmt.expression.target)
                    local handlerExpr = lowerExpression(stmt.expression.value)

                    local sigVar = "__sig"
                    local mapVar = "__map"
                    local connVar = "__conn"

                    if op == "+=" then
                        return {
                            type = "compound",
                            statements = {
                                { type = "local_decl", name = sigVar, value = signalExpr },
                                {
                                    type = "local_decl",
                                    name = mapVar,
                                    value = {
                                        type = "binary_op",
                                        left = { type = "index_access", object = { type = "identifier", name = "__eventConnections" }, key = { type = "identifier", name = sigVar } },
                                        op = "or",
                                        right = { type = "literal", value = "{}", literalType = "table" },
                                    },
                                },
                                {
                                    type = "local_decl",
                                    name = connVar,
                                    value = { type = "method_call", object = { type = "identifier", name = sigVar }, method = "Connect", args = { handlerExpr } },
                                },
                                {
                                    type = "assignment",
                                    target = { type = "index_access", object = { type = "identifier", name = mapVar }, key = handlerExpr },
                                    value = { type = "identifier", name = connVar },
                                },
                                {
                                    type = "assignment",
                                    target = { type = "index_access", object = { type = "identifier", name = "__eventConnections" }, key = { type = "identifier", name = sigVar } },
                                    value = { type = "identifier", name = mapVar },
                                },
                            },
                        }
                    else
                        return {
                            type = "compound",
                            statements = {
                                { type = "local_decl", name = sigVar, value = signalExpr },
                                {
                                    type = "local_decl",
                                    name = mapVar,
                                    value = { type = "index_access", object = { type = "identifier", name = "__eventConnections" }, key = { type = "identifier", name = sigVar } },
                                },
                                {
                                    type = "if_stmt",
                                    condition = { type = "identifier", name = mapVar },
                                    body = {
                                        {
                                            type = "local_decl",
                                            name = connVar,
                                            value = { type = "index_access", object = { type = "identifier", name = mapVar }, key = handlerExpr },
                                        },
                                        {
                                            type = "if_stmt",
                                            condition = { type = "identifier", name = connVar },
                                            body = {
                                                { type = "expr_statement", expression = { type = "method_call", object = { type = "identifier", name = connVar }, method = "Disconnect", args = {} } },
                                                {
                                                    type = "assignment",
                                                    target = { type = "index_access", object = { type = "identifier", name = mapVar }, key = handlerExpr },
                                                    value = { type = "literal", value = "nil", literalType = "nil" },
                                                },
                                            },
                                            elseBody = nil,
                                        },
                                    },
                                    elseBody = nil,
                                },
                            },
                        }
                    end
                end
            end

            local value
            if op == "=" then
                value = lowerExpression(stmt.expression.value)
            elseif op == "+=" then
                value = {
                    type = "binary_op",
                    left = target,
                    op = "+",
                    right = lowerExpression(stmt.expression.value),
                }
            elseif op == "-=" then
                value = {
                    type = "binary_op",
                    left = target,
                    op = "-",
                    right = lowerExpression(stmt.expression.value),
                }
            elseif op == "*=" then
                value = {
                    type = "binary_op",
                    left = target,
                    op = "*",
                    right = lowerExpression(stmt.expression.value),
                }
            elseif op == "/=" then
                value = {
                    type = "binary_op",
                    left = target,
                    op = "/",
                    right = lowerExpression(stmt.expression.value),
                }
            elseif op == "%=" then
                value = {
                    type = "binary_op",
                    left = target,
                    op = "%",
                    right = lowerExpression(stmt.expression.value),
                }
            else
                value = lowerExpression(stmt.expression.value)
            end
            return {
                type = "assignment",
                target = target,
                value = value,
            }
        end

        -- Handle increment/decrement statements as assignments with side effects
        if stmt.expression and stmt.expression.type == "unary"
            and (stmt.expression.operator == "++" or stmt.expression.operator == "--") then
            local target = lowerExpression(stmt.expression.operand)
            return {
                type = "assignment",
                target = target,
                value = {
                    type = "binary_op",
                    left = target,
                    op = stmt.expression.operator == "++" and "+" or "-",
                    right = { type = "literal", value = "1", literalType = "number" },
                },
            }
        end

        return {
            type = "expr_statement",
            expression = lowerExpression(stmt.expression),
        }
    end

    -- If statement
    if t == "if" then
        local elseBody = nil
        -- Handle else-ifs by nesting
        if stmt.elseIfs and #stmt.elseIfs > 0 then
            -- Build nested if chain from else-ifs
            local current = nil
            for i = #stmt.elseIfs, 1, -1 do
                local elif = stmt.elseIfs[i]
                local nextElse = current or (stmt.elseBody and lowerBlock(stmt.elseBody) or nil)
                current = {
                    {
                        type = "if_stmt",
                        condition = lowerExpression(elif.condition),
                        body = lowerBlock(elif.body),
                        elseBody = nextElse,
                    }
                }
            end
            elseBody = current
        elseif stmt.elseBody then
            elseBody = lowerBlock(stmt.elseBody)
        end
        return {
            type = "if_stmt",
            condition = lowerExpression(stmt.condition),
            body = lowerBlock(stmt.body),
            elseBody = elseBody,
        }
    end

    -- For loop (C-style → numeric for when possible, otherwise while)
    if t == "for" then
        -- Try to detect simple numeric for: for (int i = 0; i < N; i++)
        local init = stmt.init
        local cond = stmt.condition
        local incr = stmt.increment

        -- Check if it's a simple numeric pattern
        local isNumeric = false
        local varName, startVal, stopVal, stepVal

        if init and init.type == "local_var" and init.initializer
            and cond and cond.type == "binary" and (cond.operator == "<" or cond.operator == "<=")
            and cond.left and cond.left.type == "identifier" and cond.left.name == init.name
            and incr and incr.type == "unary" and incr.operator == "++"
            and incr.operand and incr.operand.type == "identifier" and incr.operand.name == init.name then
            varName = init.name
            startVal = lowerExpression(init.initializer)
            stopVal = lowerExpression(cond.right)
            -- Adjust for < vs <=
            if cond.operator == "<" then
                stopVal = {
                    type = "binary_op",
                    left = stopVal,
                    op = "-",
                    right = { type = "literal", value = "1", literalType = "number" },
                }
            end
            stepVal = { type = "literal", value = "1", literalType = "number" }
            isNumeric = true
        end

        if isNumeric then
            return {
                type = "for_numeric",
                var = varName,
                start = startVal,
                stop = stopVal,
                step = stepVal,
                body = lowerBlock(stmt.body),
            }
        end

        -- Fallback: convert to while loop with optional initializer prelude
        local body = lowerBlock(stmt.body)
        if incr then
            local incStmt = lowerStatement({ type = "expression_statement", expression = incr })
            if incStmt then
                table.insert(body, incStmt)
            end
        end

        local whileNode = {
            type = "while_stmt",
            condition = cond and lowerExpression(cond) or { type = "literal", value = "true", literalType = "boolean" },
            body = body,
        }

        if not init then
            return whileNode
        end

        local initNode
        if init.type == "local_var" then
            initNode = lowerStatement(init)
        else
            initNode = lowerStatement({ type = "expression_statement", expression = init })
        end

        return {
            type = "compound",
            statements = { initNode, whileNode },
        }
    end

    -- Foreach loop → for_in
    if t == "foreach" then
        return {
            type = "for_in",
            vars = { "_", stmt.variable },
            iterator = lowerExpression(stmt.iterable),
            body = lowerBlock(stmt.body),
        }
    end

    -- While loop
    if t == "while" then
        return {
            type = "while_stmt",
            condition = lowerExpression(stmt.condition),
            body = lowerBlock(stmt.body),
        }
    end

    -- Do-while → repeat-until (Luau's equivalent)
    if t == "do_while" then
        return {
            type = "repeat_until",
            condition = {
                type = "unary_op",
                op = "not ",
                operand = lowerExpression(stmt.condition),
            },
            body = lowerBlock(stmt.body),
        }
    end

    -- Return
    if t == "return" then
        return {
            type = "return_stmt",
            value = stmt.value and lowerExpression(stmt.value) or nil,
        }
    end

    -- Break
    if t == "break" then
        return { type = "break_stmt" }
    end

    -- Continue
    if t == "continue" then
        return { type = "continue_stmt" }
    end

    -- Try/catch → pcall wrapping
    if t == "try_catch" then
        local tryBody = lowerBlock(stmt.tryBody or {})
        local catchBody = lowerBlock(stmt.catchBody or {})
        return {
            type = "try_catch",
            tryBody = tryBody,
            catchVar = stmt.catchVar,
            catchBody = catchBody,
            finallyBody = stmt.finallyBody and lowerBlock(stmt.finallyBody) or nil,
        }
    end

    -- Throw → error()
    if t == "throw" then
        return {
            type = "expr_statement",
            expression = {
                type = "call",
                callee = { type = "identifier", name = "error" },
                args = stmt.expression and { lowerExpression(stmt.expression) } or {},
            },
        }
    end

    -- Switch → evaluate once, then if/elseif chain
    if t == "switch" then
        local switchExpr = lowerExpression(stmt.expression)
        local switchTempName = "__switchValue"
        local switchTempDecl = {
            type = "local_decl",
            name = switchTempName,
            value = switchExpr,
        }

        local cases = stmt.cases or {}
        if #cases == 0 then
            return {
                type = "compound",
                statements = {
                    switchTempDecl,
                    { type = "expr_statement", expression = { type = "identifier", name = switchTempName } },
                },
            }
        end

        local function stripSwitchBreak(body)
            if body and #body > 0 and body[#body].type == "break_stmt" then
                table.remove(body, #body)
            end
            return body
        end

        -- Build if/elseif chain
        local firstCase = nil
        local lastIf = nil
        for _, case in cases do
            if case.type == "case" then
                local caseBody = stripSwitchBreak(lowerBlock(case.body))
                local cond = {
                    type = "binary_op",
                    left = { type = "identifier", name = switchTempName },
                    op = "==",
                    right = lowerExpression(case.value),
                }
                local ifNode = {
                    type = "if_stmt",
                    condition = cond,
                    body = caseBody,
                    elseBody = nil,
                }
                if not firstCase then
                    firstCase = ifNode
                else
                    lastIf.elseBody = { ifNode }
                end
                lastIf = ifNode
            elseif case.type == "default" then
                local defaultBody = stripSwitchBreak(lowerBlock(case.body))
                if lastIf then
                    lastIf.elseBody = defaultBody
                else
                    -- Only default case, no regular cases
                    return {
                        type = "compound",
                        statements = { switchTempDecl, table.unpack(defaultBody) },
                    }
                end
            end
        end

        return {
            type = "compound",
            statements = {
                switchTempDecl,
                firstCase or { type = "expr_statement", expression = { type = "identifier", name = switchTempName } },
            },
        }
    end

    -- Fallback: wrap as expression statement
    return {
        type = "expr_statement",
        expression = { type = "identifier", name = "unknown" },
    }
end

-- Lower a block (array of statements)
lowerBlock = function(stmts)
    if not stmts then return {} end
    local result = {}
    for _, stmt in stmts do
        local lowered = lowerStatement(stmt)
        if lowered then
            if lowered.type == "compound" and lowered.statements then
                for _, inner in lowered.statements do
                    table.insert(result, inner)
                end
            else
                table.insert(result, lowered)
            end
        end
    end
    return result
end

local function cloneSet(set)
    local out = {}
    for k, v in set or {} do
        if v then
            out[k] = true
        end
    end
    return out
end

local function addListToSet(set, list)
    for _, name in list or {} do
        set[name] = true
    end
end

local rewriteSelfCallsInBlock

local function rewriteSelfCallsInExpression(expr, ctx, locals)
    if not expr then
        return expr
    end

    local t = expr.type

    if t == "call" then
        expr.callee = rewriteSelfCallsInExpression(expr.callee, ctx, locals)
        for i, arg in expr.args or {} do
            expr.args[i] = rewriteSelfCallsInExpression(arg, ctx, locals)
        end

        if expr.callee and expr.callee.type == "identifier" then
            local name = expr.callee.name
            if name and not locals[name] then
                if (not ctx.isStatic) and ctx.instanceMethods[name] then
                    return {
                        type = "method_call",
                        object = { type = "identifier", name = "self" },
                        method = name,
                        args = expr.args,
                    }
                end

                if ctx.staticMethods[name] then
                    return {
                        type = "call",
                        callee = {
                            type = "dot_access",
                            object = { type = "identifier", name = ctx.className },
                            field = name,
                        },
                        args = expr.args,
                    }
                end
            end
        end

        return expr
    end

    if t == "method_call" then
        expr.object = rewriteSelfCallsInExpression(expr.object, ctx, locals)
        for i, arg in expr.args or {} do
            expr.args[i] = rewriteSelfCallsInExpression(arg, ctx, locals)
        end
        return expr
    end

    if t == "dot_access" then
        expr.object = rewriteSelfCallsInExpression(expr.object, ctx, locals)
        return expr
    end

    if t == "index_access" then
        expr.object = rewriteSelfCallsInExpression(expr.object, ctx, locals)
        expr.key = rewriteSelfCallsInExpression(expr.key, ctx, locals)
        return expr
    end

    if t == "binary_op" then
        expr.left = rewriteSelfCallsInExpression(expr.left, ctx, locals)
        expr.right = rewriteSelfCallsInExpression(expr.right, ctx, locals)
        return expr
    end

    if t == "unary_op" then
        expr.operand = rewriteSelfCallsInExpression(expr.operand, ctx, locals)
        return expr
    end

    if t == "ternary" then
        expr.condition = rewriteSelfCallsInExpression(expr.condition, ctx, locals)
        expr.thenExpr = rewriteSelfCallsInExpression(expr.thenExpr, ctx, locals)
        expr.elseExpr = rewriteSelfCallsInExpression(expr.elseExpr, ctx, locals)
        return expr
    end

    if t == "null_conditional" then
        expr.object = rewriteSelfCallsInExpression(expr.object, ctx, locals)
        return expr
    end

    if t == "incdec_expr" then
        expr.operand = rewriteSelfCallsInExpression(expr.operand, ctx, locals)
        return expr
    end

    if t == "new_object" then
        for i, arg in expr.args or {} do
            expr.args[i] = rewriteSelfCallsInExpression(arg, ctx, locals)
        end
        return expr
    end

    if t == "function_expr" then
        local innerLocals = cloneSet(locals)
        addListToSet(innerLocals, expr.params)
        rewriteSelfCallsInBlock(expr.body or {}, ctx, innerLocals)
        return expr
    end

    return expr
end

rewriteSelfCallsInBlock = function(stmts, ctx, locals)
    locals = locals or {}

    for _, stmt in stmts or {} do
        local t = stmt.type

        if t == "local_decl" then
            if stmt.value then
                stmt.value = rewriteSelfCallsInExpression(stmt.value, ctx, locals)
            end
            locals[stmt.name] = true
        elseif t == "assignment" then
            stmt.target = rewriteSelfCallsInExpression(stmt.target, ctx, locals)
            stmt.value = rewriteSelfCallsInExpression(stmt.value, ctx, locals)
        elseif t == "expr_statement" then
            stmt.expression = rewriteSelfCallsInExpression(stmt.expression, ctx, locals)
        elseif t == "if_stmt" then
            stmt.condition = rewriteSelfCallsInExpression(stmt.condition, ctx, locals)
            rewriteSelfCallsInBlock(stmt.body or {}, ctx, cloneSet(locals))
            if stmt.elseBody then
                rewriteSelfCallsInBlock(stmt.elseBody, ctx, cloneSet(locals))
            end
        elseif t == "for_numeric" then
            stmt.start = rewriteSelfCallsInExpression(stmt.start, ctx, locals)
            stmt.stop = rewriteSelfCallsInExpression(stmt.stop, ctx, locals)
            stmt.step = rewriteSelfCallsInExpression(stmt.step, ctx, locals)
            local innerLocals = cloneSet(locals)
            innerLocals[stmt.var] = true
            rewriteSelfCallsInBlock(stmt.body or {}, ctx, innerLocals)
        elseif t == "for_in" then
            stmt.iterator = rewriteSelfCallsInExpression(stmt.iterator, ctx, locals)
            local innerLocals = cloneSet(locals)
            addListToSet(innerLocals, stmt.vars)
            rewriteSelfCallsInBlock(stmt.body or {}, ctx, innerLocals)
        elseif t == "while_stmt" then
            stmt.condition = rewriteSelfCallsInExpression(stmt.condition, ctx, locals)
            rewriteSelfCallsInBlock(stmt.body or {}, ctx, cloneSet(locals))
        elseif t == "repeat_until" then
            rewriteSelfCallsInBlock(stmt.body or {}, ctx, cloneSet(locals))
            stmt.condition = rewriteSelfCallsInExpression(stmt.condition, ctx, locals)
        elseif t == "return_stmt" then
            if stmt.value then
                stmt.value = rewriteSelfCallsInExpression(stmt.value, ctx, locals)
            end
        elseif t == "try_catch" then
            rewriteSelfCallsInBlock(stmt.tryBody or {}, ctx, cloneSet(locals))
            local catchLocals = cloneSet(locals)
            if stmt.catchVar then
                catchLocals[stmt.catchVar] = true
            end
            rewriteSelfCallsInBlock(stmt.catchBody or {}, ctx, catchLocals)
            if stmt.finallyBody then
                rewriteSelfCallsInBlock(stmt.finallyBody or {}, ctx, cloneSet(locals))
            end
        elseif t == "compound" then
            rewriteSelfCallsInBlock(stmt.statements or {}, ctx, locals)
        end
    end
end

-- Lower a class AST node to IR class
local function lowerClass(classNode)
    local cls = {
        name = classNode.name,
        baseClass = classNode.baseClass,
        staticFields = {},
        instanceFields = {},
        methods = {},
        constructor = nil,
        properties = {},
    }

    -- Lower fields
    for _, field in classNode.fields or {} do
        local irField = {
            name = field.name,
            value = lowerFieldInitializer(field.initializer),
        }
        if field.isStatic then
            table.insert(cls.staticFields, irField)
        else
            table.insert(cls.instanceFields, irField)
        end

        if loweringContext and field.fieldType then
            local normalizedType = normalizeTypeName(field.fieldType)
            if normalizedType and normalizedType ~= "" then
                loweringContext.localTypes[field.name] = normalizedType
            end
        end
    end

    -- Primary constructor parameters become captured instance fields.
    for _, parameter in classNode.primaryConstructorParameters or {} do
        if type(parameter) == "table" and type(parameter.name) == "string" and parameter.name ~= "" then
            local exists = false
            for _, existingField in cls.instanceFields do
                if existingField.name == parameter.name then
                    exists = true
                    break
                end
            end

            if not exists then
                table.insert(cls.instanceFields, {
                    name = parameter.name,
                    value = { type = "identifier", name = parameter.name },
                })
            end
        end
    end

    local instanceMethodNames = {}
    local staticMethodNames = {}
    for _, m in classNode.methods or {} do
        if m.isStatic then
            staticMethodNames[m.name] = true
        else
            instanceMethodNames[m.name] = true
        end
    end

    -- Lower methods
    for _, method in classNode.methods or {} do
        if loweringContext then
            loweringContext.localTypes = {}
            -- Re-track fields for this method's scope
            for _, field in classNode.fields or {} do
                if field.fieldType then
                    local normalizedType = normalizeTypeName(field.fieldType)
                    if normalizedType and normalizedType ~= "" then
                        loweringContext.localTypes[field.name] = normalizedType
                    end
                end
            end
        end

        local params = {}
        for _, p in method.parameters or {} do
            table.insert(params, p.name)
        end
        local irMethod = {
            name = method.name,
            isStatic = method.isStatic or false,
            params = params,
            body = lowerBlock(method.body),
        }

        local locals = {}
        addListToSet(locals, params)
        rewriteSelfCallsInBlock(irMethod.body, {
            className = classNode.name,
            isStatic = irMethod.isStatic,
            instanceMethods = instanceMethodNames,
            staticMethods = staticMethodNames,
        }, locals)

        table.insert(cls.methods, irMethod)
    end

    -- Lower constructor
    if classNode.constructor then
        local params = {}
        for _, p in classNode.constructor.parameters or {} do
            table.insert(params, p.name)
        end
        cls.constructor = {
            params = params,
            body = lowerBlock(classNode.constructor.body),
        }

        local locals = {}
        addListToSet(locals, params)
        rewriteSelfCallsInBlock(cls.constructor.body, {
            className = classNode.name,
            isStatic = false,
            instanceMethods = instanceMethodNames,
            staticMethods = staticMethodNames,
        }, locals)
    end

    -- Lower properties
    for _, prop in classNode.properties or {} do
        table.insert(cls.properties, {
            name = prop.name,
            hasGet = prop.hasGet or false,
            hasSet = prop.hasSet or false,
            initializer = lowerFieldInitializer(prop.initializer),
        })
    end

    return cls
end

-- Lower an enum AST node to IR enum
local function lowerEnum(enumNode)
    local values = {}
    for i, v in enumNode.values or {} do
        table.insert(values, {
            name = v.name,
            value = v.value or tostring(i - 1),
        })
    end
    return {
        name = enumNode.name,
        values = values,
    }
end

-- Find the entry class (one with Main method)
local function findEntryClass(classNodes)
    for _, cls in classNodes do
        for _, method in cls.methods or {} do
            if method.name == "Main" then
                return cls.name
            end
        end
    end
    return nil
end

-- Collect all game:GetService("X") calls from IR nodes
local function collectServiceCallsFromExpr(node, found)
    if type(node) ~= "table" then return end

    -- Match: game:GetService("ServiceName") or game.GetService("ServiceName")
    if node.type == "method_call" and node.method == "GetService" then
        if node.object and node.object.type == "identifier" and node.object.name == "game" then
            if node.args and #node.args >= 1 then
                local firstArg = node.args[1]
                if firstArg.type == "literal" and type(firstArg.value) == "string" then
                    local serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
                    if serviceName ~= "" then
                        found[serviceName] = true
                    end
                end
            end
        end
    elseif node.type == "call" and node.callee then
        if node.callee.type == "dot_access" and node.callee.field == "GetService" then
            if node.callee.object and node.callee.object.type == "identifier" and node.callee.object.name == "game" then
                if node.args and #node.args >= 1 then
                    local firstArg = node.args[1]
                    if firstArg.type == "literal" and type(firstArg.value) == "string" then
                        local serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
                        if serviceName ~= "" then
                            found[serviceName] = true
                        end
                    end
                end
            end
        end
    end

    -- Recurse into all table children
    for k, v in node do
        if type(v) == "table" then
            collectServiceCallsFromExpr(v, found)
        end
    end
end

-- Replace game:GetService("X") nodes in IR with identifier "X"
local function replaceServiceCalls(node)
    if type(node) ~= "table" then return node end

    -- Check if this node is a GetService call
    local serviceName = nil

    if node.type == "method_call" and node.method == "GetService" then
        if node.object and node.object.type == "identifier" and node.object.name == "game" then
            if node.args and #node.args >= 1 then
                local firstArg = node.args[1]
                if firstArg.type == "literal" and type(firstArg.value) == "string" then
                    serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
                end
            end
        end
    elseif node.type == "call" and node.callee then
        if node.callee.type == "dot_access" and node.callee.field == "GetService" then
            if node.callee.object and node.callee.object.type == "identifier" and node.callee.object.name == "game" then
                if node.args and #node.args >= 1 then
                    local firstArg = node.args[1]
                    if firstArg.type == "literal" and type(firstArg.value) == "string" then
                        serviceName = firstArg.value:gsub('^"', ""):gsub('"$', "")
                    end
                end
            end
        end
    end

    if serviceName and serviceName ~= "" then
        return { type = "identifier", name = serviceName }
    end

    -- Recurse
    for k, v in node do
        if type(v) == "table" then
            node[k] = replaceServiceCalls(v)
        end
    end
    return node
end

-- Collect all identifiers referenced in IR
local function collectReferencedIdentifiers(node, found)
    if type(node) ~= "table" then return end
    found = found or {}

    if node.type == "identifier" and node.name then
        found[node.name] = true
    end
    if node.type == "new_object" and node.class then
        local className = tostring(node.class):gsub("<.*>", ""):match("([%w_]+)$") or ""
        if className ~= "" then
            found[className] = true
        end
    end

    for k, v in node do
        if type(v) == "table" then
            collectReferencedIdentifiers(v, found)
        end
    end
    return found
end

local function buildModuleSymbols(classNodes)
    local symbols = {}
    for _, cls in classNodes or {} do
        local classSym = { methods = {}, fields = {} }
        for _, method in cls.methods or {} do
            classSym.methods[method.name] = { isStatic = method.isStatic or false }
        end
        for _, field in cls.fields or {} do
            classSym.fields[field.name] = { isStatic = field.isStatic or false, fieldType = field.fieldType }
        end
        if cls.constructor then
            classSym.methods["new"] = { isStatic = true }
        end
        symbols[cls.name] = classSym
    end
    return symbols
end

-- Main lowering function: AST → IR
function Lowerer.lower(ast)
    local moduleSymbols = buildModuleSymbols(ast.classes)
    loweringContext = {
        moduleSymbols = moduleSymbols,
        localTypes = {},
        diagnostics = {},
    }

    local ir = {
        modules = {},
    }

    -- Create a single module from the AST
    local module = {
        scriptType = "ModuleScript",
        classes = {},
        enums = {},
        entryClass = nil,
        needsEventConnectionCache = needsEventConnectionCache,
    }

    -- Lower classes
    for _, classNode in ast.classes or {} do
        table.insert(module.classes, lowerClass(classNode))
    end

    -- Lower enums
    for _, enumNode in ast.enums or {} do
        table.insert(module.enums, lowerEnum(enumNode))
    end

    -- Find entry class
    module.entryClass = findEntryClass(ast.classes or {})

    -- Service hoisting pass
    local serviceSet = {}
    for _, cls in module.classes do
        for _, method in cls.methods or {} do
            collectServiceCallsFromExpr(method, serviceSet)
        end
        if cls.constructor then
            collectServiceCallsFromExpr(cls.constructor, serviceSet)
        end
    end

    -- Build sorted service list for IR
    local services = {}
    for name in serviceSet do
        table.insert(services, { name = name })
    end
    table.sort(services, function(a, b) return a.name < b.name end)
    module.services = services

    -- Replace GetService calls with identifiers
    for _, cls in module.classes do
        for _, method in cls.methods or {} do
            replaceServiceCalls(method)
        end
        if cls.constructor then
            replaceServiceCalls(cls.constructor)
        end
    end

    -- Require generation pass
    local localNames = {}
    for _, cls in module.classes do
        localNames[cls.name] = true
    end
    for _, enum in module.enums or {} do
        localNames[enum.name] = true
    end

    local referenced = {}
    for _, cls in module.classes do
        collectReferencedIdentifiers(cls, referenced)
    end

    local requires = {}
    for name in referenced do
        if not localNames[name] and name:sub(1, 1):match("[A-Z]") then
            if not serviceSet[name] and name ~= "game" and name ~= "script"
                and name ~= "workspace" and name ~= "print" and name ~= "warn"
                and name ~= "error" and name ~= "type" and name ~= "typeof"
                and name ~= "tostring" and name ~= "tonumber" and name ~= "pcall"
                and name ~= "table" and name ~= "string" and name ~= "math"
                and name ~= "Instance" and name ~= "Enum" and name ~= "Vector3"
                and name ~= "CFrame" and name ~= "Color3" and name ~= "UDim2"
                and name ~= "UDim" and name ~= "BrickColor" and name ~= "Ray"
                and name ~= "Region3" and name ~= "TweenInfo" and name ~= "task"
                and name ~= "coroutine" and name ~= "setmetatable" then
                table.insert(requires, { name = name })
            end
        end
    end
    table.sort(requires, function(a, b) return a.name < b.name end)
    module.requires = requires

    module.needsEventConnectionCache = needsEventConnectionCache

    table.insert(ir.modules, module)

    ir.diagnostics = loweringContext.diagnostics
    loweringContext = nil

    return ir
end

return Lowerer
