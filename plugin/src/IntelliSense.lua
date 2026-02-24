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
    -- Keep this aligned with Lexer keywords + newer C# keywords for better dotnet 8+ authoring.
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
    "char", "checked", "class", "const", "continue", "decimal", "default", "delegate",
    "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float",
    "for", "foreach", "get", "goto", "if", "implicit", "in", "init", "int", "interface", "internal",
    "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
    "override", "params", "private", "protected", "public", "readonly", "record", "ref", "required",
    "return", "sealed", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
    "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
    "using", "var", "virtual", "void", "volatile", "while",

    -- Contextual/modern keywords commonly used in dotnet 8 era code
    "file", "global", "with",
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

local DOTNET_BASE_TYPES = {
    {
        alias = "Console",
        fullName = "System.Console",
        kind = "class",
    },
    {
        alias = "String",
        fullName = "System.String",
        kind = "class",
    },
    {
        alias = "Int32",
        fullName = "System.Int32",
        kind = "struct",
    },
    {
        alias = "List",
        fullName = "System.Collections.Generic.List<T>",
        kind = "class",
    },
    {
        alias = "Dictionary",
        fullName = "System.Collections.Generic.Dictionary<TKey, TValue>",
        kind = "class",
    },
}

local DOTNET_NAMESPACE_MEMBERS = {
    ["System"] = {
        {
            label = "Collections",
            kind = "namespace",
            detail = "namespace",
            documentation = "System.Collections",
        },
        {
            label = "Console",
            kind = "type",
            detail = "class",
            documentation = "System.Console",
        },
        {
            label = "String",
            kind = "type",
            detail = "class",
            documentation = "System.String",
        },
        {
            label = "Int32",
            kind = "type",
            detail = "struct",
            documentation = "System.Int32",
        },
    },
    ["System.Collections"] = {
        {
            label = "Generic",
            kind = "namespace",
            detail = "namespace",
            documentation = "System.Collections.Generic",
        },
    },
    ["System.Collections.Generic"] = {
        {
            label = "List",
            kind = "type",
            detail = "class",
            documentation = "System.Collections.Generic.List<T>",
        },
        {
            label = "Dictionary",
            kind = "type",
            detail = "class",
            documentation = "System.Collections.Generic.Dictionary<TKey, TValue>",
        },
    },
}

local MANUAL_TYPE_ALIASES = {
    Console = "System.Console",
}

local MANUAL_TYPES = {
    ["System.Console"] = {
        name = "Console",
        fullName = "System.Console",
        kind = "class",
        baseType = nil,
        members = {
            {
                kind = "method",
                name = "WriteLine",
                static = true,
                returnType = "Void",
                parameters = { { name = "value", type = "Object" } },
            },
        },
    },
}

local ROBLOX_TYPE_DOCS = {
    DataModel = "Roblox DataModel root singleton (game) that contains the running experience.",
    Workspace = "Roblox Workspace service that contains 3D world instances and simulation state.",
    Players = "Roblox Players service that tracks connected players and player lifecycle events.",
    Part = "Roblox BasePart-derived 3D primitive with physics and rendering properties.",
    Model = "Roblox container instance for grouping parts and related objects into a single unit.",
    Humanoid = "Roblox character controller object that manages health, movement, and character state.",
    Instance = "Roblox base object type for all objects in the data model hierarchy.",
    Vector3 = "Roblox 3D vector value type used for position, direction, and scale.",
    CFrame = "Roblox coordinate frame value type representing 3D position and rotation.",
    Color3 = "Roblox RGB color value type used for visual color properties.",
    UDim2 = "Roblox UI dimension value type combining scale and offset for 2D layout.",
}
local symbolTable = {
    game = "DataModel",
    workspace = "Workspace",
    script = "LuaSourceContainer",
    Enum = "Enums",
    shared = "List<Object>",
}

local DEFAULT_VISIBLE_SERVICES = {
    "Workspace",
    "Players",
    "Lighting",
    "MaterialService",
    "NetworkClient",
    "ReplicatedFirst",
    "ReplicatedStorage",
    "ServerScriptService",
    "ServerStorage",
    "StarterGui",
    "StarterPack",
    "StarterPlayer",
    "Teams",
    "SoundService",
    "TextChatService",
}

local function toLowerSet(names)
    local out = {}
    if type(names) ~= "table" then
        return out
    end

    for _, name in ipairs(names) do
        if type(name) == "string" and name ~= "" then
            out[string.lower(name)] = true
        end
    end

    return out
end

local DEFAULT_VISIBLE_SERVICE_SET = toLowerSet(DEFAULT_VISIBLE_SERVICES)

local SERVICE_NAMES = nil
local USING_NAMESPACE_NAMES = nil

local function getServiceNames()
    if SERVICE_NAMES then
        return SERVICE_NAMES
    end

    local out = {}
    local seen = {}

    local function addName(name)
        if type(name) ~= "string" or name == "" then
            return
        end

        local key = string.lower(name)
        if seen[key] then
            return
        end

        seen[key] = true
        table.insert(out, name)
    end

    for _, defaultName in ipairs(DEFAULT_VISIBLE_SERVICES) do
        addName(defaultName)
    end

    for alias, fullName in pairs(TypeDatabase.aliases or {}) do
        if type(alias) == "string" and alias ~= "" and type(fullName) == "string" then
            if fullName:find("%.Services%.") then
                addName(alias)
            end
        end
    end

    table.sort(out, function(a, b)
        return string.lower(a) < string.lower(b)
    end)

    SERVICE_NAMES = out
    return out
end

local function getVisibleServiceSet(opts)
    local requested = opts and opts.visibleServices
    if type(requested) == "table" then
        return toLowerSet(requested)
    end

    return DEFAULT_VISIBLE_SERVICE_SET
end

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

    local fullName = TypeDatabase.aliases[normalized] or MANUAL_TYPE_ALIASES[normalized] or normalized
    return TypeDatabase.types[fullName] or MANUAL_TYPES[fullName]
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

local function getIdentifierRangeAt(source, cursorPos)
    if type(source) ~= "string" or source == "" then
        return nil
    end

    local index = math.max(1, math.min(cursorPos or 1, #source))
    local char = source:sub(index, index)

    if not char:match("[%w_]") then
        local prevIndex = math.max(1, math.min(index - 1, #source))
        if source:sub(prevIndex, prevIndex):match("[%w_]") then
            index = prevIndex
        else
            local nextIndex = math.max(1, math.min(index + 1, #source))
            if source:sub(nextIndex, nextIndex):match("[%w_]") then
                index = nextIndex
            else
                return nil
            end
        end
    end

    local startIndex = index
    while startIndex > 1 and source:sub(startIndex - 1, startIndex - 1):match("[%w_]") do
        startIndex -= 1
    end

    local endIndex = index
    while endIndex < #source and source:sub(endIndex + 1, endIndex + 1):match("[%w_]") do
        endIndex += 1
    end

    local identifier = source:sub(startIndex, endIndex)
    if not identifier:match("^[%a_][%w_]*$") then
        return nil
    end

    return startIndex, endIndex, identifier
end

local function buildLineStarts(source)
    local starts = { 1 }
    for i = 1, #source do
        if source:sub(i, i) == "\n" then
            table.insert(starts, i + 1)
        end
    end
    return starts
end

local function lineColumnToChunkIndex(lineStarts, line, column, chunkLength)
    local lineStart = lineStarts[line]
    if not lineStart then
        return nil
    end

    local index = lineStart + math.max(0, (column or 1) - 1)
    return math.max(1, math.min(index, chunkLength + 1))
end

local function getNearbyTokenAnchorPositions(source, cursorPos, opts)
    local sourceLength = #source
    if sourceLength == 0 then
        return {}
    end

    local scanRadius = math.max(64, math.floor(tonumber(opts.nearbyScanRadius) or 512))
    local chunkStart = math.max(1, cursorPos - scanRadius)
    local chunkEnd = math.min(sourceLength, cursorPos + scanRadius)
    if chunkEnd < chunkStart then
        return {}
    end

    local chunk = source:sub(chunkStart, chunkEnd)
    if chunk == "" then
        return {}
    end

    local tokens = Lexer.tokenize(chunk)
    local lineStarts = buildLineStarts(chunk)
    local candidates = {}

    for _, token in ipairs(tokens) do
        if token.type == "identifier" or token.type == "keyword" then
            local tokenStartInChunk = lineColumnToChunkIndex(lineStarts, token.line, token.column, #chunk)
            if tokenStartInChunk then
                local absStart = chunkStart + tokenStartInChunk - 1
                local tokenLength = #tostring(token.value or "")
                if tokenLength > 0 then
                    local absEnd = absStart + tokenLength - 1
                    local distance = 0
                    if cursorPos < absStart then
                        distance = absStart - cursorPos
                    elseif cursorPos > absEnd then
                        distance = cursorPos - absEnd
                    end

                    table.insert(candidates, {
                        pos = absStart,
                        distance = distance,
                    })
                end
            end
        end
    end

    table.sort(candidates, function(a, b)
        if a.distance == b.distance then
            return a.pos < b.pos
        end
        return a.distance < b.distance
    end)

    local positions = {}
    local seen = {}
    for _, candidate in ipairs(candidates) do
        if not seen[candidate.pos] then
            seen[candidate.pos] = true
            table.insert(positions, candidate.pos)
        end
    end

    return positions
end

local function parseGenericTypeSuffix(tokens, startIndex)
    local tok = tokens[startIndex]
    if not tok or not (tok.type == "identifier" or tok.type == "keyword") then
        return nil, startIndex
    end

    local parts = { tostring(tok.value) }
    local j = startIndex + 1

    while true do
        local cur = tokens[j]
        local nxt = tokens[j + 1]

        if cur and cur.type == "punctuation" and cur.value == "." and nxt and (nxt.type == "identifier" or nxt.type == "keyword") then
            table.insert(parts, ".")
            table.insert(parts, tostring(nxt.value))
            j += 2
        else
            break
        end
    end

    if tokens[j] and tokens[j].type == "operator" and tokens[j].value == "<" then
        local depth = 0
        while tokens[j] do
            local t = tokens[j]
            if t.type == "operator" and t.value == "<" then
                depth += 1
                table.insert(parts, "<")
            elseif t.type == "operator" and t.value == ">" then
                depth -= 1
                table.insert(parts, ">")
                if depth == 0 then
                    j += 1
                    break
                end
            elseif t.type == "punctuation" and t.value == "," then
                table.insert(parts, ",")
            elseif t.type == "identifier" or t.type == "keyword" then
                table.insert(parts, tostring(t.value))
            end
            j += 1
        end
    end

    if tokens[j] and tokens[j].type == "operator" and tokens[j].value == "?" then
        table.insert(parts, "?")
        j += 1
    end

    if tokens[j] and tokens[j].type == "punctuation" and tokens[j].value == "["
        and tokens[j + 1] and tokens[j + 1].type == "punctuation" and tokens[j + 1].value == "]" then
        table.insert(parts, "[]")
        j += 2
    end

    return table.concat(parts, ""), j
end

local function inferGenericReturnTypeFromCall(tokens, startIndex)
    local tok = tokens[startIndex]
    if not tok or not (tok.type == "identifier" or tok.type == "keyword") then
        return nil
    end

    local i = startIndex
    while tokens[i + 1] and tokens[i + 1].type == "punctuation" and tokens[i + 1].value == "."
        and tokens[i + 2] and (tokens[i + 2].type == "identifier" or tokens[i + 2].type == "keyword") do
        i += 2
    end

    if not (tokens[i + 1] and tokens[i + 1].type == "operator" and tokens[i + 1].value == "<") then
        return nil
    end

    local genericType, afterGenericIndex = parseGenericTypeSuffix(tokens, i + 2)
    if genericType
        and tokens[afterGenericIndex] and tokens[afterGenericIndex].type == "operator" and tokens[afterGenericIndex].value == ">"
        and tokens[afterGenericIndex + 1] and tokens[afterGenericIndex + 1].type == "punctuation" and tokens[afterGenericIndex + 1].value == "(" then
        return genericType
    end

    return nil
end

local unquoteStringTokenValue

local function inferVarTypeFromInitializer(tokens, startIndex)
    local tok = tokens[startIndex]
    if not tok then
        return nil
    end

    if tok.type == "keyword" and tok.value == "new" then
        local typeName = parseGenericTypeSuffix(tokens, startIndex + 1)
        return typeName
    end

    local genericReturnType = inferGenericReturnTypeFromCall(tokens, startIndex)
    if genericReturnType then
        return genericReturnType
    end

    if tok.type == "identifier" and tok.value == "game"
        and tokens[startIndex + 1] and tokens[startIndex + 1].type == "punctuation" and tokens[startIndex + 1].value == "."
        and tokens[startIndex + 2] and tokens[startIndex + 2].type == "identifier" and tokens[startIndex + 2].value == "GetService" then

        if tokens[startIndex + 3] and tokens[startIndex + 3].type == "punctuation" and tokens[startIndex + 3].value == "("
            and tokens[startIndex + 4] and (tokens[startIndex + 4].type == "string" or tokens[startIndex + 4].type == "interpolated_string") then
            return unquoteStringTokenValue(tokens[startIndex + 4].value)
        end
    end

    return nil
end

local function inferLocalSymbols(sourceUpToCursor)
    local tokens = Lexer.tokenize(sourceUpToCursor)
    local symbols = {}

    for i = 1, #tokens - 2 do
        local token = tokens[i]
        local nextToken = tokens[i + 1]

        if token.type == "keyword" and token.value == "var" and nextToken.type == "identifier" then
            local declaredName = nextToken.value
            local inferred = nil

            if tokens[i + 2] and tokens[i + 2].type == "operator" and tokens[i + 2].value == "=" then
                inferred = inferVarTypeFromInitializer(tokens, i + 3)
            end

            symbols[declaredName] = inferred or "Object"
        elseif token.type == "identifier" or token.type == "keyword" then
            local parsedType, afterTypeIndex = parseGenericTypeSuffix(tokens, i)
            if parsedType then
                local declaredToken = tokens[afterTypeIndex]
                local afterDeclared = tokens[afterTypeIndex + 1]

                if declaredToken and declaredToken.type == "identifier" and afterDeclared
                    and (
                        (afterDeclared.type == "operator" and afterDeclared.value == "=")
                        or (afterDeclared.type == "punctuation" and (afterDeclared.value == ";" or afterDeclared.value == "," or afterDeclared.value == ")"))
                    ) then
                    symbols[declaredToken.value] = parsedType
                end
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

local function displayTypeName(typeName)
    local t = tostring(typeName or "")
    if t == "" then
        return t
    end

    t = t:gsub("^LUSharpAPI%.", "")
    return t
end

local function getRobloxTypeDocumentation(typeName, fullName, typeInfo)
    local resolvedTypeInfo = typeInfo or resolveType(typeName) or resolveType(fullName)
    local resolvedFullName = tostring(fullName or (resolvedTypeInfo and resolvedTypeInfo.fullName) or typeName or "")

    local displayName = tostring(typeName or "")
    if displayName == "" and resolvedTypeInfo and resolvedTypeInfo.name then
        displayName = tostring(resolvedTypeInfo.name)
    end
    if displayName == "" then
        displayName = resolvedFullName
    end
    displayName = displayTypeName(displayName)

    local rootName = displayName:match("^([%a_][%w_]*)") or displayName
    local baseDoc = ROBLOX_TYPE_DOCS[rootName]

    local namespace = tostring((resolvedTypeInfo and resolvedTypeInfo.namespace) or "")
    local isRobloxType = baseDoc ~= nil
        or namespace:find("^LUSharpAPI%.Runtime%.STL") ~= nil
        or resolvedFullName:find("^LUSharpAPI%.Runtime%.STL") ~= nil

    if not isRobloxType then
        return displayTypeName(resolvedFullName ~= "" and resolvedFullName or displayName)
    end

    if not baseDoc then
        local kind = tostring((resolvedTypeInfo and resolvedTypeInfo.kind) or "type")
        baseDoc = string.format("Roblox %s %s.", kind, displayName)
    end

    local lines = { baseDoc }

    if resolvedTypeInfo and resolvedTypeInfo.baseType and resolvedTypeInfo.baseType ~= "" then
        table.insert(lines, "Base: " .. displayTypeName(resolvedTypeInfo.baseType))
    end

    if namespace ~= "" then
        table.insert(lines, "Namespace: " .. displayTypeName(namespace))
    end

    if resolvedFullName ~= "" then
        table.insert(lines, "Type: " .. displayTypeName(resolvedFullName))
    end

    if resolvedTypeInfo and resolvedTypeInfo.members then
        table.insert(lines, "Members: " .. tostring(#resolvedTypeInfo.members))
    end

    if rootName ~= "" then
        table.insert(lines, "Docs: https://create.roblox.com/docs/reference/engine/classes/" .. rootName)
    end

    return table.concat(lines, "\n")
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
        local returnType = displayTypeName(member.returnType or "Void")
        local params = {}

        if type(member.parameters) == "table" then
            for index, param in ipairs(member.parameters) do
                local paramType = displayTypeName(param.type or "Object")
                local paramName = tostring(param.name or "")
                if paramName == "" then
                    paramName = "arg" .. tostring(index)
                end
                table.insert(params, string.format("%s %s", paramType, paramName))
            end
        end

        return string.format("method %s(%s) -> %s", member.name, table.concat(params, ", "), returnType)
    end

    if member.kind == "property" or member.kind == "field" or member.kind == "event" then
        return string.format("%s : %s", member.kind, displayTypeName(member.type or "Object"))
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
                        getRobloxTypeDocumentation(alias, typeInfo.fullName, typeInfo)
                    ))
                end
            end
        end
    end

    for _, typeInfo in ipairs(DOTNET_BASE_TYPES) do
        local isConstructable = typeInfo.kind == "class" or typeInfo.kind == "struct"
        if (not constructableOnly or isConstructable) then
            if normalizedPrefix == "" or string.sub(string.lower(typeInfo.alias), 1, #normalizedPrefix) == normalizedPrefix then
                addUniqueCompletion(result, seen, makeCompletion(
                    typeInfo.alias,
                    "type",
                    typeInfo.kind,
                    getRobloxTypeDocumentation(typeInfo.alias, typeInfo.fullName)
                ))
            end
        end
    end

    return sortAndLimit(result, 150)
end

local function getDotnetBaselineCompletions(prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    if normalizedPrefix == "" or string.sub("system", 1, #normalizedPrefix) == normalizedPrefix then
        addUniqueCompletion(result, seen, makeCompletion("System", "namespace", "namespace", "System"))
    end

    for _, typeInfo in ipairs(DOTNET_BASE_TYPES) do
        if normalizedPrefix == "" or string.sub(string.lower(typeInfo.alias), 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(
                typeInfo.alias,
                "type",
                typeInfo.kind,
                typeInfo.fullName
            ))
        end
    end

    return sortAndLimit(result, 80)
end

local function getNamespaceCompletions(left)
    local namespacePath, memberPrefix = left:match("([%a_][%w_%.]*)%.([%a_][%w_]*)$")
    if not namespacePath then
        namespacePath = left:match("([%a_][%w_%.]*)%.$")
        memberPrefix = ""
    end

    if not namespacePath then
        return nil
    end

    local members = DOTNET_NAMESPACE_MEMBERS[namespacePath]
    if not members then
        return nil
    end

    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(memberPrefix or "")

    for _, member in ipairs(members) do
        if normalizedPrefix == "" or string.sub(string.lower(member.label), 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(
                member.label,
                member.kind,
                member.detail,
                member.documentation
            ))
        end
    end

    return sortAndLimit(result, 150)
end

local function addNamespaceWithParents(out, seen, namespaceName)
    if type(namespaceName) ~= "string" then
        return
    end

    local cleaned = tostring(namespaceName):gsub("^%s+", ""):gsub("%s+$", "")
    if cleaned == "" then
        return
    end

    local current = ""
    for segment in cleaned:gmatch("[%a_][%w_]*") do
        if current == "" then
            current = segment
        else
            current = current .. "." .. segment
        end

        local key = string.lower(current)
        if not seen[key] then
            seen[key] = true
            table.insert(out, current)
        end
    end
end

local function getUsingNamespaceNames()
    if USING_NAMESPACE_NAMES then
        return USING_NAMESPACE_NAMES
    end

    local out = {}
    local seen = {}

    for namespaceName, members in pairs(DOTNET_NAMESPACE_MEMBERS) do
        addNamespaceWithParents(out, seen, namespaceName)

        for _, member in ipairs(members or {}) do
            if member.kind == "namespace" then
                local documentation = member.documentation or ""
                addNamespaceWithParents(out, seen, documentation)
                addNamespaceWithParents(out, seen, namespaceName .. "." .. tostring(member.label or ""))
            end
        end
    end

    for _, typeInfo in pairs(TypeDatabase.types or {}) do
        addNamespaceWithParents(out, seen, typeInfo.namespace)
    end

    table.sort(out, function(a, b)
        return string.lower(a) < string.lower(b)
    end)

    USING_NAMESPACE_NAMES = out
    return out
end

local function parseUsingDirectivePath(left)
    local line = tostring(left or ""):match("([^\n]*)$") or ""
    if line:match("^%s*using%s*$") then
        return "", nil, ""
    end

    local path = line:match("^%s*using%s+([%a_][%w_%.]*)%s*$")
    if not path then
        return nil
    end

    if path:sub(-1) == "." then
        return path, path:sub(1, -2), ""
    end

    local namespacePath, memberPrefix = path:match("^(.*)%.([%a_][%w_]*)$")
    if namespacePath then
        return path, namespacePath, memberPrefix
    end

    return path, nil, path
end

local function extractIncludedUsingNamespaces(source, left)
    local result = {}
    local beforeCursor = tostring(left or "")

    local currentLineStart = 1
    for newlinePos in beforeCursor:gmatch("()\n") do
        currentLineStart = newlinePos + 1
    end

    local previousLinesSource = tostring(source or ""):sub(1, math.max(0, currentLineStart - 1))
    for line in (previousLinesSource .. "\n"):gmatch("([^\n]*)\n") do
        local namespaceName = line:match("^%s*using%s+([%a_][%w_%.]*)%s*;?%s*$")
        if namespaceName then
            result[string.lower(namespaceName)] = true
        end
    end

    return result
end

local function getUsingNamespaceCompletions(source, left)
    local path, namespacePath, memberPrefix = parseUsingDirectivePath(left)
    if path == nil then
        return nil
    end

    local result = {}
    local seen = {}
    local seenChildren = {}
    local includedNamespaces = extractIncludedUsingNamespaces(source, left)
    local normalizedPrefix = string.lower(memberPrefix or "")
    local namespaceNames = getUsingNamespaceNames()

    local function maybeAddNamespace(label, fullName)
        local lowerLabel = string.lower(label)
        local lowerFullName = string.lower(tostring(fullName or label or ""))
        if seenChildren[lowerLabel] then
            return
        end

        if includedNamespaces[lowerFullName] then
            return
        end

        if normalizedPrefix ~= "" and string.sub(lowerLabel, 1, #normalizedPrefix) ~= normalizedPrefix then
            return
        end

        seenChildren[lowerLabel] = true
        addUniqueCompletion(result, seen, makeCompletion(label, "namespace", "namespace", fullName))
    end

    if namespacePath == nil then
        for _, fullName in ipairs(namespaceNames) do
            local root = fullName:match("^([%a_][%w_]*)")
            if root then
                maybeAddNamespace(root, root)
            end
        end

        return sortAndLimit(result, 150)
    end

    local baseLower = string.lower(namespacePath)
    local childPrefix = namespacePath .. "."
    local childPrefixLower = string.lower(childPrefix)

    for _, fullName in ipairs(namespaceNames) do
        local fullNameLower = string.lower(fullName)
        if string.sub(fullNameLower, 1, #childPrefixLower) == childPrefixLower then
            local remainder = fullName:sub(#childPrefix + 1)
            local child = remainder:match("^([%a_][%w_]*)")
            if child then
                maybeAddNamespace(child, namespacePath .. "." .. child)
            end
        elseif fullNameLower == baseLower then
            -- Keep exact namespace valid but do not emit itself as a child.
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

local function getLastNonEof(tokens)
    for i = #tokens, 1, -1 do
        if tokens[i] and tokens[i].type ~= "eof" then
            return tokens[i]
        end
    end
    return nil
end

local function isCursorInComment(tokens)
    local last = getLastNonEof(tokens)
    if not last or last.type ~= "comment" then
        return false
    end

    local value = tostring(last.value or "")
    if value:sub(1, 2) == "//" then
        return true
    end

    if value:sub(1, 2) == "/*" then
        return value:sub(-2) ~= "*/"
    end

    return false
end

local function isCursorInStringLiteral(tokens)
    local last = getLastNonEof(tokens)
    if not last then
        return false
    end

    if last.type == "string" or last.type == "interpolated_string" then
        local value = tostring(last.value or "")
        return value:sub(-1) ~= '"'
    end

    return false
end

local function getServiceNameCompletions(prefix, opts)
    local result = {}
    local seen = {}

    local normalizedPrefix = string.lower(prefix or "")
    local visibleSet = getVisibleServiceSet(opts)

    for _, name in ipairs(getServiceNames()) do
        local normalizedName = string.lower(name)
        if visibleSet[normalizedName] and (normalizedPrefix == "" or string.sub(normalizedName, 1, #normalizedPrefix) == normalizedPrefix) then
            addUniqueCompletion(result, seen, makeCompletion(name, "service", "service", nil))
        end
    end

    return sortAndLimit(result, 150)
end

local function objectExpressionBeforeDot(left)
    return left:match("([%a_][%w_]*)%.%w*$")
end

unquoteStringTokenValue = function(value)
    value = tostring(value or "")
    if value:sub(1, 2) == '@"' then
        value = value:sub(3)
    elseif value:sub(1, 2) == '$"' then
        value = value:sub(3)
    elseif value:sub(1, 1) == '"' then
        value = value:sub(2)
    end

    if value:sub(-1) == '"' then
        value = value:sub(1, -2)
    end

    return value
end

local function findMemberInType(typeName, memberName)
    local members = {}
    collectMembers(typeName, nil, members)

    for _, member in ipairs(members) do
        if member.name == memberName then
            return member
        end
    end

    return nil
end

local function inferExprTypeFromTokens(tokens, endIndex, symbolTypes)
    local idx = endIndex

    local function skipTrivia(i)
        while i >= 1 do
            local t = tokens[i]
            if not t or t.type == "eof" then
                i -= 1
            else
                return i
            end
        end
        return i
    end

    local parseMemberAccess

    local function parseExpr(i)
        i = skipTrivia(i)
        if i < 1 then
            return nil, i
        end

        local t = tokens[i]

        if t.type == "identifier" or t.type == "keyword" then
            local node = { kind = "identifier", name = t.value }
            return node, i - 1
        end

        if t.type == "punctuation" and t.value == ")" then
            local depth = 1
            local j = i - 1
            while j >= 1 do
                local tok = tokens[j]
                if tok and tok.type == "punctuation" then
                    if tok.value == ")" then
                        depth += 1
                    elseif tok.value == "(" then
                        depth -= 1
                        if depth == 0 then
                            break
                        end
                    end
                end
                j -= 1
            end

            local openIndex = j
            if openIndex < 1 then
                return nil, i - 1
            end

            local callee, beforeCallee = parseMemberAccess(openIndex - 1)
            if not callee then
                return nil, beforeCallee
            end

            local args = {}
            for k = openIndex + 1, i - 1 do
                local tok = tokens[k]
                if tok and tok.type ~= "eof" then
                    table.insert(args, tok)
                end
            end

            local node = { kind = "call", callee = callee, args = args }
            return node, beforeCallee
        end

        return nil, i - 1
    end

    local function parseMemberChain(i)
        local expr, j = parseExpr(i)
        if not expr then
            return nil
        end

        j = skipTrivia(j)
        while j >= 2 do
            local dot = tokens[j]
            local nameTok = tokens[j + 1]
            -- Note: when parsing backwards, member access pattern is: <obj> . <name>
            -- but our parseExpr consumed <name> already when i pointed at it.
            -- We'll build member nodes in the forward-looking branch below.
            break
        end

        return expr
    end

    local function inferType(expr)
        if not expr then
            return nil
        end

        if expr.kind == "identifier" then
            local symbolType = symbolTypes[expr.name]
            if symbolType then
                return symbolType
            end

            if resolveType(expr.name) then
                return expr.name
            end

            return nil
        end

        if expr.kind == "member" then
            local objType = inferType(expr.object)
            if not objType then
                return nil
            end

            local member = findMemberInType(objType, expr.name)
            if not member then
                return nil
            end

            if member.kind == "method" then
                return member.returnType
            end

            return member.type
        end

        if expr.kind == "call" then
            -- Special-case: game.GetService("Players")
            if expr.callee and expr.callee.kind == "member"
                and expr.callee.object and expr.callee.object.kind == "identifier"
                and expr.callee.object.name == "game"
                and expr.callee.name == "GetService" then
                for _, tok in ipairs(expr.args) do
                    if tok.type == "string" or tok.type == "interpolated_string" then
                        local serviceName = unquoteStringTokenValue(tok.value)
                        if TypeDatabase.aliases[serviceName] then
                            return serviceName
                        end
                        return nil
                    end
                end
                return nil
            end

            -- Generic: infer return type from callee member
            if expr.callee and expr.callee.kind == "member" then
                local objType = inferType(expr.callee.object)
                if not objType then
                    return nil
                end
                local method = findMemberInType(objType, expr.callee.name)
                if method and method.kind == "method" then
                    return method.returnType
                end
            end

            return nil
        end

        return nil
    end

    -- Build member nodes by walking from the end: <obj> '.' <name>
    parseMemberAccess = function(i)
        i = skipTrivia(i)
        local tok = tokens[i]

        -- name
        if tok and tok.type == "identifier" then
            local name = tok.value
            local prev = tokens[i - 1]
            if prev and prev.type == "punctuation" and prev.value == "." then
                local obj, beforeObj = parseMemberAccess(i - 2)
                return { kind = "member", object = obj, name = name }, beforeObj
            end
        end

        return parseExpr(i)
    end

    local expr = nil
    do
        local parsed, _before = parseMemberAccess(idx)
        expr = parsed
    end

    return inferType(expr)
end

local function inferMemberAccessType(tokens, symbolTypes)
    local last = getLastNonEof(tokens)
    if not last then
        return nil, nil
    end

    local i = #tokens
    while i >= 1 and (tokens[i] == nil or tokens[i].type == "eof") do
        i -= 1
    end

    if i < 1 then
        return nil, nil
    end

    local prefix = ""
    local dotIndex = nil

    if tokens[i].type == "punctuation" and tokens[i].value == "." then
        prefix = ""
        dotIndex = i
    elseif tokens[i].type == "identifier" then
        prefix = tokens[i].value
        local prev = tokens[i - 1]
        if prev and prev.type == "punctuation" and prev.value == "." then
            dotIndex = i - 1
        end
    end

    if not dotIndex then
        return nil, nil
    end

    local objType = inferExprTypeFromTokens(tokens, dotIndex - 1, symbolTypes)
    return objType, prefix
end

local function typePrefixAfterNew(left)
    return left:match("new%s+([%a_][%w_%.]*)$")
end

local function staticTypeMemberPrefix(left)
    local typeName, memberPrefix = left:match("([%a_][%w_]*)%.([%a_][%w_]*)$")
    if not typeName then
        typeName = left:match("([%a_][%w_]*)%.$")
        memberPrefix = ""
    end

    if not typeName then
        return nil, nil
    end

    return typeName, memberPrefix or ""
end

local function getStaticTypeMemberCompletions(left)
    local typeName, memberPrefix = staticTypeMemberPrefix(left)
    if not typeName then
        return nil
    end

    if not resolveType(typeName) then
        return nil
    end

    return getMemberCompletions(typeName, memberPrefix)
end

local function isClassDeclarationNameContext(left)
    return left:match("class%s+$") ~= nil or left:match("class%s+[%a_][%w_]*$") ~= nil
end

local function hasMainClassDeclaration(left)
    return left:match("class%s+Main[%s%{%:]+") ~= nil or left:match("class%s+Main$") ~= nil
end

local function isMainEntryMethodNameContext(left)
    local inMethodName = left:match("void%s+$") ~= nil or left:match("void%s+[%a_][%w_]*$") ~= nil
    if not inMethodName then
        return false
    end

    return hasMainClassDeclaration(left)
end

local function getDeclarationNameCompletions(left, prefix)
    local result = {}
    local seen = {}
    local normalizedPrefix = string.lower(prefix or "")

    if isClassDeclarationNameContext(left) then
        if normalizedPrefix == "" or string.sub("main", 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(
                "Main",
                "class",
                "entry class",
                "Default LUSharp entry class name"
            ))
        end
    end

    if isMainEntryMethodNameContext(left) then
        if normalizedPrefix == "" or string.sub("gameentry", 1, #normalizedPrefix) == normalizedPrefix then
            addUniqueCompletion(result, seen, makeCompletion(
                "GameEntry",
                "method",
                "entry method -> Void",
                "Default LUSharp entry method name"
            ))
        end
    end

    return result
end

local COMPLETION_DECLARATION_MODIFIERS = {
    ["public"] = true,
    ["private"] = true,
    ["protected"] = true,
    ["internal"] = true,
    ["static"] = true,
    ["readonly"] = true,
    ["const"] = true,
    ["virtual"] = true,
    ["override"] = true,
    ["abstract"] = true,
    ["sealed"] = true,
    ["partial"] = true,
    ["async"] = true,
}

local DECLARATION_CONTEXT_BOUNDARY_PUNCT = {
    [";"] = true,
    ["{"] = true,
    ["}"] = true,
    ["("] = true,
    [","] = true,
}

local function getLastNonEofIndex(tokens)
    local i = #tokens
    while i >= 1 and tokens[i] and tokens[i].type == "eof" do
        i -= 1
    end

    return i
end

local function looksLikeCustomTypeName(typeName)
    local normalized = normalizeTypeName(typeName)
    if not normalized then
        return false
    end

    if not normalized:match("^[%a_][%w_%.]*$") then
        return false
    end

    local terminalName = normalized:match("([%a_][%w_]*)$") or normalized
    local leadingChar = terminalName:sub(1, 1)
    return leadingChar:match("%u") ~= nil
end

local function isTypeLikeDeclarationName(typeName)
    local normalized = normalizeTypeName(typeName)
    if not normalized then
        return false
    end

    if TYPE_KEYWORDS[normalized] then
        return true
    end

    if resolveType(typeName) or resolveType(normalized) then
        return true
    end

    for _, baseType in ipairs(DOTNET_BASE_TYPES) do
        if baseType.alias == normalized then
            return true
        end
    end

    if looksLikeCustomTypeName(normalized) then
        return true
    end

    return false
end

local function findDeclarationTypeStart(tokens, typeEndIndex)
    if typeEndIndex == nil or typeEndIndex < 1 then
        return nil
    end

    local bestStartIndex = nil

    for startIndex = typeEndIndex, 1, -1 do
        local token = tokens[startIndex]
        if token and (token.type == "identifier" or token.type == "keyword") then
            local parsedType, nextIndex = parseGenericTypeSuffix(tokens, startIndex)
            if parsedType and nextIndex and (nextIndex - 1) == typeEndIndex then
                if isTypeLikeDeclarationName(parsedType) then
                    bestStartIndex = startIndex
                end
            end
        end
    end

    return bestStartIndex
end

local function isValueDeclarationNameContext(left, tokens)
    if type(left) ~= "string" or left == "" then
        return false
    end

    local lastIndex = getLastNonEofIndex(tokens)
    if lastIndex < 1 then
        return false
    end

    local typeStartIndex = nil
    local endsWithWhitespace = left:match("%s$") ~= nil

    if endsWithWhitespace then
        typeStartIndex = findDeclarationTypeStart(tokens, lastIndex)
    else
        local nameToken = tokens[lastIndex]
        if not nameToken or nameToken.type ~= "identifier" then
            return false
        end

        typeStartIndex = findDeclarationTypeStart(tokens, lastIndex - 1)
    end

    if typeStartIndex == nil or typeStartIndex < 1 then
        return false
    end

    local contextIndex = typeStartIndex - 1
    while contextIndex >= 1 do
        local contextToken = tokens[contextIndex]
        if contextToken and contextToken.type == "keyword" and COMPLETION_DECLARATION_MODIFIERS[contextToken.value] then
            contextIndex -= 1
        else
            break
        end
    end

    if contextIndex < 1 then
        return true
    end

    local contextToken = tokens[contextIndex]
    if contextToken and contextToken.type == "punctuation" and DECLARATION_CONTEXT_BOUNDARY_PUNCT[contextToken.value] then
        return true
    end

    return false
end

local function determineCompletionMode(left, tokens, declarationNameCompletions)
    if isClassDeclarationNameContext(left) or isMainEntryMethodNameContext(left) then
        if #declarationNameCompletions > 0 then
            return "declaration_name"
        end

        return "suppress"
    end

    if isValueDeclarationNameContext(left, tokens) then
        return "suppress"
    end

    return "default"
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
    if not nameToken or (nameToken.type ~= "identifier" and nameToken.type ~= "keyword") then
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

function IntelliSense.getCompletions(source, cursorPos, opts)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    opts = opts or {}
    local _context = opts.context

    local left, prefix = extractPrefix(source, cursorPos)

    -- One tokenization pass for context + symbol inference.
    local tokensWithComments = Lexer.tokenize(left, { preserveComments = true })
    if isCursorInComment(tokensWithComments) then
        return {}
    end

    local tokens = Lexer.tokenize(left)
    local symbolTypes = getMergedSymbolTypes(left)

    if isCursorInStringLiteral(tokens) then
        local callContext = findCallContext(source, cursorPos)
        if callContext and callContext.activeParam == 1 then
            if callContext.objectName == "game" and callContext.methodName == "GetService" then
                return getServiceNameCompletions(prefix, opts)
            end

            if callContext.objectName == "Instance" and callContext.methodName == "new" then
                -- Suggest Roblox class names for Instance.new("...")
                local result = {}
                local seen = {}
                local normalizedPrefix = string.lower(prefix or "")

                for alias, fullName in pairs(TypeDatabase.aliases or {}) do
                    if type(alias) == "string" and alias ~= "" and type(fullName) == "string" then
                        if fullName:find("%.Classes%.") then
                            if normalizedPrefix == "" or string.sub(string.lower(alias), 1, #normalizedPrefix) == normalizedPrefix then
                                addUniqueCompletion(result, seen, makeCompletion(alias, "class", "class", fullName))
                            end
                        end
                    end
                end

                return sortAndLimit(result, 150)
            end
        end

        -- No noisy completions inside arbitrary strings.
        return {}
    end

    do
        local objType, memberPrefix = inferMemberAccessType(tokens, symbolTypes)
        if objType then
            return getMemberCompletions(objType, memberPrefix)
        end
    end

    do
        local staticTypeMemberCompletions = getStaticTypeMemberCompletions(left)
        if staticTypeMemberCompletions then
            return staticTypeMemberCompletions
        end
    end

    do
        local usingNamespaceCompletions = getUsingNamespaceCompletions(source, left)
        if usingNamespaceCompletions ~= nil then
            return usingNamespaceCompletions
        end
    end

    do
        local namespaceCompletions = getNamespaceCompletions(left)
        if namespaceCompletions then
            return namespaceCompletions
        end
    end

    local typePrefix = typePrefixAfterNew(left)
    if typePrefix then
        return getTypeCompletions(typePrefix, true)
    end

    local declarationNameCompletions = getDeclarationNameCompletions(left, prefix)
    local completionMode = determineCompletionMode(left, tokens, declarationNameCompletions)
    if completionMode == "declaration_name" then
        return declarationNameCompletions
    end

    if completionMode == "suppress" then
        return {}
    end

    local localCompletions = getLocalCompletions(symbolTypes, prefix)
    local keywordCompletions = getKeywordCompletions(prefix)
    local globalCompletions = getGlobalCompletions(prefix)
    local typeCompletions = getTypeCompletions(prefix, false)
    local dotnetBaselineCompletions = getDotnetBaselineCompletions(prefix)

    return mergeCompletions(declarationNameCompletions, localCompletions, keywordCompletions, globalCompletions, dotnetBaselineCompletions, typeCompletions)
end

local DECLARATION_NON_RETURN_KEYWORDS = {
    ["if"] = true,
    ["for"] = true,
    ["foreach"] = true,
    ["while"] = true,
    ["switch"] = true,
    ["catch"] = true,
    ["return"] = true,
    ["new"] = true,
    ["class"] = true,
    ["interface"] = true,
    ["enum"] = true,
    ["namespace"] = true,
}

local DECLARATION_MODIFIERS = {
    ["public"] = true,
    ["private"] = true,
    ["protected"] = true,
    ["internal"] = true,
    ["static"] = true,
    ["readonly"] = true,
    ["const"] = true,
    ["virtual"] = true,
    ["override"] = true,
    ["abstract"] = true,
    ["sealed"] = true,
    ["partial"] = true,
    ["async"] = true,
}

local HOVER_SUPPRESSED_KEYWORDS = {
    ["public"] = true,
    ["private"] = true,
    ["protected"] = true,
    ["internal"] = true,
    ["static"] = true,
}

local HOVER_SUPPRESSED_STRUCTURAL_CHARS = {
    ["{"] = true,
    ["}"] = true,
}

local function trimWhitespace(value)
    value = tostring(value or "")
    value = value:gsub("^%s+", "")
    value = value:gsub("%s+$", "")
    return value
end

local function collapseWhitespace(value)
    return trimWhitespace(value):gsub("%s+", " ")
end

local function inferCurrentBaseType(beforeIdentifier)
    local baseType = nil
    for matchedBaseType in tostring(beforeIdentifier or ""):gmatch("class%s+[%a_][%w_]*%s*:%s*([%a_][%w_%.<>%?%[%]]*)") do
        baseType = matchedBaseType
    end

    if baseType and baseType ~= "" then
        return displayTypeName(baseType)
    end

    return nil
end

local function getLineTextAtCursor(source, cursorPos)
    if type(source) ~= "string" or source == "" then
        return nil
    end

    local index = math.max(1, math.min(cursorPos or 1, #source + 1))
    local lineStart = 1

    if index > 1 then
        local prefix = source:sub(1, index - 1)
        local lastNewline = prefix:match(".*()\n")
        if lastNewline then
            lineStart = lastNewline + 1
        end
    end

    local lineEnd = source:find("\n", index, true)
    if not lineEnd then
        lineEnd = #source + 1
    end

    return source:sub(lineStart, lineEnd - 1)
end

local function isWhitespaceOnlyLineAtCursor(source, cursorPos)
    local lineText = getLineTextAtCursor(source, cursorPos)
    return type(lineText) == "string" and lineText:match("^%s*$") ~= nil
end

local function isStructuralOnlyLineAtCursor(source, cursorPos)
    local lineText = getLineTextAtCursor(source, cursorPos)
    return type(lineText) == "string" and lineText:match("^%s*[%{%}]%s*$") ~= nil
end

local function shouldSuppressHoverAtCursor(source, cursorPos)
    if type(source) ~= "string" or source == "" then
        return false
    end

    local index = math.max(1, math.min(cursorPos or 1, #source))
    local char = source:sub(index, index)

    if HOVER_SUPPRESSED_STRUCTURAL_CHARS[char] then
        return true
    end

    if isWhitespaceOnlyLineAtCursor(source, cursorPos) then
        return true
    end

    if isStructuralOnlyLineAtCursor(source, cursorPos) then
        return true
    end

    return false
end

local function buildDeclarationMethodSignature(beforeIdentifier, methodName, trailingText)
    local linePrefix = tostring(beforeIdentifier or ""):match("([^\n]*)$") or ""
    local declarationPrefix = collapseWhitespace(linePrefix:match("([^;{}]*)$") or linePrefix)
    local parameters = tostring(trailingText or ""):match("^%s*(%b())") or "()"

    if declarationPrefix == "" then
        return string.format("%s%s", methodName, parameters)
    end

    return string.format("%s %s%s", declarationPrefix, methodName, parameters)
end

local function buildTypeHoverInfo(identifier)
    local typeInfo = resolveType(identifier)
    if not typeInfo then
        local typeFullName = TypeDatabase.aliases[identifier] or identifier
        typeInfo = TypeDatabase.types[typeFullName]
    end

    if not typeInfo then
        return nil
    end

    return {
        label = identifier,
        kind = "type",
        detail = tostring(typeInfo.kind or "type"),
        documentation = getRobloxTypeDocumentation(identifier, typeInfo.fullName or identifier, typeInfo),
    }
end

local function inferDeclarationHover(identifier, trailingText, beforeTrimmed, beforeIdentifier)
    local normalizedIdentifier = string.lower(tostring(identifier or ""))

    if normalizedIdentifier == "class" or normalizedIdentifier == "interface" or normalizedIdentifier == "enum" then
        return {
            label = normalizedIdentifier,
            kind = "keyword",
            detail = "keyword",
            documentation = nil,
        }
    end

    if normalizedIdentifier == "namespace" then
        return {
            label = "namespace",
            kind = "keyword",
            detail = "keyword",
            documentation = nil,
        }
    end

    if normalizedIdentifier == "override" then
        local preMethodTokens, methodName = trailingText:match("^%s+([%a_][%w_%s%.<>%?%[%]]-)%s+([%a_][%w_]*)%s*%(")
        local returnType = preMethodTokens and preMethodTokens:match("([%a_][%w_%.<>%?%[%]]*)%s*$") or nil

        if methodName and returnType then
            local baseType = inferCurrentBaseType(beforeIdentifier)
            local detail = string.format("override %s %s()", displayTypeName(returnType), methodName)
            if baseType then
                detail = string.format("%s <- %s.%s", detail, baseType, methodName)
            end

            return {
                label = methodName,
                kind = "method",
                detail = detail,
                documentation = nil,
            }
        end

        return {
            label = "override",
            kind = "keyword",
            detail = "keyword",
            documentation = nil,
        }
    end

    if beforeTrimmed:match("class$") then
        local baseName = trailingText:match("^%s*:%s*([%a_][%w_%.<>%?%[%]]*)")
        local detail = "class " .. identifier
        if baseName and baseName ~= "" then
            detail = detail .. " : " .. displayTypeName(baseName)
        end

        return {
            label = identifier,
            kind = "class",
            detail = detail,
            documentation = nil,
        }
    end

    if beforeTrimmed:match("interface$") or beforeTrimmed:match("enum$") then
        return {
            label = identifier,
            kind = "type",
            detail = string.format("%s %s", beforeTrimmed:match("([%a_]+)%s+$") or "type", identifier),
            documentation = nil,
        }
    end

    if beforeTrimmed:match("namespace$") then
        local namespaceName = trailingText:match("^%s+([%a_][%w_%.]*)")
        return {
            label = namespaceName or identifier,
            kind = "namespace",
            detail = "namespace",
            documentation = namespaceName or identifier,
        }
    end

    local methodName = trailingText:match("^%s+([%a_][%w_]*)%s*%(")
    if methodName and not DECLARATION_NON_RETURN_KEYWORDS[normalizedIdentifier] then
        local linePrefix = tostring(beforeIdentifier or ""):match("([^\n]*)$") or ""
        local declarationPrefix = collapseWhitespace(linePrefix:match("([^;{}]*)$") or linePrefix)

        local signature = string.format("%s %s()", displayTypeName(identifier), methodName)
        if declarationPrefix ~= "" then
            signature = string.format("%s %s %s()", declarationPrefix, displayTypeName(identifier), methodName)
        end

        return {
            label = methodName,
            kind = "method",
            detail = signature,
            documentation = nil,
        }
    end

    local declaredType = nil
    local declaredName = nil

    if DECLARATION_MODIFIERS[normalizedIdentifier] then
        declaredType, declaredName = trailingText:match("^%s+([%a_][%w_%.<>%?%[%]]*)%s+([%a_][%w_]*)%s*[%=;,%}]")
    else
        declaredType = identifier
        declaredName = trailingText:match("^%s+([%a_][%w_]*)%s*[%=;,%}]")
    end

    if declaredType and declaredName then
        return {
            label = declaredName,
            kind = "variable",
            detail = "variable : " .. displayTypeName(declaredType),
            documentation = nil,
        }
    end

    return nil
end

local function resolveHoverInfoAtPosition(source, cursorPos, opts)
    opts = opts or {}
    local _context = opts.context

    if shouldSuppressHoverAtCursor(source, cursorPos) then
        return nil, true
    end

    local startIndex, endIndex, identifier = getIdentifierRangeAt(source, cursorPos)
    if not identifier then
        return nil
    end

    local normalizedIdentifier = string.lower(tostring(identifier or ""))
    if HOVER_SUPPRESSED_KEYWORDS[normalizedIdentifier] then
        return nil, true
    end

    local sourceUpToIdentifier = source:sub(1, endIndex)
    local symbolTypes = getMergedSymbolTypes(sourceUpToIdentifier)
    local beforeIdentifier = source:sub(1, startIndex - 1)
    local beforeTrimmed = beforeIdentifier:gsub("%s+$", "")

    if beforeTrimmed:sub(-1) == "." then
        local objectExprSource = beforeTrimmed:sub(1, -2)
        local objectTokens = Lexer.tokenize(objectExprSource)

        local objectEndIndex = #objectTokens
        while objectEndIndex >= 1 and objectTokens[objectEndIndex].type == "eof" do
            objectEndIndex -= 1
        end

        if objectEndIndex >= 1 then
            local objectType = inferExprTypeFromTokens(objectTokens, objectEndIndex, symbolTypes)
            if objectType then
                local member = findMemberInType(objectType, identifier)
                if member then
                    return {
                        label = identifier,
                        kind = memberKind(member),
                        detail = memberDetail(member),
                        documentation = member.documentation or member.summary or objectType,
                    }
                end
            end
        end
    end

    local trailingText = source:sub(endIndex + 1)

    if trailingText:match("^%s*%(") then
        local returnType = beforeIdentifier:match("([%a_][%w_%.<>%?%[%]]*)%s+$")
        local normalizedReturnType = string.lower(tostring(returnType or ""))

        if returnType and not DECLARATION_NON_RETURN_KEYWORDS[normalizedReturnType] then
            return {
                label = identifier,
                kind = "method",
                detail = buildDeclarationMethodSignature(beforeIdentifier, identifier, trailingText),
                documentation = nil,
            }
        end
    end

    local declaredNameCandidate = trailingText:match("^%s+([%a_][%w_]*)%s*[%=;,%}]")
    if declaredNameCandidate then
        local typeHover = buildTypeHoverInfo(identifier)
        if typeHover then
            return typeHover
        end
    end

    local declarationHover = inferDeclarationHover(identifier, trailingText, beforeTrimmed, beforeIdentifier)
    if declarationHover then
        return declarationHover
    end

    local localType = symbolTypes[identifier]
    if localType then
        local resolvedLocalType = resolveType(localType) or resolveType(TypeDatabase.aliases[localType] or localType)
        local resolvedLocalFullName = tostring((resolvedLocalType and resolvedLocalType.fullName) or localType)

        local localDisplayType = displayTypeName(localType)
        local detail = "variable : " .. localDisplayType
        if resolvedLocalFullName ~= "" then
            local resolvedDisplay = displayTypeName(resolvedLocalFullName)
            if resolvedDisplay ~= "" and resolvedDisplay ~= localDisplayType then
                detail = detail .. " (" .. resolvedLocalFullName .. ")"
            end
        end

        local namespace = tostring((resolvedLocalType and resolvedLocalType.namespace) or "")
        local rootName = displayTypeName(localType):match("^([%a_][%w_]*)") or displayTypeName(localType)
        local isRobloxType = ROBLOX_TYPE_DOCS[rootName] ~= nil
            or namespace:find("^LUSharpAPI%.Runtime%.STL") ~= nil
            or resolvedLocalFullName:find("^LUSharpAPI%.Runtime%.STL") ~= nil

        local documentation = nil
        if isRobloxType then
            documentation = getRobloxTypeDocumentation(localType, resolvedLocalFullName, resolvedLocalType)
        end

        return {
            label = identifier,
            kind = "variable",
            detail = detail,
            documentation = documentation,
        }
    end

    local globalMember = findMemberInType("Globals", identifier)
    if globalMember then
        return {
            label = identifier,
            kind = memberKind(globalMember),
            detail = memberDetail(globalMember),
            documentation = globalMember.documentation or globalMember.summary or "Global",
        }
    end

    local typeHover = buildTypeHoverInfo(identifier)
    if typeHover then
        return typeHover
    end

    if DOTNET_NAMESPACE_MEMBERS[identifier] ~= nil then
        return {
            label = identifier,
            kind = "namespace",
            detail = "namespace",
            documentation = identifier,
        }
    end

    if KEYWORD_SET[identifier] then
        return {
            label = identifier,
            kind = "keyword",
            detail = "keyword",
            documentation = nil,
        }
    end

    return nil
end

function IntelliSense.getHoverInfo(source, cursorPos, opts)
    source = source or ""
    cursorPos = math.max(1, math.min(cursorPos or (#source + 1), #source + 1))

    opts = opts or {}

    local info, suppressNearby = resolveHoverInfoAtPosition(source, cursorPos, opts)
    if info then
        return info
    end

    if suppressNearby then
        return nil
    end

    if opts.searchNearby then
        local sourceLength = #source
        local radius = math.max(1, math.floor(tonumber(opts.nearbyRadius) or 24))

        for delta = 1, radius do
            local leftPos = cursorPos - delta
            if leftPos >= 1 then
                info = resolveHoverInfoAtPosition(source, leftPos, opts)
                if info then
                    return info
                end
            end

            local rightPos = cursorPos + delta
            if rightPos <= sourceLength + 1 then
                info = resolveHoverInfoAtPosition(source, rightPos, opts)
                if info then
                    return info
                end
            end
        end

        local nearbyTokenAnchors = getNearbyTokenAnchorPositions(source, cursorPos, opts)
        for _, anchorPos in ipairs(nearbyTokenAnchors) do
            info = resolveHoverInfoAtPosition(source, anchorPos, opts)
            if info then
                return info
            end
        end
    end

    return nil
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

local function toIntegerOrNil(value)
    if type(value) ~= "number" then
        return nil
    end

    if value ~= value then
        return nil
    end

    return math.floor(value)
end

local function clampPositiveInt(value, fallback)
    local intValue = toIntegerOrNil(value)
    if intValue == nil then
        intValue = toIntegerOrNil(fallback) or 1
    end

    return math.max(1, intValue)
end

function IntelliSense.getDiagnostics(parseResult)
    local diagnostics = {}

    if not parseResult or type(parseResult) ~= "table" then
        return diagnostics
    end

    for _, diagnostic in ipairs(parseResult.diagnostics or {}) do
        local startLine = clampPositiveInt(diagnostic.line, 1)
        local startColumn = clampPositiveInt(diagnostic.column, 1)

        local rawLength = toIntegerOrNil(diagnostic.length)
        local lengthFromField = nil
        if rawLength and rawLength > 0 then
            lengthFromField = rawLength
        end

        local endLine = clampPositiveInt(diagnostic.endLine, startLine)
        local endColumn = toIntegerOrNil(diagnostic.endColumn)

        if endColumn == nil then
            if lengthFromField and endLine == startLine then
                endColumn = startColumn + lengthFromField
            else
                endColumn = startColumn + 1
            end
        end

        endColumn = math.max(1, endColumn)

        if endLine < startLine then
            endLine = startLine
        end

        if endLine == startLine and endColumn <= startColumn then
            if lengthFromField then
                endColumn = startColumn + lengthFromField
            else
                endColumn = startColumn + 1
            end
        end

        local length = nil
        if endLine == startLine then
            length = math.max(1, endColumn - startColumn)
        elseif lengthFromField then
            length = lengthFromField
        else
            length = 1
        end

        table.insert(diagnostics, {
            severity = diagnostic.severity or "error",
            message = diagnostic.message or "",
            line = startLine,
            column = startColumn,
            endLine = endLine,
            endColumn = endColumn,
            length = length,
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
