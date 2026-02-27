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

local emitExpression
local emitStatement
local emitBlock

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
        for _, arg in ipairs(expr.args or {}) do
            table.insert(args, emitExpression(arg))
        end
        return emitExpression(expr.callee) .. "(" .. join(args) .. ")"
    end

    if t == "method_call" then
        local args = {}
        for _, arg in ipairs(expr.args or {}) do
            table.insert(args, emitExpression(arg))
        end
        return emitExpression(expr.object) .. ":" .. (expr.method or "") .. "(" .. join(args) .. ")"
    end

    if t == "dot_access" then
        return emitExpression(expr.object) .. "." .. (expr.field or "")
    end

    if t == "new_object" then
        local args = {}
        for _, arg in ipairs(expr.args or {}) do
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
        for _, element in ipairs(expr.elements or {}) do
            table.insert(elements, emitExpression(element))
        end
        return "{" .. join(elements) .. "}"
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
        local nested = {}
        appendLine(nested, 0, "function(" .. params .. ")")
        emitBlock(expr.body or {}, nested, 1)
        appendLine(nested, 0, "end")
        return table.concat(nested, "\n")
    end

    -- Fallback
    return "nil"
end

emitBlock = function(stmts, lines, level)
    for _, stmt in ipairs(stmts or {}) do
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

    local t = stmt.type

    if t == "local_decl" then
        if stmt.value then
            appendLine(lines, level, "local " .. stmt.name .. " = " .. emitExpression(stmt.value))
        else
            appendLine(lines, level, "local " .. stmt.name)
        end
        return
    end

    if t == "assignment" then
        appendLine(lines, level, emitExpression(stmt.target) .. " = " .. emitExpression(stmt.value))
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
            "for " .. stmt.var .. " = " .. emitExpression(stmt.start) .. ", " .. emitExpression(stmt.stop) .. ", " .. emitExpression(stmt.step) .. " do")
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
            appendLine(lines, level, "return " .. emitExpression(stmt.value))
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

    if t == "compound" then
        emitBlock(stmt.statements, lines, level)
        return
    end

    if t == "try_catch" then
        appendLine(lines, level, "local __ok, __err = pcall(function()")
        emitBlock(stmt.tryBody, lines, level + 1)
        appendLine(lines, level, "end)")
        appendLine(lines, level, "if not __ok then")
        if stmt.catchVar then
            appendLine(lines, level + 1, "local " .. stmt.catchVar .. " = __err")
        end
        emitBlock(stmt.catchBody, lines, level + 1)
        appendLine(lines, level, "end")
        if stmt.finallyBody and #stmt.finallyBody > 0 then
            emitBlock(stmt.finallyBody, lines, level)
        end
        return
    end

    -- Fallback
    appendLine(lines, level, "--[[ unsupported statement: " .. tostring(t) .. " ]]")
end

local function emitClass(cls, lines)
    appendLine(lines, 0, "local " .. cls.name .. " = {}")

    for _, field in ipairs(cls.staticFields or {}) do
        appendLine(lines, 0, cls.name .. "." .. field.name .. " = " .. emitExpression(field.value))
    end

    if cls.constructor then
        local params = join(cls.constructor.params or {})
        appendLine(lines, 0, "")
        appendLine(lines, 0, "function " .. cls.name .. ".new(" .. params .. ")")
        appendLine(lines, 1, "local self = {}")

        for _, field in ipairs(cls.instanceFields or {}) do
            appendLine(lines, 1, "self." .. field.name .. " = " .. emitExpression(field.value))
        end

        emitBlock(cls.constructor.body or {}, lines, 1)
        appendLine(lines, 1, "return self")
        appendLine(lines, 0, "end")
    end

    for _, method in ipairs(cls.methods or {}) do
        appendLine(lines, 0, "")
        local params = join(method.params or {})
        if method.isStatic then
            appendLine(lines, 0, "function " .. cls.name .. "." .. method.name .. "(" .. params .. ")")
        else
            appendLine(lines, 0, "function " .. cls.name .. ":" .. method.name .. "(" .. params .. ")")
        end
        emitBlock(method.body or {}, lines, 1)
        appendLine(lines, 0, "end")
    end

    appendLine(lines, 0, "")
end

local function findClass(moduleIR, className)
    for _, cls in ipairs(moduleIR.classes or {}) do
        if cls.name == className then
            return cls
        end
    end
    return nil
end

local function findMethod(classIR, methodName)
    for _, m in ipairs(classIR.methods or {}) do
        if m.name == methodName then
            return m
        end
    end
    return nil
end

function Emitter.emit(moduleIR)
    local lines = {}

    if moduleIR.needsEventConnectionCache then
        appendLine(lines, 0, "local __eventConnections = setmetatable({}, { __mode = \"k\" })")
        appendLine(lines, 0, "")
    end

    for _, cls in ipairs(moduleIR.classes or {}) do
        emitClass(cls, lines)
    end

    if moduleIR.scriptType == "ModuleScript" then
        local returnClass = moduleIR.entryClass
        if not returnClass and moduleIR.classes and moduleIR.classes[1] then
            returnClass = moduleIR.classes[1].name
        end
        if returnClass then
            appendLine(lines, 0, "return " .. returnClass)
        end
    elseif (moduleIR.scriptType == "Script" or moduleIR.scriptType == "LocalScript") and moduleIR.entryClass then
        local entryClass = findClass(moduleIR, moduleIR.entryClass)
        local gameEntry = entryClass and findMethod(entryClass, "GameEntry") or nil

        if entryClass and gameEntry and gameEntry.isStatic then
            appendLine(lines, 0, entryClass.name .. ".GameEntry()")
        elseif entryClass and entryClass.constructor then
            appendLine(lines, 0, entryClass.name .. ".new():GameEntry()")
        elseif entryClass then
            appendLine(lines, 0, entryClass.name .. ":GameEntry()")
        end
    end

    return table.concat(lines, "\n")
end

function Emitter.emitModule(moduleIR)
    return Emitter.emit(moduleIR)
end

return Emitter
