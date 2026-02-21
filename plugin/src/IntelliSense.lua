local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local Lexer = requireModule("Lexer")
local TypeDatabase = requireModule("TypeDatabase")

local IntelliSense = {}

local KEYWORDS = {
    "abstract", "as", "bool", "break", "case", "catch", "class", "continue",
    "default", "do", "else", "enum", "false", "finally", "for", "foreach",
    "if", "in", "interface", "new", "null", "private", "protected", "public",
    "return", "static", "switch", "this", "throw", "true", "try", "using",
    "var", "void", "while",
}

local KEYWORD_SET = {}
for _, keyword in ipairs(KEYWORDS) do
    KEYWORD_SET[keyword] = true
end

local TYPE_KEYWORDS = {
    bool = true,
    byte = true,
    char = true,
    decimal = true,
    double = true,
    float = true,
    int = true,
    long = true,
    object = true,
    short = true,
    string = true,
    var = true,
}

local symbolTable = {
    game = "DataModel",
    workspace = "Workspace",
    script = "LuaSourceContainer",
    Enum = "Enums",
    shared = "List<Object>",
}

local function normalizeTypeName(typeName)
    if type(typeName) ~= "string" or typeName == "" then
        return nil
    end

    local normalized = typeName:gsub("%s+", "")
    normalized = normalized:gsub("%?", "")
    normalized = normalized:gsub("%[%]", "")
    normalized = normalized:match("([^<]+)") or normalized

    return normalized
end

local function resolveType(typeName)
    local normalized = normalizeTypeName(typeName)
    if not normalized then
        return nil
    end

    local fullName = TypeDatabase.aliases[normalized] or normalized
    return TypeDatabase.types[fullName]
end

local function collectMembers(typeName, seen, out)
    local typeInfo = resolveType(typeName)
    if not typeInfo then
        return
    end

    seen = seen or {}
    out = out or {}

    if seen[typeInfo.fullName] then
        return
    end
    seen[typeInfo.fullName] = true

    for _, member in ipairs(typeInfo.members or {}) do
        table.insert(out, member)
    end

    if typeInfo.baseType and typeInfo.baseType ~= "" then
        collectMembers(typeInfo.baseType, seen, out)
    end
end

local function makeCompletion(label, kind, detail, documentation)
    return {
        label = label,
        kind = kind,
        detail = detail,
        documentation = documentation,
    }
end

local function addUniqueCompletion(result, seen, completion)
    local key = completion.kind .. "::" .. completion.label
    if seen[key] then
        return
    end

    seen[key] = true
    table.insert(result, completion)
end

local function sortAndLimit(result, maxItems)
    table.sort(result, function(a, b)
        if a.label == b.label then
            return a.kind < b.kind
        end
        return a.label < b.label
    end)

    if #result > maxItems then
        local sliced = {}
        for i = 1, maxItems do
            sliced[i] = result[i]
        end
        return sliced
    end

    return result
end

local function extractPrefix(source, cursorPos)
    local left = source:sub(1, cursorPos - 1)
    local prefix = left:match("([%a_][%w_]*)$") or ""
    return left, prefix
end

local function inferLocalSymbols(sourceUpToCursor)
    local tokens = Lexer.tokenize(sourceUpToCursor)
    local symbols = {}

    for i = 1, #tokens - 2 do
        local token = tokens[i]
        local nextToken = tokens[i + 1]
        local afterNext = tokens[i + 2]

        if token.type == "keyword" and token.value == "var" and nextToken.type == "identifier" then
            symbols[nextToken.value] = "Object"
        elseif (token.type == "identifier" or token.type == "keyword") and nextToken.type == "identifier" then
            local typeName = token.value
            local isTypeKeyword = TYPE_KEYWORDS[typeName] == true
            local looksLikeTypeName = token.type == "identifier" or isTypeKeyword

            if looksLikeTypeName and (afterNext.type == "operator" and afterNext.value == "="
                or afterNext.type == "punctuation" and (afterNext.value == ";" or afterNext.value == "," or afterNext.value == ")")) then
                symbols[nextToken.value] = typeName
            end
        end
    end

    return symbols
end

local function getMergedSymbolTypes(sourceUpToCursor)
    local merged = {}

    for name, typeName in pairs(symbolTable) do
        merged[name] = typeName
    end

    local locals = inferLocalSymbols(sourceUpToCursor)
    for name, typeName in pairs(locals) do
        merged[name] = typeName
    end

    return merged
end

local function getGlobalType()
    return resolveType("Globals")
end

local function memberKind(member)
    if member.kind == "method" then return "method" end
    if member.kind == "property" then return "property" end
    if member.kind == "field" then return "field" end
    if member.kind == "event" then return "event" end
    if member.kind == "constructor" then return "constructor" end
    return "member"
end

local function memberDetail(member)
    if member.kind == "method" then
        local returnType = member.returnType or "Void"
        return string.format("method %s -> %s", member.name, returnType)
    end

    if member.kind == "property" or member.kind == "field" or member.kind == "event" then
        return string.format("%s : %s", member.kind, member.type or "Object")
    end

    return member.kind
end

local function getMemberCompletions(typeName, prefix)
    local members = {}
    collectMembers(typeName, nil, members)

    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, member in ipairs(members) do
        local label = member.name
        if type(label) == "string" and label ~= "" then
            if normalizedPrefix == "" or string.sub(string.lower(label), 1, #normalizedPrefix) == normalizedPrefix then
                addUniqueCompletion(result, seen, makeCompletion(
                    label,
                    memberKind(member),
                    memberDetail(member),
                    nil
                ))
            end
        end
    end

    return sortAndLimit(result, 100)
end

local function getTypeCompletions(prefix, constructableOnly)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for alias, fullName in pairs(TypeDatabase.aliases or {}) do
        local typeInfo = TypeDatabase.types[fullName]
        if typeInfo then
            local isConstructable = typeInfo.kind == "class" or typeInfo.kind == "struct"
            if (not constructableOnly or isConstructable) then
                if normalizedPrefix == "" or string.sub(string.lower(alias), 1, #normalizedPrefix) == normalizedPrefix then
                    addUniqueCompletion(result, seen, makeCompletion(
                        alias,
                        "type",
                        typeInfo.kind,
                        typeInfo.fullName
                    ))
                end
            end
        end
    end

    return sortAndLimit(result, 150)
end

local function getKeywordCompletions(prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, keyword in ipairs(KEYWORDS) do
        if normalizedPrefix == "" or string.sub(keyword, 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(keyword, "keyword", "keyword", nil))
        end
    end

    return sortAndLimit(result, 80)
end

local function getGlobalCompletions(prefix)
    local globalType = getGlobalType()
    if not globalType then
        return {}
    end

    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, member in ipairs(globalType.members or {}) do
        local label = member.name
        if type(label) == "string" and label ~= "" then
            if normalizedPrefix == "" or string.sub(string.lower(label), 1, #normalizedPrefix) == normalizedPrefix then
                addUniqueCompletion(result, seen, makeCompletion(
                    label,
                    memberKind(member),
                    memberDetail(member),
                    "Global"
                ))
            end
        end
    end

    return sortAndLimit(result, 120)
end

local function getLocalCompletions(symbolTypes, prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for name, typeName in pairs(symbolTypes) do
        if normalizedPrefix == "" or string.sub(string.lower(name), 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(name, "variable", typeName, nil))
        end
    end

    return sortAndLimit(result, 80)
end

local function mergeCompletions(...)
    local result = {}
    local seen = {}

    for _, list in ipairs({ ... }) do
        for _, completion in ipairs(list) do
            addUniqueCompletion(result, seen, completion)
        end
    end

    if #result > 150 then
        local sliced = {}
        for i = 1, 150 do
            sliced[i] = result[i]
        end
        return sliced
    end

    return result
end

local function objectExpressionBeforeDot(left)
    return left:match("([%a_][%w_]*)%.%w*$")
end

local function typePrefixAfterNew(left)
    return left:match("new%s+([%a_][%w_%.]*)$")
end

local function findCallContext(source, cursorPos)
    local left = source:sub(1, cursorPos - 1)
    local tokens = Lexer.tokenize(left)

    local stack = {}

    for i, token in ipairs(tokens) do
        if token.type == "eof" then
            break
        end

        if token.value == "(" then
            table.insert(stack, { tokenIndex = i, commaCount = 0 })
        elseif token.value == ")" then
            if #stack > 0 then
                table.remove(stack)
            end
        elseif token.value == "," then
            local top = stack[#stack]
            if top then
                top.commaCount += 1
            end
        end
    end

    local activeCall = stack[#stack]
    if not activeCall then
        return nil
    end

    local nameTokenIndex = activeCall.tokenIndex - 1
    local nameToken = tokens[nameTokenIndex]
    if not nameToken or nameToken.type ~= "identifier" then
        return nil
    end

    local objectToken = nil
    local dotToken = tokens[nameTokenIndex - 1]
    if dotToken and dotToken.value == "." then
        local possibleObject = tokens[nameTokenIndex - 2]
        if possibleObject and possibleObject.type == "identifier" then
            objectToken = possibleObject
        end
    end

    return {
        objectName = objectToken and objectToken.value or nil,
        methodName = nameToken.value,
        activeParam = activeCall.commaCount + 1,
    }
end

local function findMethod(typeName, methodName)
    local members = {}
    collectMembers(typeName, nil, members)

    for _, member in ipairs(members) do
        if member.kind == "method" and member.name == methodName then
            return member
        end
    end

    return nil
end

function IntelliSense.getCompletions(source, cursorPos)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    local left, prefix = extractPrefix(source, cursorPos)
    local symbolTypes = getMergedSymbolTypes(left)

    local objectName = objectExpressionBeforeDot(left)
    if objectName then
        local objectType = symbolTypes[objectName]
        if objectType then
            return getMemberCompletions(objectType, prefix)
        end
        return {}
    end

    local typePrefix = typePrefixAfterNew(left)
    if typePrefix then
        return getTypeCompletions(typePrefix, true)
    end

    local localCompletions = getLocalCompletions(symbolTypes, prefix)
    local keywordCompletions = getKeywordCompletions(prefix)
    local globalCompletions = getGlobalCompletions(prefix)

    local typeCompletions = getTypeCompletions(prefix, false)

    return mergeCompletions(localCompletions, keywordCompletions, globalCompletions, typeCompletions)
end

function IntelliSense.getParameterHints(source, cursorPos)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    local callContext = findCallContext(source, cursorPos)
    if not callContext then
        return nil
    end

    local method = nil

    if callContext.objectName then
        local left = source:sub(1, cursorPos - 1)
        local symbolTypes = getMergedSymbolTypes(left)
        local objectType = symbolTypes[callContext.objectName]
        if objectType then
            method = findMethod(objectType, callContext.methodName)
        end
    else
        method = findMethod("Globals", callContext.methodName)
    end

    if not method then
        return nil
    end

    return {
        methodName = callContext.methodName,
        parameters = method.parameters or {},
        activeParam = callContext.activeParam,
    }
end

function IntelliSense.getDiagnostics(parseResult)
    local diagnostics = {}

    if not parseResult or type(parseResult) ~= "table" then
        return diagnostics
    end

    for _, diagnostic in ipairs(parseResult.diagnostics or {}) do
        table.insert(diagnostics, {
            severity = diagnostic.severity or "error",
            message = diagnostic.message or "",
            line = diagnostic.line or 1,
            column = diagnostic.column or 1,
            code = diagnostic.code,
        })
    end

    return diagnostics
end

function IntelliSense.setSymbolType(name, typeName)
    if type(name) ~= "string" or name == "" then
        return false
    end

    if type(typeName) ~= "string" or typeName == "" then
        return false
    end

    symbolTable[name] = typeName
    return true
end

function IntelliSense.resetSymbolTable()
    symbolTable = {
        game = "DataModel",
        workspace = "Workspace",
        script = "LuaSourceContainer",
        Enum = "Enums",
        shared = "List<Object>",
    }
end

function IntelliSense.getSymbolType(name)
    return symbolTable[name]
end

return IntelliSense
