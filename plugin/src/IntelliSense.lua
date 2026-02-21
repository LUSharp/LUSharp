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
local CompletionTemplates = requireModule("CompletionTemplates")
local SystemCatalog = requireModule("SystemCatalog")

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

local function makeCompletion(label, kind, detail, documentation, source)
    return {
        label = label,
        kind = kind,
        detail = detail,
        documentation = documentation,
        source = source,
        insertMode = "replacePrefix",
        insertText = label,
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

    if type(maxItems) == "number" and maxItems > 0 and #result > maxItems then
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

local PARAMETER_MODIFIERS = {
    ref = true,
    out = true,
    params = true,
    ["in"] = true,
    ["this"] = true,
}

local function copyMap(map)
    local copied = {}
    for key, value in pairs(map or {}) do
        copied[key] = value
    end
    return copied
end

local function addParameterFromSegment(symbols, segment)
    if #segment == 0 then
        return
    end

    local startIndex = 1
    while startIndex <= #segment do
        local token = segment[startIndex]
        if token.type == "keyword" and PARAMETER_MODIFIERS[token.value] then
            startIndex += 1
        else
            break
        end
    end

    if startIndex > #segment then
        return
    end

    local nameIndex = nil
    for i = startIndex, #segment do
        local token = segment[i]
        local nextToken = segment[i + 1]

        if token.type == "identifier" and (nextToken == nil or nextToken.value == "=") then
            nameIndex = i
            break
        end
    end

    if not nameIndex then
        for i = #segment, startIndex, -1 do
            if segment[i].type == "identifier" then
                nameIndex = i
                break
            end
        end
    end

    if not nameIndex then
        return
    end

    local name = segment[nameIndex].value
    local typeName = ""

    for i = startIndex, nameIndex - 1 do
        typeName ..= segment[i].value
    end

    if typeName == "" then
        typeName = "Object"
    elseif typeName == "var" then
        typeName = "Object"
    end

    symbols[name] = typeName
end

local function parseMethodParameterSymbols(tokens, openIndex, closeIndex)
    local symbols = {}
    local segment = {}
    local genericDepth = 0

    for i = openIndex + 1, closeIndex - 1 do
        local token = tokens[i]

        if token.value == "<" then
            genericDepth += 1
            table.insert(segment, token)
        elseif token.value == ">" and genericDepth > 0 then
            genericDepth -= 1
            table.insert(segment, token)
        elseif token.value == "," and genericDepth == 0 then
            addParameterFromSegment(symbols, segment)
            segment = {}
        else
            table.insert(segment, token)
        end
    end

    addParameterFromSegment(symbols, segment)

    return symbols
end

local MEMBER_DECLARATION_MODIFIERS = {
    public = true,
    private = true,
    protected = true,
    internal = true,
    static = true,
    readonly = true,
    const = true,
    virtual = true,
    override = true,
    sealed = true,
    abstract = true,
    partial = true,
    unsafe = true,
    extern = true,
    new = true,
}

local FOR_CONTROL_FLOW_KEYWORDS = {
    ["if"] = true,
    ["for"] = true,
    ["foreach"] = true,
    ["while"] = true,
    ["switch"] = true,
    ["using"] = true,
    ["lock"] = true,
    ["fixed"] = true,
    ["try"] = true,
    ["do"] = true,
}

local DECLARATION_BOUNDARY_TOKENS = {
    ["{"] = true,
    ["}"] = true,
    [";"] = true,
}

local function previousSignificantToken(tokens, index)
    for i = index, 1, -1 do
        local token = tokens[i]
        if token and token.type ~= "eof" then
            return token, i
        end
    end

    return nil, nil
end

local function isTypeLikeDeclarationToken(token)
    if not token then
        return false
    end

    if token.type == "identifier" then
        return true
    end

    if token.type == "keyword" and (token.value == "void" or TYPE_KEYWORDS[token.value]) then
        return true
    end

    return false
end

local function isMethodOrConstructorDeclaration(tokens, openIndex, closeIndex)
    local nameToken = tokens[openIndex - 1]
    if not nameToken or nameToken.type ~= "identifier" then
        return false
    end

    local nextToken = tokens[closeIndex + 1]
    if not nextToken or nextToken.value ~= "{" then
        return false
    end

    local previousToken, previousIndex = previousSignificantToken(tokens, openIndex - 2)
    if not previousToken then
        return false
    end

    if previousToken.value == "." then
        return false
    end

    if previousToken.type == "keyword" and previousToken.value == "new" then
        return false
    end

    if previousToken.type == "keyword" and MEMBER_DECLARATION_MODIFIERS[previousToken.value] then
        local boundaryToken = previousSignificantToken(tokens, previousIndex - 1)
        if boundaryToken == nil then
            return true
        end

        if boundaryToken.type == "punctuation" and DECLARATION_BOUNDARY_TOKENS[boundaryToken.value] then
            return true
        end
    end

    local scanIndex = openIndex - 2
    local sawTypeToken = false
    local genericDepth = 0

    while scanIndex >= 1 do
        local token = tokens[scanIndex]
        if not token or token.type == "eof" then
            break
        end

        if token.type == "operator" and token.value == ">" then
            genericDepth += 1
            scanIndex -= 1
        elseif token.type == "operator" and token.value == "<" then
            if genericDepth > 0 then
                genericDepth -= 1
                scanIndex -= 1
            else
                break
            end
        elseif isTypeLikeDeclarationToken(token) then
            sawTypeToken = true
            scanIndex -= 1
        elseif token.type == "operator" and token.value == "?" then
            scanIndex -= 1
        elseif token.type == "punctuation" and (
            token.value == "."
            or token.value == "]"
            or token.value == "["
            or (genericDepth > 0 and token.value == ",")
        ) then
            scanIndex -= 1
        else
            break
        end
    end

    if not sawTypeToken then
        return false
    end

    local boundaryToken = previousSignificantToken(tokens, scanIndex)
    if not boundaryToken then
        return true
    end

    if boundaryToken.type == "punctuation" and DECLARATION_BOUNDARY_TOKENS[boundaryToken.value] then
        return true
    end

    return boundaryToken.type == "keyword" and MEMBER_DECLARATION_MODIFIERS[boundaryToken.value] == true
end

local function inferScopedSymbols(sourceUpToCursor)
    local tokens = Lexer.tokenize(sourceUpToCursor)
    local scopeStack = { {} }
    local parenStack = {}
    local pendingMethodParams = {}
    local forScopeStack = {}
    local braceDepth = 0

    local function currentScope()
        return scopeStack[#scopeStack]
    end

    local function declareSymbol(name, typeName)
        if type(name) ~= "string" or name == "" then
            return
        end
        currentScope()[name] = typeName or "Object"
    end

    local function popForScope()
        if #forScopeStack == 0 then
            return
        end

        table.remove(forScopeStack)
        if #scopeStack > 1 then
            table.remove(scopeStack)
        end
    end

    for i = 1, #tokens - 1 do
        local token = tokens[i]

        if token.type == "eof" then
            break
        end

        local activeFor = forScopeStack[#forScopeStack]
        if activeFor and activeFor.mode == "pendingBody" then
            if token.value == "{" then
                activeFor.mode = "block"
                activeFor.blockDepth = braceDepth + 1
            elseif token.value == ";" then
                popForScope()
                activeFor = forScopeStack[#forScopeStack]
            else
                activeFor.mode = "single"
                activeFor.controlStatementHead = token.type == "keyword" and FOR_CONTROL_FLOW_KEYWORDS[token.value] == true
                activeFor.sawBlock = false
                activeFor.lastBlockDepth = nil
            end
        end

        if token.value == "(" then
            local previousToken = tokens[i - 1]
            local isForHeader = previousToken ~= nil and previousToken.type == "keyword" and previousToken.value == "for"
            local forScopeFrame = nil

            if isForHeader then
                table.insert(scopeStack, {})
                forScopeFrame = {
                    mode = "header",
                    parenDepth = 0,
                    braceDepthAtStart = braceDepth,
                    blockDepth = nil,
                    controlStatementHead = false,
                    sawBlock = false,
                    lastBlockDepth = nil,
                }
                table.insert(forScopeStack, forScopeFrame)
            end

            table.insert(parenStack, {
                openIndex = i,
                maybeMethodLike = previousToken ~= nil and previousToken.type == "identifier",
                isForHeader = isForHeader,
                forScopeFrame = forScopeFrame,
            })
        elseif token.value == ")" then
            local frame = table.remove(parenStack)
            if frame then
                if frame.isForHeader and frame.forScopeFrame then
                    frame.forScopeFrame.mode = "pendingBody"
                    frame.forScopeFrame.parenDepth = #parenStack
                    frame.forScopeFrame.braceDepthAtStart = braceDepth
                elseif frame.maybeMethodLike and isMethodOrConstructorDeclaration(tokens, frame.openIndex, i) then
                    pendingMethodParams[i + 1] = parseMethodParameterSymbols(tokens, frame.openIndex, i)
                end
            end
        elseif token.value == "{" then
            local topFor = forScopeStack[#forScopeStack]
            if topFor and topFor.mode == "single" then
                topFor.sawBlock = true
                topFor.lastBlockDepth = braceDepth + 1
            end

            local scope = copyMap(pendingMethodParams[i])
            table.insert(scopeStack, scope)
            braceDepth += 1
        elseif token.value == "}" then
            if #scopeStack > 1 then
                table.remove(scopeStack)
            end

            local topFor = forScopeStack[#forScopeStack]
            if topFor then
                if topFor.mode == "block" and topFor.blockDepth == braceDepth then
                    popForScope()
                elseif topFor.mode == "single"
                    and topFor.controlStatementHead
                    and topFor.sawBlock
                    and topFor.lastBlockDepth == braceDepth
                    and #parenStack == topFor.parenDepth then
                    popForScope()
                end
            end

            braceDepth = math.max(0, braceDepth - 1)
        else
            local topParen = parenStack[#parenStack]
            local insidePotentialMethodParams = topParen and topParen.maybeMethodLike

            if not insidePotentialMethodParams then
                local nextToken = tokens[i + 1]
                local afterNext = tokens[i + 2]

                if nextToken and afterNext then
                    if token.type == "keyword" and token.value == "var" and nextToken.type == "identifier" then
                        declareSymbol(nextToken.value, "Object")
                    elseif (token.type == "identifier" or token.type == "keyword") and nextToken.type == "identifier" then
                        local typeName = token.value
                        local isTypeKeyword = TYPE_KEYWORDS[typeName] == true
                        local looksLikeTypeName = token.type == "identifier" or isTypeKeyword

                        if looksLikeTypeName and (
                            afterNext.type == "operator" and afterNext.value == "="
                            or afterNext.type == "punctuation" and (
                                afterNext.value == ";"
                                or afterNext.value == ","
                                or afterNext.value == ")"
                            )
                        ) then
                            declareSymbol(nextToken.value, typeName)
                        end
                    end
                end
            end

            local topFor = forScopeStack[#forScopeStack]
            if topFor
                and topFor.mode == "single"
                and token.value == ";"
                and #parenStack == topFor.parenDepth
                and braceDepth == topFor.braceDepthAtStart then
                popForScope()
            end
        end
    end

    local visibleSymbols = {}
    for _, scope in ipairs(scopeStack) do
        for name, typeName in pairs(scope) do
            visibleSymbols[name] = typeName
        end
    end

    return visibleSymbols
end

local function buildSymbolModel(sourceUpToCursor)
    return {
        locals = inferScopedSymbols(sourceUpToCursor),
        globals = copyMap(symbolTable),
    }
end

local function getVisibleVariableSymbols(symbolModel)
    local merged = copyMap(symbolModel.globals)

    for name, typeName in pairs(symbolModel.locals or {}) do
        merged[name] = typeName
    end

    return merged
end

local function resolveSymbolType(symbolModel, name)
    if type(name) ~= "string" or name == "" then
        return nil
    end

    if symbolModel.locals and symbolModel.locals[name] then
        return symbolModel.locals[name]
    end

    if symbolModel.globals and symbolModel.globals[name] then
        return symbolModel.globals[name]
    end

    return nil
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
                    nil,
                    "member"
                ))
            end
        end
    end

    for _, completion in ipairs(SystemCatalog.getMemberCompletions(typeName, prefix)) do
        addUniqueCompletion(result, seen, completion)
    end

    return sortAndLimit(result)
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
                        typeInfo.fullName,
                        "type"
                    ))
                end
            end
        end
    end

    return sortAndLimit(result)
end

local function getKeywordCompletions(prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for _, keyword in ipairs(KEYWORDS) do
        if normalizedPrefix == "" or string.sub(keyword, 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(keyword, "keyword", "keyword", nil, "keyword"))
        end
    end

    return sortAndLimit(result)
end

local function getTemplateCompletions(prefix)
    return CompletionTemplates.getCompletions(prefix)
end

local function buildCompletionLabelSet(completions)
    local labels = {}

    for _, completion in ipairs(completions or {}) do
        local label = completion and completion.label
        if type(label) == "string" and label ~= "" then
            labels[label] = true
        end
    end

    return labels
end

local function filterCompletionsByLabel(completions, excludedLabels)
    if not excludedLabels then
        return completions
    end

    local filtered = {}

    for _, completion in ipairs(completions or {}) do
        if not excludedLabels[completion.label] then
            table.insert(filtered, completion)
        end
    end

    return filtered
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
                    "Global",
                    "global"
                ))
            end
        end
    end

    return sortAndLimit(result)
end

local function getLocalCompletions(symbolTypes, prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    for name, typeName in pairs(symbolTypes) do
        if normalizedPrefix == "" or string.sub(string.lower(name), 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(name, "variable", typeName, nil, "local"))
        end
    end

    return sortAndLimit(result)
end

local function mergeCompletions(...)
    local result = {}
    local seen = {}

    for _, list in ipairs({ ... }) do
        for _, completion in ipairs(list) do
            addUniqueCompletion(result, seen, completion)
        end
    end

    return result
end

local function getAllTypeCompletions(prefix, constructableOnly)
    local typeCompletions = getTypeCompletions(prefix, constructableOnly)
    local systemTypeCompletions = SystemCatalog.getTypeCompletions(prefix, constructableOnly)
    return mergeCompletions(systemTypeCompletions, typeCompletions)
end

local function getUsingNamespaceCompletions(prefix)
    local typeCompletions = getTypeCompletions(prefix, false)
    local systemNamespaceCompletions = SystemCatalog.getNamespaceCompletions(prefix)
    local systemTypeCompletions = SystemCatalog.getTypeCompletions(prefix, false)
    return mergeCompletions(systemNamespaceCompletions, systemTypeCompletions, typeCompletions)
end

local CONTEXT_MODE = {
    default = "default",
    memberAccess = "memberAccess",
    newExpression = "newExpression",
    typePosition = "typePosition",
    usingNamespace = "usingNamespace",
    invocationArgs = "invocationArgs",
    statementStart = "statementStart",
}

local DECLARATION_MODIFIERS = {
    public = true,
    private = true,
    protected = true,
    internal = true,
    static = true,
    readonly = true,
    const = true,
    virtual = true,
    override = true,
    sealed = true,
    abstract = true,
    partial = true,
    unsafe = true,
    extern = true,
    new = true,
}

local function objectExpressionBeforeDot(left)
    return left:match("([%a_][%w_]*)%.%w*$")
end

local function objectPathBeforeDot(left)
    return left:match("([%a_][%w_%.]*)%.%w*$")
end

local function typePrefixAfterNew(left)
    return left:match("new%s+([%a_][%w_%.]*)$")
end

local function namespacePrefixAfterUsing(left)
    local explicitPrefix = left:match("using%s+([%a_][%w_%.]*)$")
    if explicitPrefix ~= nil then
        return explicitPrefix
    end

    if left:match("using%s*$") then
        return ""
    end

    return nil
end

local function previousTokenBeforePrefix(left, prefix)
    local withoutPrefix = left
    if prefix and #prefix > 0 then
        withoutPrefix = left:sub(1, #left - #prefix)
    end

    local tokens = Lexer.tokenize(withoutPrefix)
    for i = #tokens, 1, -1 do
        local token = tokens[i]
        if token and token.type ~= "eof" then
            return token
        end
    end

    return nil
end

local TYPE_POSITION_GENERIC_PRECEDER_KEYWORDS = {
    ["new"] = true,
    ["class"] = true,
    ["struct"] = true,
    ["interface"] = true,
    ["where"] = true,
    ["using"] = true,
    ["as"] = true,
    ["is"] = true,
    ["typeof"] = true,
    ["default"] = true,
    ["return"] = true,
}

local function isLikelyTypeIdentifier(token)
    if not token or token.type ~= "identifier" then
        return false
    end

    if resolveType(token.value) then
        return true
    end

    return token.value:match("^[A-Z]") ~= nil
end

local function isGenericTypeArgumentContext(left, prefix)
    local withoutPrefix = left
    if prefix and #prefix > 0 then
        withoutPrefix = left:sub(1, #left - #prefix)
    end

    local tokens = Lexer.tokenize(withoutPrefix)
    local genericOpenToken, genericOpenIndex = previousSignificantToken(tokens, #tokens)

    if not genericOpenToken or genericOpenToken.type ~= "operator" or genericOpenToken.value ~= "<" then
        return false
    end

    local typeToken, typeIndex = previousSignificantToken(tokens, genericOpenIndex - 1)
    if not typeToken then
        return false
    end

    if typeToken.type == "identifier" then
        if not isLikelyTypeIdentifier(typeToken) then
            return false
        end
    elseif not (typeToken.type == "keyword" and (typeToken.value == "void" or TYPE_KEYWORDS[typeToken.value])) then
        return false
    end

    local precedingToken = previousSignificantToken(tokens, typeIndex - 1)
    if not precedingToken then
        return true
    end

    if precedingToken.type == "keyword" then
        return DECLARATION_MODIFIERS[precedingToken.value] == true
            or TYPE_POSITION_GENERIC_PRECEDER_KEYWORDS[precedingToken.value] == true
    end

    if precedingToken.type == "punctuation" then
        return precedingToken.value == ":"
            or precedingToken.value == "("
            or precedingToken.value == ","
            or precedingToken.value == "<"
            or precedingToken.value == ";"
            or precedingToken.value == "{"
            or precedingToken.value == "["
    end

    if precedingToken.type == "operator" then
        return precedingToken.value == "="
            or precedingToken.value == "?"
            or precedingToken.value == "<"
    end

    return false
end

local function isTypePositionContext(left, prefix)
    local previous = previousTokenBeforePrefix(left, prefix)
    if not previous then
        return false
    end

    if previous.type == "punctuation" and (
        previous.value == ":"
        or previous.value == "("
        or previous.value == ","
    ) then
        return true
    end

    if previous.type == "operator" and previous.value == "<" then
        return isGenericTypeArgumentContext(left, prefix)
    end

    if previous.type == "keyword" and DECLARATION_MODIFIERS[previous.value] then
        return true
    end

    return false
end

local function isStatementStartContext(left, prefix)
    local withoutPrefix = left
    if prefix and #prefix > 0 then
        withoutPrefix = left:sub(1, #left - #prefix)
    end

    if withoutPrefix:match("^%s*$") then
        return true
    end

    if withoutPrefix:match("[;{}]%s*$") then
        return true
    end

    if withoutPrefix:match("[\r\n]%s*$") then
        return true
    end

    return false
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

local function analyzeContext(source, cursorPos)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    local left, prefix = extractPrefix(source, cursorPos)
    local objectName = objectExpressionBeforeDot(left)
    local objectPath = objectPathBeforeDot(left)
    local typePrefix = typePrefixAfterNew(left)
    local usingPrefix = namespacePrefixAfterUsing(left)
    local callContext = findCallContext(source, cursorPos)

    local mode = CONTEXT_MODE.default
    local completionPrefix = prefix

    if usingPrefix ~= nil then
        mode = CONTEXT_MODE.usingNamespace
        completionPrefix = usingPrefix
    elseif typePrefix then
        mode = CONTEXT_MODE.newExpression
        completionPrefix = typePrefix
    elseif objectName then
        mode = CONTEXT_MODE.memberAccess
    elseif callContext then
        mode = CONTEXT_MODE.invocationArgs
    elseif isTypePositionContext(left, prefix) then
        mode = CONTEXT_MODE.typePosition
    elseif isStatementStartContext(left, prefix) then
        mode = CONTEXT_MODE.statementStart
    end

    return {
        source = source,
        cursorPos = cursorPos,
        left = left,
        mode = mode,
        prefix = completionPrefix,
        rawPrefix = prefix,
        objectName = objectName,
        objectPath = objectPath,
        typePrefix = typePrefix,
        callContext = callContext,
    }
end

local function buildScopeSymbols(source, cursorPos)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    local sourceUpToCursor = source:sub(1, cursorPos - 1)
    return buildSymbolModel(sourceUpToCursor)
end

local function collectCandidates(context, symbols)
    local visibleSymbols = getVisibleVariableSymbols(symbols)

    if context.mode == CONTEXT_MODE.memberAccess then
        local resolvedObjectType = resolveSymbolType(symbols, context.objectName)
        local objectType = resolvedObjectType
        if not objectType and resolveType(context.objectName) then
            objectType = context.objectName
        end

        local typeMemberCompletions = {}
        if objectType then
            typeMemberCompletions = getMemberCompletions(objectType, context.prefix)
        end

        local systemMemberCompletions = {}
        local systemTarget = context.objectPath or context.objectName

        if type(systemTarget) == "string" and systemTarget ~= "" then
            local hasPathSeparator = string.find(systemTarget, ".", 1, true) ~= nil
            local allowSystemCatalog = true

            if hasPathSeparator then
                local rootIdentifier = systemTarget:match("^([%a_][%w_]*)")
                local rootSymbolType = rootIdentifier and resolveSymbolType(symbols, rootIdentifier) or nil
                if rootSymbolType then
                    allowSystemCatalog = false
                end
            elseif resolvedObjectType then
                allowSystemCatalog = false
            end

            if allowSystemCatalog then
                systemMemberCompletions = SystemCatalog.getMemberCompletions(systemTarget, context.rawPrefix)
            end
        end

        return mergeCompletions(typeMemberCompletions, systemMemberCompletions)
    end

    if context.mode == CONTEXT_MODE.newExpression then
        return getAllTypeCompletions(context.prefix, true)
    end

    if context.mode == CONTEXT_MODE.usingNamespace then
        return getUsingNamespaceCompletions(context.prefix)
    end

    if context.mode == CONTEXT_MODE.typePosition then
        local typeCompletions = getAllTypeCompletions(context.prefix, false)
        local keywordCompletions = getKeywordCompletions(context.prefix)
        return mergeCompletions(typeCompletions, keywordCompletions)
    end

    if context.mode == CONTEXT_MODE.invocationArgs then
        local localCompletions = getLocalCompletions(visibleSymbols, context.prefix)
        local globalCompletions = getGlobalCompletions(context.prefix)
        local typeCompletions = getAllTypeCompletions(context.prefix, false)
        return mergeCompletions(localCompletions, globalCompletions, typeCompletions)
    end

    local localCompletions = getLocalCompletions(visibleSymbols, context.prefix)
    local keywordCompletions = getKeywordCompletions(context.prefix)
    local globalCompletions = getGlobalCompletions(context.prefix)
    local typeCompletions = getAllTypeCompletions(context.prefix, false)

    if context.mode == CONTEXT_MODE.statementStart then
        local templateCompletions = getTemplateCompletions(context.prefix)
        local templateLabels = buildCompletionLabelSet(templateCompletions)
        local keywordCompletionsWithoutTemplateLabels = filterCompletionsByLabel(keywordCompletions, templateLabels)

        return mergeCompletions(
            localCompletions,
            templateCompletions,
            keywordCompletionsWithoutTemplateLabels,
            globalCompletions,
            typeCompletions
        )
    end

    return mergeCompletions(localCompletions, keywordCompletions, globalCompletions, typeCompletions)
end

local MEMBER_LIKE_KINDS = {
    method = true,
    property = true,
    field = true,
    event = true,
    constructor = true,
    member = true,
}

local function isMemberLikeKind(kind)
    return MEMBER_LIKE_KINDS[kind] == true
end

local function defaultSourceForCompletion(completion)
    local kind = completion and completion.kind

    if kind == "variable" then
        return "local"
    end

    if kind == "keyword" then
        return "keyword"
    end

    if kind == "template" then
        return "template"
    end

    if kind == "type" then
        return "type"
    end

    if kind == "namespace" then
        return "namespace"
    end

    if isMemberLikeKind(kind) then
        return "member"
    end

    return "unknown"
end

local function normalizeCompletionMetadata(completion)
    local normalized = {}

    for key, value in pairs(completion or {}) do
        normalized[key] = value
    end

    normalized.label = normalized.label or ""
    normalized.kind = normalized.kind or "unknown"
    normalized.source = normalized.source or defaultSourceForCompletion(normalized)

    if normalized.insertMode == nil then
        if normalized.source == "template" or normalized.kind == "template" then
            normalized.insertMode = "snippetExpand"
        else
            normalized.insertMode = "replacePrefix"
        end
    end

    if normalized.insertText == nil then
        normalized.insertText = normalized.label
    end

    return normalized
end

local function passesContextGate(item, context)
    local mode = context and context.mode or CONTEXT_MODE.default

    if mode == CONTEXT_MODE.memberAccess then
        return item.kind ~= "keyword"
    end

    if mode == CONTEXT_MODE.newExpression then
        return item.kind == "type" or item.kind == "template"
    end

    if mode == CONTEXT_MODE.usingNamespace then
        return item.kind == "namespace" or item.kind == "type"
    end

    if mode == CONTEXT_MODE.typePosition then
        return item.kind == "type"
            or item.kind == "namespace"
            or item.kind == "keyword"
    end

    return true
end

local function contextCompatibilityScore(item, context)
    local mode = context and context.mode or CONTEXT_MODE.default

    if mode == CONTEXT_MODE.memberAccess then
        if isMemberLikeKind(item.kind) then
            return 500
        end
        if item.kind == "namespace" or item.kind == "type" then
            return 470
        end
        if item.kind == "variable" then
            return 200
        end
        if item.kind == "keyword" then
            return 0
        end
        return 120
    end

    if mode == CONTEXT_MODE.newExpression then
        if item.kind == "type" then
            return 500
        end
        if item.kind == "template" then
            return 470
        end
        if item.kind == "namespace" then
            return 260
        end
        if item.kind == "keyword" then
            return 40
        end
        return 60
    end

    if mode == CONTEXT_MODE.typePosition then
        if item.kind == "type" then
            return 500
        end
        if item.kind == "namespace" then
            return 490
        end
        if item.kind == "keyword" then
            return 280
        end
        return 120
    end

    if mode == CONTEXT_MODE.usingNamespace then
        if item.kind == "namespace" then
            return 500
        end
        if item.kind == "type" then
            return 470
        end
        if item.kind == "keyword" then
            return 80
        end
        return 60
    end

    if mode == CONTEXT_MODE.invocationArgs then
        if item.kind == "variable" then
            return 500
        end
        if isMemberLikeKind(item.kind) then
            return 470
        end
        if item.kind == "type" then
            return 320
        end
        if item.kind == "namespace" then
            return 260
        end
        if item.kind == "keyword" then
            return 150
        end
        if item.kind == "template" then
            return 140
        end
        return 200
    end

    if mode == CONTEXT_MODE.statementStart then
        if item.kind == "template" then
            return 530
        end
        if item.kind == "variable" then
            return 490
        end
        if isMemberLikeKind(item.kind) then
            return 460
        end
        if item.kind == "type" then
            return 380
        end
        if item.kind == "namespace" then
            return 360
        end
        if item.kind == "keyword" then
            return 300
        end
        return 220
    end

    if item.kind == "variable" then
        return 500
    end
    if isMemberLikeKind(item.kind) then
        return 460
    end
    if item.kind == "type" then
        return 380
    end
    if item.kind == "namespace" then
        return 340
    end
    if item.kind == "keyword" then
        return 260
    end
    if item.kind == "template" then
        return 250
    end

    return 230
end

local function prefixQualityScore(item, prefix)
    prefix = prefix or ""
    if prefix == "" then
        return 0
    end

    local label = item.label or ""
    if label == "" then
        return 0
    end

    local normalizedPrefix = string.lower(prefix)
    local normalizedLabel = string.lower(label)

    if label == prefix then
        return 220
    end

    if normalizedLabel == normalizedPrefix then
        return 210
    end

    if string.sub(label, 1, #prefix) == prefix then
        return 180 - math.min(#label - #prefix, 60)
    end

    if string.sub(normalizedLabel, 1, #normalizedPrefix) == normalizedPrefix then
        return 170 - math.min(#label - #prefix, 60)
    end

    if string.find(normalizedLabel, normalizedPrefix, 1, true) then
        return 80
    end

    return 0
end

local function typeRelevanceScore(item, context)
    local mode = context and context.mode or CONTEXT_MODE.default
    local score = 0

    if item.kind == "template" then
        score = 85
    elseif item.kind == "type" then
        score = 80
    elseif item.kind == "namespace" then
        score = 70
    elseif isMemberLikeKind(item.kind) then
        score = 62
    elseif item.kind == "variable" then
        score = 60
    elseif item.kind == "keyword" then
        score = 20
    else
        score = 40
    end

    if mode == CONTEXT_MODE.newExpression then
        if item.kind == "type" then
            score += 45
            local detail = string.lower(item.detail or "")
            if detail == "class" or detail == "struct" then
                score += 15
            end
        elseif item.kind == "template" then
            score += 35
        end
    elseif mode == CONTEXT_MODE.typePosition then
        if item.kind == "type" or item.kind == "namespace" then
            score += 35
        elseif item.kind == "keyword" then
            score += 5
        end
    elseif mode == CONTEXT_MODE.usingNamespace then
        if item.kind == "namespace" then
            score += 40
        elseif item.kind == "type" then
            score += 30
        end
    elseif mode == CONTEXT_MODE.memberAccess then
        if isMemberLikeKind(item.kind) then
            score += 30
        elseif item.kind == "type" or item.kind == "namespace" then
            score += 20
        end
    elseif mode == CONTEXT_MODE.invocationArgs then
        if item.kind == "variable" then
            score += 25
        elseif isMemberLikeKind(item.kind) then
            score += 20
        end
    elseif mode == CONTEXT_MODE.statementStart then
        if item.kind == "template" then
            score += 30
        elseif item.kind == "keyword" then
            score += 10
        end
    end

    return score
end

local function providerBoostScore(item)
    local source = item.source or ""

    if source == "template" then
        return 30
    end
    if source == "local" then
        return 22
    end
    if source == "global" then
        return 20
    end
    if source == "member" then
        return 18
    end
    if source == "system" then
        return 16
    end
    if source == "type" then
        return 14
    end
    if source == "namespace" then
        return 12
    end
    if source == "keyword" then
        return 4
    end

    return 0
end

local function rankCandidates(candidates, context)
    local scored = {}

    for index, candidate in ipairs(candidates or {}) do
        local normalized = normalizeCompletionMetadata(candidate)

        if passesContextGate(normalized, context) then
            normalized._contextScore = contextCompatibilityScore(normalized, context)
            normalized._prefixScore = prefixQualityScore(normalized, context and context.prefix or "")
            normalized._typeScore = typeRelevanceScore(normalized, context)
            normalized._providerBoost = providerBoostScore(normalized)
            normalized._stableIndex = index
            table.insert(scored, normalized)
        end
    end

    table.sort(scored, function(a, b)
        if a._contextScore ~= b._contextScore then
            return a._contextScore > b._contextScore
        end

        if a._prefixScore ~= b._prefixScore then
            return a._prefixScore > b._prefixScore
        end

        if a._typeScore ~= b._typeScore then
            return a._typeScore > b._typeScore
        end

        if a._providerBoost ~= b._providerBoost then
            return a._providerBoost > b._providerBoost
        end

        local aLabelLower = string.lower(a.label or "")
        local bLabelLower = string.lower(b.label or "")
        if aLabelLower ~= bLabelLower then
            return aLabelLower < bLabelLower
        end

        if (a.label or "") ~= (b.label or "") then
            return (a.label or "") < (b.label or "")
        end

        if (a.kind or "") ~= (b.kind or "") then
            return (a.kind or "") < (b.kind or "")
        end

        if (a.source or "") ~= (b.source or "") then
            return (a.source or "") < (b.source or "")
        end

        return (a._stableIndex or 0) < (b._stableIndex or 0)
    end)

    for _, item in ipairs(scored) do
        item._contextScore = nil
        item._prefixScore = nil
        item._typeScore = nil
        item._providerBoost = nil
        item._stableIndex = nil
    end

    if #scored > 150 then
        local sliced = {}
        for i = 1, 150 do
            sliced[i] = scored[i]
        end
        return sliced
    end

    return scored
end

function IntelliSense.getCompletions(source, cursorPos)
    local context = analyzeContext(source, cursorPos)
    local symbols = buildScopeSymbols(context.source, context.cursorPos)
    local candidates = collectCandidates(context, symbols)
    return rankCandidates(candidates, context)
end

function IntelliSense._analyzeContextForTests(source, cursorPos)
    return analyzeContext(source, cursorPos)
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
        local symbolModel = buildScopeSymbols(source, cursorPos)
        local objectType = resolveSymbolType(symbolModel, callContext.objectName)

        if not objectType and resolveType(callContext.objectName) then
            objectType = callContext.objectName
        end

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
