-- LUSharp Lexer: Tokenizes C# source into a stream of typed tokens

local Lexer = {}

local KEYWORDS = {
    "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch",
    "char", "class", "const", "continue", "decimal", "default", "delegate",
    "do", "double", "else", "enum", "event", "false", "finally", "float",
    "for", "foreach", "get", "if", "in", "int", "interface", "internal",
    "is", "long", "namespace", "new", "null", "object", "operator", "out",
    "override", "params", "private", "protected", "public", "readonly",
    "ref", "return", "sealed", "set", "short", "static", "string", "struct",
    "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
    "unsafe", "ushort", "using", "var", "virtual", "void", "while",
}

local KEYWORD_SET = {}
for _, kw in ipairs(KEYWORDS) do
    KEYWORD_SET[kw] = true
end

local function isAlpha(c)
    return (c >= "a" and c <= "z") or (c >= "A" and c <= "Z") or c == "_"
end

local function isDigit(c)
    return c >= "0" and c <= "9"
end

local function isAlphaNumeric(c)
    return isAlpha(c) or isDigit(c)
end

local function makeToken(type, value, line, column)
    return { type = type, value = value, line = line, column = column }
end

function Lexer.tokenize(source, options)
    options = options or {}
    local preserveComments = options.preserveComments or false

    local tokens = {}
    local pos = 1
    local line = 1
    local column = 1
    local len = #source

    local function peek(offset)
        offset = offset or 0
        local i = pos + offset
        if i > len then return "\0" end
        return string.sub(source, i, i)
    end

    local function advance()
        local c = peek()
        pos += 1
        if c == "\n" then
            line += 1
            column = 1
        else
            column += 1
        end
        return c
    end

    local function addToken(type, value, startLine, startCol)
        table.insert(tokens, makeToken(type, value, startLine, startCol))
    end

    local function readWhile(predicate)
        local start = pos
        while pos <= len and predicate(peek()) do
            advance()
        end
        return string.sub(source, start, pos - 1)
    end

    local function readString(quote)
        local startLine, startCol = line, column
        advance() -- skip opening quote
        local result = quote
        while pos <= len do
            local c = peek()
            if c == "\\" then
                result ..= advance() -- backslash
                if pos <= len then
                    result ..= advance() -- escaped char
                end
            elseif c == quote then
                result ..= advance()
                break
            elseif c == "\n" then
                break -- unterminated string
            else
                result ..= advance()
            end
        end
        return result, startLine, startCol
    end

    local function readInterpolatedString()
        local startLine, startCol = line, column
        advance() -- skip $
        advance() -- skip "
        local result = '$"'
        local depth = 0
        while pos <= len do
            local c = peek()
            if c == "\\" then
                result ..= advance()
                if pos <= len then result ..= advance() end
            elseif c == "{" then
                depth += 1
                result ..= advance()
            elseif c == "}" then
                depth -= 1
                result ..= advance()
            elseif c == '"' and depth == 0 then
                result ..= advance()
                break
            elseif c == "\n" then
                break
            else
                result ..= advance()
            end
        end
        return result, startLine, startCol
    end

    local TWO_CHAR_OPS = {
        ["=="] = true, ["!="] = true, ["<="] = true, [">="] = true,
        ["&&"] = true, ["||"] = true, ["??"] = true, ["?."] = true,
        ["=>"] = true, ["+="] = true, ["-="] = true, ["*="] = true,
        ["/="] = true, ["%="] = true, ["++"] = true, ["--"] = true,
    }

    local SINGLE_OPS = {
        ["+"] = true, ["-"] = true, ["*"] = true, ["/"] = true,
        ["%"] = true, ["="] = true, ["!"] = true, ["<"] = true,
        [">"] = true, ["&"] = true, ["|"] = true, ["^"] = true,
        ["~"] = true, ["?"] = true,
    }

    local PUNCTUATION = {
        ["("] = true, [")"] = true, ["{"] = true, ["}"] = true,
        ["["] = true, ["]"] = true, [";"] = true, [","] = true,
        ["."] = true, [":"] = true,
    }

    while pos <= len do
        local c = peek()

        -- Skip whitespace
        if c == " " or c == "\t" or c == "\r" or c == "\n" then
            advance()

        -- Comments
        elseif c == "/" and peek(1) == "/" then
            local startLine, startCol = line, column
            advance() -- /
            advance() -- /
            local comment = "//"
            while pos <= len and peek() ~= "\n" do
                comment ..= advance()
            end
            if preserveComments then
                addToken("comment", comment, startLine, startCol)
            end

        elseif c == "/" and peek(1) == "*" then
            local startLine, startCol = line, column
            advance() -- /
            advance() -- *
            local comment = "/*"
            while pos <= len do
                if peek() == "*" and peek(1) == "/" then
                    comment ..= advance() -- *
                    comment ..= advance() -- /
                    break
                else
                    comment ..= advance()
                end
            end
            if preserveComments then
                addToken("comment", comment, startLine, startCol)
            end

        -- Interpolated strings
        elseif c == "$" and peek(1) == '"' then
            local value, sl, sc = readInterpolatedString()
            addToken("interpolated_string", value, sl, sc)

        -- Verbatim strings
        elseif c == "@" and peek(1) == '"' then
            local startLine, startCol = line, column
            advance() -- @
            advance() -- "
            local result = '@"'
            while pos <= len do
                local ch = peek()
                if ch == '"' then
                    if peek(1) == '"' then
                        result ..= advance() .. advance() -- escaped ""
                    else
                        result ..= advance()
                        break
                    end
                else
                    result ..= advance()
                end
            end
            addToken("string", result, startLine, startCol)

        -- Regular strings
        elseif c == '"' then
            local value, sl, sc = readString('"')
            addToken("string", value, sl, sc)

        -- Char literals
        elseif c == "'" then
            local value, sl, sc = readString("'")
            addToken("string", value, sl, sc)

        -- Numbers
        elseif isDigit(c) then
            local startLine, startCol = line, column
            local num = ""

            -- Hex
            if c == "0" and (peek(1) == "x" or peek(1) == "X") then
                num = advance() .. advance() -- 0x
                num ..= readWhile(function(ch)
                    return isDigit(ch) or (ch >= "a" and ch <= "f") or (ch >= "A" and ch <= "F")
                end)
            else
                num = readWhile(isDigit)
                if peek() == "." and isDigit(peek(1)) then
                    num ..= advance() -- .
                    num ..= readWhile(isDigit)
                end
            end

            -- Suffix (f, d, m, L, UL, etc.)
            local suffix = peek()
            if suffix == "f" or suffix == "F" or suffix == "d" or suffix == "D"
                or suffix == "m" or suffix == "M" or suffix == "L" or suffix == "U" then
                num ..= advance()
                if peek() == "L" then num ..= advance() end
            end

            addToken("number", num, startLine, startCol)

        -- Identifiers and keywords
        elseif isAlpha(c) then
            local startLine, startCol = line, column
            local word = readWhile(isAlphaNumeric)
            if KEYWORD_SET[word] then
                addToken("keyword", word, startLine, startCol)
            else
                addToken("identifier", word, startLine, startCol)
            end

        -- Two-char operators (check before single-char)
        elseif SINGLE_OPS[c] then
            local startLine, startCol = line, column
            local twoChar = c .. peek(1)
            if TWO_CHAR_OPS[twoChar] then
                advance()
                advance()
                addToken("operator", twoChar, startLine, startCol)
            else
                addToken("operator", advance(), startLine, startCol)
            end

        -- Punctuation
        elseif PUNCTUATION[c] then
            local startLine, startCol = line, column
            addToken("punctuation", advance(), startLine, startCol)

        else
            -- Unknown character — skip
            advance()
        end
    end

    addToken("eof", "", line, column)
    return tokens
end

return Lexer
