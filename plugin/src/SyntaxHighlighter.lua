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

local SyntaxHighlighter = {}

local DEFAULT_STYLES = {
    keyword = "#C586C0",
    identifier = "#9CDCFE",
    type = "#4EC9B0",
    method = "#DCDCAA",
    enum = "#B5CEA8",
    localVar = "#D7BA7D",
    namespace = "#C8C8C8",
    string = "#CE9178",
    number = "#B5CEA8",
    comment = "#6A9955",
    operator = "#D4D4D4",
    punctuation = "#D4D4D4",
}

local LIGHT_STYLES = {
    keyword = "#0000FF",
    identifier = "#001080",
    type = "#267F99",
    method = "#795E26",
    enum = "#2B91AF",
    localVar = "#A31515",
    namespace = "#7A3E9D",
    string = "#A31515",
    number = "#098658",
    comment = "#008000",
    operator = "#000000",
    punctuation = "#000000",
}

local function escapeRichText(text)
    local escaped = text
        :gsub("&", "&amp;")
        :gsub("<", "&lt;")
        :gsub(">", "&gt;")
        :gsub('"', "&quot;")
    return escaped
end

local function buildLineStarts(source)
    local starts = { 1 }
    for i = 1, #source do
        if string.sub(source, i, i) == "\n" then
            table.insert(starts, i + 1)
        end
    end
    return starts
end

local function lineColumnToIndex(lineStarts, line, column, sourceLength)
    local lineStart = lineStarts[line]
    if lineStart == nil then
        return sourceLength + 1
    end
    return lineStart + column - 1
end

local function normalizeCategory(tokenType)
    if tokenType == "interpolated_string" then
        return "string"
    end

    if tokenType == "keyword"
        or tokenType == "identifier"
        or tokenType == "string"
        or tokenType == "number"
        or tokenType == "comment"
        or tokenType == "operator"
        or tokenType == "punctuation" then
        return tokenType
    end

    return nil
end

local function isPrimitiveTypeKeyword(value)
    return value == "bool"
        or value == "byte"
        or value == "char"
        or value == "decimal"
        or value == "double"
        or value == "float"
        or value == "int"
        or value == "long"
        or value == "object"
        or value == "sbyte"
        or value == "short"
        or value == "string"
        or value == "uint"
        or value == "ulong"
        or value == "ushort"
        or value == "var"
end

local function classifyIdentifierTokens(tokens)
    local identifierKinds = {}
    local enumTypes = {}
    local localVars = {}

    for i, token in ipairs(tokens) do
        if token.type == "keyword" and token.value == "enum" then
            local nextToken = tokens[i + 1]
            if nextToken and nextToken.type == "identifier" then
                enumTypes[nextToken.value] = true
                identifierKinds[i + 1] = "type"
            end
        end
    end

    for i, token in ipairs(tokens) do
        if token.type ~= "identifier" then
            continue
        end

        local prev = tokens[i - 1]
        local prev2 = tokens[i - 2]
        local nextTok = tokens[i + 1]

        if prev and prev.type == "keyword" then
            if prev.value == "class"
                or prev.value == "struct"
                or prev.value == "interface"
                or prev.value == "new" then
                identifierKinds[i] = "type"
                continue
            elseif prev.value == "namespace" or prev.value == "using" then
                identifierKinds[i] = "namespace"
                continue
            end
        end

        if nextTok and nextTok.type == "punctuation" and nextTok.value == "(" then
            identifierKinds[i] = "method"
            continue
        end

        if prev and prev.type == "punctuation" and prev.value == "." then
            local inEnumChain = false

            if prev2 and prev2.type == "identifier" and (prev2.value == "Enum" or enumTypes[prev2.value]) then
                inEnumChain = true
            else
                local prev3 = tokens[i - 3]
                local prev4 = tokens[i - 4]
                if prev3 and prev3.type == "punctuation" and prev3.value == "."
                    and prev4 and prev4.type == "identifier" and prev4.value == "Enum" then
                    inEnumChain = true
                end
            end

            if inEnumChain then
                identifierKinds[i] = "enum"
                continue
            end
        end

        local isLocalDeclaration = false
        if prev and prev.type == "keyword" and isPrimitiveTypeKeyword(prev.value) then
            isLocalDeclaration = true
        elseif prev and prev.type == "identifier" then
            local nextValue = nextTok and nextTok.value or nil
            local nextType = nextTok and nextTok.type or nil
            if nextType == "operator" and (nextValue == "=" or nextValue == "?" or nextValue == "??") then
                isLocalDeclaration = true
            elseif nextType == "punctuation" and (nextValue == "," or nextValue == ";" or nextValue == ")") then
                isLocalDeclaration = true
            end
        end

        if isLocalDeclaration then
            localVars[token.value] = true
            identifierKinds[i] = "localVar"
            continue
        end

        if localVars[token.value] and not (prev and prev.type == "punctuation" and prev.value == ".") then
            identifierKinds[i] = "localVar"
            continue
        end

        if enumTypes[token.value] then
            identifierKinds[i] = "type"
            continue
        end
    end

    return identifierKinds
end

local function makeStyleMap(options)
    local base = DEFAULT_STYLES

    local theme = options and options.theme or nil
    if theme ~= nil then
        local t = string.lower(tostring(theme))
        if t == "light" then
            base = LIGHT_STYLES
        end
    end

    local styleMap = {}
    for k, v in pairs(base) do
        styleMap[k] = v
    end

    if options and options.styles then
        for k, v in pairs(options.styles) do
            styleMap[k] = v
        end
    end

    return styleMap
end

local function styleText(text, category, styleMap)
    local escaped = escapeRichText(text)
    local color = category and styleMap[category] or nil

    if color == nil then
        return escaped
    end

    return '<font color="' .. color .. '">' .. escaped .. "</font>"
end

function SyntaxHighlighter.highlight(source, options)
    source = source or ""
    options = options or {}

    local tokens = Lexer.tokenize(source, { preserveComments = true })
    local styleMap = makeStyleMap(options)
    local lineStarts = buildLineStarts(source)
    local identifierKinds = classifyIdentifierTokens(tokens)

    local out = {}
    local cursor = 1
    local sourceLength = #source

    for i, token in ipairs(tokens) do
        if token.type == "eof" then
            break
        end

        local startIndex = lineColumnToIndex(lineStarts, token.line, token.column, sourceLength)
        if startIndex > sourceLength + 1 then
            startIndex = sourceLength + 1
        end

        if startIndex > cursor then
            local gap = string.sub(source, cursor, startIndex - 1)
            table.insert(out, escapeRichText(gap))
        end

        local tokenLength = #token.value
        local tokenEnd = startIndex + tokenLength - 1
        if tokenEnd > sourceLength then
            tokenEnd = sourceLength
        end

        local tokenText = string.sub(source, startIndex, tokenEnd)
        local category = normalizeCategory(token.type)

        if token.type == "identifier" and identifierKinds[i] ~= nil then
            category = identifierKinds[i]
        end

        table.insert(out, styleText(tokenText, category, styleMap))

        cursor = tokenEnd + 1
    end

    if cursor <= sourceLength then
        table.insert(out, escapeRichText(string.sub(source, cursor)))
    end

    return table.concat(out)
end

return SyntaxHighlighter
