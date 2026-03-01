-- LUSharp Emitter: Transforms Lua IR into Luau source text
-- Input: one module IR object (e.g., ir.modules[1])
-- Output: Luau source string

local Emitter = {}

local INDENT = "    "

local function join(list, sep)
    return table.concat(list, sep or ", ")
end

local function appendLine(lines, level, text)
    level = level or 0
    text = text or ""
    table.insert(lines, string.rep(INDENT, level) .. text)
end

-- Strip matched outer parentheses from an expression string
local function stripOuterParens(s)
    if #s < 2 or s:sub(1, 1) ~= "(" or s:sub(-1) ~= ")" then
        return s
    end
    local depth = 0
    for i = 1, #s do
        local c = s:sub(i, i)
        if c == "(" then
            depth = depth + 1
        elseif c == ")" then
            depth = depth - 1
        end
        if depth == 0 and i < #s then
            return s
        end
    end
    return s:sub(2, -2)
end

-- Post-process: collapse consecutive blank lines, trim trailing blanks
local function beautify(source)
    source = source:gsub("\n\n\n+", "\n\n")
    source = source:gsub("[ \t]+\n", "\n")
    source = source:gsub("\n+$", "")
    return source
end

local emitExpression
local emitStatement
local emitBlock

-- Tracks the current statement indent level for multi-line expressions
local currentBaseLevel = 0

emitExpression = function(expr)
    if not expr then
        return "nil"
    end

    local t = expr.type

    if t == "literal" then
        return tostring(expr.value)
    end

    if t == "identifier" then
        return expr.name
    end

    if t == "binary_op" then
        return "(" .. emitExpression(expr.left) .. " " .. (expr.op or "") .. " " .. emitExpression(expr.right) .. ")"
    end

    if t == "unary_op" then
        return "(" .. (expr.op or "") .. emitExpression(expr.operand) .. ")"
    end

    if t == "call" then
        local args = {}
        for _, arg in expr.args or {} do
            table.insert(args, emitExpression(arg))
        end
        return emitExpression(expr.callee) .. "(" .. join(args) .. ")"
    end

    if t == "method_call" then
        local args = {}
        for _, arg in expr.args or {} do
            table.insert(args, emitExpression(arg))
        end
        return emitExpression(expr.object) .. ":" .. (expr.method or "") .. "(" .. join(args) .. ")"
    end

    if t == "dot_access" then
        return emitExpression(expr.object) .. "." .. (expr.field or "")
    end

    if t == "new_object" then
        local args = {}
        for _, arg in expr.args or {} do
            table.insert(args, emitExpression(arg))
        end

        local className = tostring(expr.class or "")
        className = className:gsub("<.*>", "")
        className = className:match("([%w_]+)$") or className

        return className .. ".new(" .. join(args) .. ")"
    end

    if t == "template_string" then
        local value = tostring(expr.value or "")
        value = string.gsub(value, "`", "\\`")
        return "`" .. value .. "`"
    end

    if t == "array_literal" then
        local elements = {}
        for _, element in expr.elements or {} do
            table.insert(elements, emitExpression(element))
        end
        return "{ " .. join(elements) .. " }"
    end

    if t == "index_access" then
        return emitExpression(expr.object) .. "[" .. emitExpression(expr.key) .. "]"
    end

    if t == "ternary" then
        return "(" .. emitExpression(expr.condition) .. " and " .. emitExpression(expr.thenExpr) .. " or " .. emitExpression(expr.elseExpr) .. ")"
    end

    if t == "null_conditional" then
        local objectText = emitExpression(expr.object)
        local fieldName = expr.field or ""
        return "(" .. objectText .. " and " .. objectText .. "." .. fieldName .. ")"
    end

    if t == "incdec_expr" then
        local op = expr.operator == "--" and "-" or "+"
        return "(" .. emitExpression(expr.operand) .. " " .. op .. " 1)"
    end

    if t == "function_expr" then
        local params = join(expr.params or {})
        local body = expr.body or {}

        -- Single-statement body: emit inline
        if #body == 1 and body[1].type ~= "if_stmt" and body[1].type ~= "for_numeric"
            and body[1].type ~= "for_in" and body[1].type ~= "while_stmt"
            and body[1].type ~= "repeat_until" and body[1].type ~= "try_catch" then
            local inner = {}
            emitStatement(body[1], inner, 0)
            local inlineBody = inner[1]
            if inlineBody and not inlineBody:find("\n") and #inlineBody < 60 then
                return "function(" .. params .. ") " .. inlineBody:match("^%s*(.-)%s*$") .. " end"
            end
        end

        -- Multi-line: indent body relative to current statement level
        local nested = {}
        appendLine(nested, currentBaseLevel, "function(" .. params .. ")")
        emitBlock(body, nested, currentBaseLevel + 1)
        appendLine(nested, currentBaseLevel, "end")
        return table.concat(nested, "\n")
    end

    -- Fallback
    return "nil"
end

emitBlock = function(stmts, lines, level)
    for _, stmt in stmts or {} do
        emitStatement(stmt, lines, level)
    end
end

local function emitIfChain(stmt, lines, level, isElseIf)
    local head = isElseIf and "elseif " or "if "
    appendLine(lines, level, head .. emitExpression(stmt.condition) .. " then")
    emitBlock(stmt.body, lines, level + 1)

    local elseBody = stmt.elseBody
    if elseBody and #elseBody > 0 then
        if #elseBody == 1 and elseBody[1].type == "if_stmt" then
            emitIfChain(elseBody[1], lines, level, true)
        else
            appendLine(lines, level, "else")
            emitBlock(elseBody, lines, level + 1)
        end
    end

    if not isElseIf then
        appendLine(lines, level, "end")
    end
end

emitStatement = function(stmt, lines, level)
    if not stmt then
        return
    end

    currentBaseLevel = level

    local t = stmt.type

    if t == "local_decl" then
        if stmt.value then
            appendLine(lines, level, "local " .. stmt.name .. " = " .. stripOuterParens(emitExpression(stmt.value)))
        else
            appendLine(lines, level, "local " .. stmt.name)
        end
        return
    end

    if t == "assignment" then
        -- Detect increment/decrement: x = (x + 1) → x += 1
        if stmt.value and stmt.value.type == "incdec_expr" then
            local targetStr = emitExpression(stmt.target)
            local operandStr = emitExpression(stmt.value.operand)
            if targetStr == operandStr then
                local op = stmt.value.operator == "--" and "-=" or "+="
                appendLine(lines, level, targetStr .. " " .. op .. " 1")
                return
            end
        end
        appendLine(lines, level, emitExpression(stmt.target) .. " = " .. stripOuterParens(emitExpression(stmt.value)))
        return
    end

    if t == "expr_statement" then
        appendLine(lines, level, emitExpression(stmt.expression))
        return
    end

    if t == "compound" then
        emitBlock(stmt.statements or {}, lines, level)
        return
    end

    if t == "if_stmt" then
        emitIfChain(stmt, lines, level, false)
        return
    end

    if t == "for_numeric" then
        appendLine(lines, level,
            "for " .. stmt.var .. " = " .. stripOuterParens(emitExpression(stmt.start)) .. ", "
            .. stripOuterParens(emitExpression(stmt.stop)) .. ", "
            .. stripOuterParens(emitExpression(stmt.step)) .. " do")
        emitBlock(stmt.body, lines, level + 1)
        appendLine(lines, level, "end")
        return
    end

    if t == "for_in" then
        appendLine(lines, level, "for " .. join(stmt.vars or {}) .. " in " .. emitExpression(stmt.iterator) .. " do")
        emitBlock(stmt.body, lines, level + 1)
        appendLine(lines, level, "end")
        return
    end

    if t == "while_stmt" then
        appendLine(lines, level, "while " .. emitExpression(stmt.condition) .. " do")
        emitBlock(stmt.body, lines, level + 1)
        appendLine(lines, level, "end")
        return
    end

    if t == "repeat_until" then
        appendLine(lines, level, "repeat")
        emitBlock(stmt.body, lines, level + 1)
        appendLine(lines, level, "until " .. emitExpression(stmt.condition))
        return
    end

    if t == "return_stmt" then
        if stmt.value then
            appendLine(lines, level, "return " .. stripOuterParens(emitExpression(stmt.value)))
        else
            appendLine(lines, level, "return")
        end
        return
    end

    if t == "break_stmt" then
        appendLine(lines, level, "break")
        return
    end

    if t == "continue_stmt" then
        appendLine(lines, level, "continue")
        return
    end

    if t == "try_catch" then
        appendLine(lines, level, "local __ok, __err = pcall(function()")
        emitBlock(stmt.tryBody, lines, level + 1)
        appendLine(lines, level, "end)")
        appendLine(lines, level, "")
        appendLine(lines, level, "if not __ok then")
        if stmt.catchVar then
            appendLine(lines, level + 1, "local " .. stmt.catchVar .. " = __err")
        end
        emitBlock(stmt.catchBody, lines, level + 1)
        appendLine(lines, level, "end")
        if stmt.finallyBody and #stmt.finallyBody > 0 then
            appendLine(lines, level, "")
            emitBlock(stmt.finallyBody, lines, level)
        end
        return
    end

    -- Fallback
    appendLine(lines, level, "--[[ unsupported statement: " .. tostring(t) .. " ]]")
end

local function emitClass(cls, lines)
    appendLine(lines, 0, "local " .. cls.name .. " = {}")

    for _, field in cls.staticFields or {} do
        appendLine(lines, 0, cls.name .. "." .. field.name .. " = " .. stripOuterParens(emitExpression(field.value)))
    end

    if cls.constructor then
        local params = join(cls.constructor.params or {})
        appendLine(lines, 0, "")
        appendLine(lines, 0, "function " .. cls.name .. ".new(" .. params .. ")")
        appendLine(lines, 1, "local self = {}")

        for _, field in cls.instanceFields or {} do
            appendLine(lines, 1, "self." .. field.name .. " = " .. stripOuterParens(emitExpression(field.value)))
        end

        emitBlock(cls.constructor.body or {}, lines, 1)
        appendLine(lines, 1, "return self")
        appendLine(lines, 0, "end")
    end

    for _, method in cls.methods or {} do
        appendLine(lines, 0, "")
        local params = join(method.params or {})
        appendLine(lines, 0, "function " .. cls.name .. ":" .. method.name .. "(" .. params .. ")")
        emitBlock(method.body or {}, lines, 1)
        appendLine(lines, 0, "end")
    end

    appendLine(lines, 0, "")
end

local function findClass(moduleIR, className)
    for _, cls in moduleIR.classes or {} do
        if cls.name == className then
            return cls
        end
    end
    return nil
end

local function findMethod(classIR, methodName)
    for _, m in classIR.methods or {} do
        if m.name == methodName then
            return m
        end
    end
    return nil
end

function Emitter.emit(moduleIR)
    local lines = {}

    local hasRequires = moduleIR.requires and #moduleIR.requires > 0

    -- Determine class name to return
    local returnClass = moduleIR.entryClass
    if not returnClass and moduleIR.classes and moduleIR.classes[1] then
        returnClass = moduleIR.classes[1].name
    end

    -- Forward-declare cross-script deps (resolved via __init by runtime)
    if hasRequires then
        for _, req in moduleIR.requires do
            appendLine(lines, 0, "local " .. req.name)
        end
        appendLine(lines, 0, "")
    end

    if moduleIR.needsEventConnectionCache then
        appendLine(lines, 0, "local __eventConnections = setmetatable({}, { __mode = \"k\" })")
        appendLine(lines, 0, "")
    end

    -- Emit service locals
    for _, svc in moduleIR.services or {} do
        appendLine(lines, 0, "local " .. svc.name .. " = game:GetService(\"" .. svc.name .. "\")")
    end
    if moduleIR.services and #moduleIR.services > 0 then
        appendLine(lines, 0, "")
    end

    -- Emit classes
    for _, cls in moduleIR.classes or {} do
        emitClass(cls, lines)
    end

    -- Emit __init for cross-script dep resolution (called by runtime)
    if hasRequires and returnClass then
        appendLine(lines, 0, "function " .. returnClass .. ".__init(__shared)")
        for _, req in moduleIR.requires do
            appendLine(lines, 1, req.name .. " = __shared." .. req.name)
        end
        appendLine(lines, 0, "end")
        appendLine(lines, 0, "")
    end

    -- Return class table (runtime detects and calls Main() if present)
    if returnClass then
        appendLine(lines, 0, "return " .. returnClass)
    end

    return beautify(table.concat(lines, "\n"))
end

function Emitter.emitModule(moduleIR)
    return Emitter.emit(moduleIR)
end

return Emitter
