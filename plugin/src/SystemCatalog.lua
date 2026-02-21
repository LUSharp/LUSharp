local SystemCatalog = {}

local NAMESPACE_ORDER = {
    "System",
    "System.Collections",
    "System.Collections.Generic",
    "System.Linq",
    "System.Text",
}

local TYPE_ORDER = {
    "Console",
    "Math",
    "List",
    "Dictionary",
    "StringBuilder",
    "Enumerable",
}

local SYSTEM_NAMESPACES = {
    ["System"] = {
        namespaces = { "System.Collections", "System.Linq", "System.Text" },
        types = { "Console", "Math" },
    },
    ["System.Collections"] = {
        namespaces = { "System.Collections.Generic" },
        types = {},
    },
    ["System.Collections.Generic"] = {
        namespaces = {},
        types = { "List", "Dictionary" },
    },
    ["System.Linq"] = {
        namespaces = {},
        types = { "Enumerable" },
    },
    ["System.Text"] = {
        namespaces = {},
        types = { "StringBuilder" },
    },
}

local SYSTEM_TYPES = {
    Console = {
        fullName = "System.Console",
        kind = "class",
        constructable = false,
        members = {
            { kind = "method", name = "WriteLine", returnType = "Void" },
            { kind = "method", name = "Write", returnType = "Void" },
            { kind = "method", name = "ReadLine", returnType = "String" },
        },
    },
    Math = {
        fullName = "System.Math",
        kind = "class",
        constructable = false,
        members = {
            { kind = "method", name = "Abs", returnType = "Double" },
            { kind = "method", name = "Ceiling", returnType = "Double" },
            { kind = "method", name = "Floor", returnType = "Double" },
            { kind = "method", name = "Max", returnType = "Double" },
            { kind = "method", name = "Min", returnType = "Double" },
            { kind = "method", name = "Pow", returnType = "Double" },
            { kind = "method", name = "Round", returnType = "Double" },
            { kind = "method", name = "Sqrt", returnType = "Double" },
        },
    },
    List = {
        fullName = "System.Collections.Generic.List",
        kind = "class",
        constructable = true,
        members = {
            { kind = "method", name = "Add", returnType = "Void" },
            { kind = "method", name = "Clear", returnType = "Void" },
            { kind = "method", name = "Contains", returnType = "Boolean" },
            { kind = "property", name = "Count", type = "Int32" },
        },
    },
    Dictionary = {
        fullName = "System.Collections.Generic.Dictionary",
        kind = "class",
        constructable = true,
        members = {
            { kind = "method", name = "Add", returnType = "Void" },
            { kind = "method", name = "Clear", returnType = "Void" },
            { kind = "method", name = "ContainsKey", returnType = "Boolean" },
            { kind = "method", name = "TryGetValue", returnType = "Boolean" },
            { kind = "property", name = "Count", type = "Int32" },
        },
    },
    StringBuilder = {
        fullName = "System.Text.StringBuilder",
        kind = "class",
        constructable = true,
        members = {
            { kind = "method", name = "Append", returnType = "StringBuilder" },
            { kind = "method", name = "AppendLine", returnType = "StringBuilder" },
            { kind = "method", name = "Clear", returnType = "StringBuilder" },
            { kind = "property", name = "Length", type = "Int32" },
        },
    },
    Enumerable = {
        fullName = "System.Linq.Enumerable",
        kind = "class",
        constructable = false,
        members = {
            { kind = "method", name = "Select", returnType = "IEnumerable" },
            { kind = "method", name = "Where", returnType = "IEnumerable" },
            { kind = "method", name = "ToList", returnType = "List" },
        },
    },
}

local function startsWithIgnoreCase(value, prefix)
    local normalizedValue = string.lower(value or "")
    local normalizedPrefix = string.lower(prefix or "")

    if normalizedPrefix == "" then
        return true
    end

    return string.sub(normalizedValue, 1, #normalizedPrefix) == normalizedPrefix
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

local function resolveTypeInfo(typeExpression)
    local normalized = normalizeTypeName(typeExpression)
    if not normalized then
        return nil
    end

    if SYSTEM_TYPES[normalized] then
        return SYSTEM_TYPES[normalized]
    end

    local terminal = normalized:match("([%w_]+)$")
    if terminal and SYSTEM_TYPES[terminal] then
        local typeInfo = SYSTEM_TYPES[terminal]
        if typeInfo.fullName == normalized then
            return typeInfo
        end

        local namespaceName = normalized:match("^(.*)%.([%w_]+)$")
        local namespaceInfo = namespaceName and SYSTEM_NAMESPACES[namespaceName] or nil

        if namespaceInfo then
            for _, typeName in ipairs(namespaceInfo.types or {}) do
                if typeName == terminal then
                    return typeInfo
                end
            end
        end
    end

    return nil
end

local function memberKind(member)
    if member.kind == "method" then
        return "method"
    end

    if member.kind == "property" then
        return "property"
    end

    if member.kind == "field" then
        return "field"
    end

    return "member"
end

local function memberDetail(member)
    if member.kind == "method" then
        local returnType = member.returnType or "Void"
        return string.format("method %s -> %s", member.name, returnType)
    end

    if member.kind == "property" or member.kind == "field" then
        return string.format("%s : %s", member.kind, member.type or "Object")
    end

    return member.kind
end

local function sortCompletions(completions)
    table.sort(completions, function(a, b)
        if a.label == b.label then
            return a.kind < b.kind
        end

        return a.label < b.label
    end)

    return completions
end

local function makeCompletion(label, kind, detail, documentation)
    return {
        label = label,
        kind = kind,
        detail = detail,
        documentation = documentation,
        source = "system",
    }
end

function SystemCatalog.getNamespaceCompletions(prefix)
    local result = {}

    for _, namespaceName in ipairs(NAMESPACE_ORDER) do
        if startsWithIgnoreCase(namespaceName, prefix) then
            table.insert(result, makeCompletion(namespaceName, "namespace", "namespace", namespaceName))
        end
    end

    return sortCompletions(result)
end

function SystemCatalog.getTypeCompletions(prefix, constructableOnly)
    local result = {}

    for _, typeName in ipairs(TYPE_ORDER) do
        local typeInfo = SYSTEM_TYPES[typeName]
        if typeInfo then
            local constructable = typeInfo.constructable == true
            if (not constructableOnly or constructable) and (
                startsWithIgnoreCase(typeName, prefix)
                or startsWithIgnoreCase(typeInfo.fullName, prefix)
            ) then
                table.insert(result, makeCompletion(typeName, "type", typeInfo.kind, typeInfo.fullName))
            end
        end
    end

    return sortCompletions(result)
end

function SystemCatalog.getMemberCompletions(targetExpression, prefix)
    if type(targetExpression) ~= "string" then
        return {}
    end

    local target = targetExpression:gsub("%s+", "")
    if target == "" then
        return {}
    end

    local namespaceInfo = SYSTEM_NAMESPACES[target]
    if namespaceInfo then
        local result = {}

        for _, childNamespace in ipairs(namespaceInfo.namespaces or {}) do
            local label = childNamespace:match("([%w_]+)$") or childNamespace
            if startsWithIgnoreCase(label, prefix) then
                table.insert(result, makeCompletion(label, "namespace", "namespace", childNamespace))
            end
        end

        for _, typeName in ipairs(namespaceInfo.types or {}) do
            local typeInfo = SYSTEM_TYPES[typeName]
            if typeInfo and startsWithIgnoreCase(typeName, prefix) then
                table.insert(result, makeCompletion(typeName, "type", typeInfo.kind, typeInfo.fullName))
            end
        end

        return sortCompletions(result)
    end

    local typeInfo = resolveTypeInfo(target)
    if not typeInfo then
        return {}
    end

    local result = {}
    for _, member in ipairs(typeInfo.members or {}) do
        if startsWithIgnoreCase(member.name, prefix) then
            table.insert(result, makeCompletion(member.name, memberKind(member), memberDetail(member), typeInfo.fullName))
        end
    end

    return sortCompletions(result)
end

return SystemCatalog
