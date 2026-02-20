-- LUSharp Lowerer: Transforms C# AST into Lua IR
-- Input: Parser AST (classes, enums, interfaces, etc.)
-- Output: Lua IR (modules with classes, methods, fields as Lua-compatible tables)

local Lowerer = {}

-- Script type determination from base class
local SCRIPT_BASES = {
    RobloxScript = "Script",
    Script = "Script",
    LocalScript = "LocalScript",
    ModuleScript = "ModuleScript",
}

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

-- Forward declarations
local lowerExpression
local lowerStatement
local lowerBlock

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
            for _, arg in ipairs(expr.arguments) do
                table.insert(args, lowerExpression(arg))
            end
            return { type = "call", callee = { type = "identifier", name = "print" }, args = args }
        end

        -- Regular method call on an object: obj.method(args)
        if expr.target then
            local args = {}
            for _, arg in ipairs(expr.arguments) do
                table.insert(args, lowerExpression(arg))
            end
            return {
                type = "method_call",
                object = lowerExpression(expr.target),
                method = expr.name,
                args = args,
            }
        end

        -- Simple function call: name(args)
        local args = {}
        for _, arg in ipairs(expr.arguments) do
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

    -- Member access: obj.field
    if t == "member_access" then
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
        local args = {}
        for _, arg in ipairs(expr.arguments or {}) do
            table.insert(args, lowerExpression(arg))
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

    -- Lambda: (x, y) => body
    if t == "lambda" then
        local params = {}
        for _, p in ipairs(expr.parameters or {}) do
            table.insert(params, p)
        end
        local body
        if type(expr.body) == "table" and expr.body.type then
            -- Single expression body → wrap in return
            body = { { type = "return_stmt", value = lowerExpression(expr.body) } }
        else
            -- Block body
            body = lowerBlock(expr.body)
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
            for _, tok in ipairs(initializer) do
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
        return {
            type = "local_decl",
            name = stmt.name,
            value = stmt.initializer and lowerExpression(stmt.initializer) or nil,
        }
    end

    -- Expression statement
    if t == "expression_statement" then
        -- Check for assignment expressions inside
        if stmt.expression and stmt.expression.type == "assignment" then
            local op = stmt.expression.operator
            local target = lowerExpression(stmt.expression.target)
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
            iterator = {
                type = "call",
                callee = { type = "identifier", name = "pairs" },
                args = { lowerExpression(stmt.iterable) },
            },
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
        for _, case in ipairs(cases) do
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
    for _, stmt in ipairs(stmts) do
        local lowered = lowerStatement(stmt)
        if lowered then
            if lowered.type == "compound" and lowered.statements then
                for _, inner in ipairs(lowered.statements) do
                    table.insert(result, inner)
                end
            else
                table.insert(result, lowered)
            end
        end
    end
    return result
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
    for _, field in ipairs(classNode.fields or {}) do
        local irField = {
            name = field.name,
            value = lowerFieldInitializer(field.initializer),
        }
        if field.isStatic then
            table.insert(cls.staticFields, irField)
        else
            table.insert(cls.instanceFields, irField)
        end
    end

    -- Lower methods
    for _, method in ipairs(classNode.methods or {}) do
        local params = {}
        for _, p in ipairs(method.parameters or {}) do
            table.insert(params, p.name)
        end
        local irMethod = {
            name = method.name,
            isStatic = method.isStatic or false,
            params = params,
            body = lowerBlock(method.body),
        }
        table.insert(cls.methods, irMethod)
    end

    -- Lower constructor
    if classNode.constructor then
        local params = {}
        for _, p in ipairs(classNode.constructor.parameters or {}) do
            table.insert(params, p.name)
        end
        cls.constructor = {
            params = params,
            body = lowerBlock(classNode.constructor.body),
        }
    end

    -- Lower properties
    for _, prop in ipairs(classNode.properties or {}) do
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
    for i, v in ipairs(enumNode.values or {}) do
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

-- Determine script type from classes in a module
local function determineScriptType(classes)
    for _, cls in ipairs(classes) do
        if cls.baseClass and SCRIPT_BASES[cls.baseClass] then
            return SCRIPT_BASES[cls.baseClass]
        end
    end
    return "ModuleScript"
end

-- Find the entry class (one with GameEntry method)
local function findEntryClass(classNodes)
    for _, cls in ipairs(classNodes) do
        for _, method in ipairs(cls.methods or {}) do
            if method.name == "GameEntry" then
                return cls.name
            end
        end
    end
    return nil
end

-- Main lowering function: AST → IR
function Lowerer.lower(ast)
    local ir = {
        modules = {},
    }

    -- Create a single module from the AST
    local module = {
        scriptType = "ModuleScript",
        classes = {},
        enums = {},
        entryClass = nil,
    }

    -- Lower classes
    for _, classNode in ipairs(ast.classes or {}) do
        table.insert(module.classes, lowerClass(classNode))
    end

    -- Lower enums
    for _, enumNode in ipairs(ast.enums or {}) do
        table.insert(module.enums, lowerEnum(enumNode))
    end

    -- Determine script type from original AST (need base class info)
    module.scriptType = determineScriptType(ast.classes or {})

    -- Find entry class
    module.entryClass = findEntryClass(ast.classes or {})

    table.insert(ir.modules, module)

    return ir
end

return Lowerer
