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
    string = "#CE9178",
    number = "#B5CEA8",
    comment = "#6A9955",
    operator = "#D4D4D4",
    punctuation = "#D4D4D4",
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

local function makeStyleMap(options)
    local styleMap = {}
    for k, v in pairs(DEFAULT_STYLES) do
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

    local out = {}
    local cursor = 1
    local sourceLength = #source

    for _, token in ipairs(tokens) do
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
        table.insert(out, styleText(tokenText, category, styleMap))

        cursor = tokenEnd + 1
    end

    if cursor <= sourceLength then
        table.insert(out, escapeRichText(string.sub(source, cursor)))
    end

    return table.concat(out)
end

return SyntaxHighlighter
