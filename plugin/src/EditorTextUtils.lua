local EditorTextUtils = {}

local function clampCursor(text, cursorPos)
    if type(cursorPos) ~= "number" then
        return #text + 1
    end

    cursorPos = math.floor(cursorPos)

    if cursorPos < 1 then
        return #text + 1
    end

    if cursorPos > #text + 1 then
        return #text + 1
    end

    return cursorPos
end

local function findLineStart(text, cursorPos)
    if cursorPos <= 1 then
        return 1
    end

    local prefix = text:sub(1, cursorPos - 1)
    local lastNewline = prefix:match(".*()\n")
    return lastNewline and (lastNewline + 1) or 1
end

-- If the user just typed a '}' on an otherwise-whitespace line, dedent by 1 indent level.
-- Returns: newText, newCursorPos, changed
function EditorTextUtils.autoDedentClosingBrace(text, cursorPos, indentText)
    text = text or ""
    indentText = indentText or "    "
    cursorPos = clampCursor(text, cursorPos)

    if cursorPos < 2 then
        return text, cursorPos, false
    end

    if text:sub(cursorPos - 1, cursorPos - 1) ~= "}" then
        return text, cursorPos, false
    end

    local lineStart = findLineStart(text, cursorPos)
    local lineToCursor = text:sub(lineStart, cursorPos - 1)

    -- Only whitespace then '}'
    if not lineToCursor:match("^%s*%}$") then
        return text, cursorPos, false
    end

    local indent = lineToCursor:match("^([ \t]+)%}")
    if not indent or indent == "" then
        return text, cursorPos, false
    end

    local removeCount = math.min(#indent, #indentText)
    local newIndent = indent:sub(removeCount + 1)

    local before = text:sub(1, lineStart - 1)
    local after = text:sub(cursorPos)
    local newLine = newIndent .. "}"
    local newText = before .. newLine .. after
    local newCursor = (lineStart - 1) + #newLine + 1

    return newText, newCursor, true
end

-- If the user just inserted a newline, compute the indentation text that should be inserted at the new cursor.
-- Pure helper: does not mutate text.
function EditorTextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, tabText)
    prevText = prevText or ""
    newText = newText or ""
    tabText = tabText or "    "

    if type(prevCursor) ~= "number" or type(newCursor) ~= "number" then
        return ""
    end

    prevCursor = math.floor(prevCursor)
    newCursor = math.floor(newCursor)

    -- Minimal v1: detect a single '\n' insertion at the cursor.
    if newCursor <= 1 or newCursor ~= prevCursor + 1 then
        return ""
    end

    if newText:sub(newCursor - 1, newCursor - 1) ~= "\n" then
        return ""
    end

    local expected = prevText:sub(1, prevCursor - 1) .. "\n" .. prevText:sub(prevCursor)
    if newText ~= expected then
        return ""
    end

    -- Previous line is the line that ends at the inserted newline.
    local lineStart = findLineStart(newText, newCursor - 1)
    local prevLine = newText:sub(lineStart, newCursor - 2)

    local leadingWhitespace = prevLine:match("^([ \t]*)") or ""
    local trimmed = prevLine:match("^(.-)%s*$") or ""

    if trimmed ~= "" and trimmed:sub(-1) == "{" then
        return leadingWhitespace .. tabText
    end

    return ""
end

return EditorTextUtils
