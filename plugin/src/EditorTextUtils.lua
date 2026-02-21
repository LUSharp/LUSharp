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

-- Resolves tab key behavior based on completion visibility and selection state.
function EditorTextUtils.resolveTabAction(completionVisible, hasSelection)
    if completionVisible and hasSelection then
        return "acceptCompletion"
    end

    return "insertIndent"
end

function EditorTextUtils.normalizeSnippetText(snippetText)
    if type(snippetText) ~= "string" then
        return ""
    end

    local normalized = snippetText

    normalized = normalized:gsub("%${%d+:([^}]*)}", "%1")
    normalized = normalized:gsub("%${%d+}", "")
    normalized = normalized:gsub("%$%d+", "")

    return normalized
end

function EditorTextUtils.getCompletionReplacePrefix(beforeText, insertText)
    beforeText = beforeText or ""

    local identifierPrefix = beforeText:match("([%a_][%w_]*)$") or ""

    if type(insertText) ~= "string" or string.find(insertText, ".", 1, true) == nil then
        return identifierPrefix
    end

    local dottedPrefix = beforeText:match("([%a_][%w_%.]*)$")
    if type(dottedPrefix) == "string" and dottedPrefix ~= "" then
        return dottedPrefix
    end

    return identifierPrefix
end

-- If the user just inserted a newline, compute the indentation text that should be inserted at the new cursor.
-- Pure helper: does not mutate text.
function EditorTextUtils.computeAutoIndentInsertion(prevText, prevCursor, newText, newCursor, tabText)
    prevText = prevText or ""
    newText = newText or ""
    tabText = tabText or "    "

    local function normalizeCursor(text, cursorPos)
        if type(cursorPos) ~= "number" then
            return nil
        end

        cursorPos = math.floor(cursorPos)
        if cursorPos < 1 then
            return 1
        end

        local maxPos = #text + 1
        if cursorPos > maxPos then
            return maxPos
        end

        return cursorPos
    end

    prevCursor = normalizeCursor(prevText, prevCursor)
    newCursor = normalizeCursor(newText, newCursor)
    if not prevCursor or not newCursor then
        return ""
    end

    local function findSingleSplice(oldText, updatedText)
        if oldText == updatedText then
            return nil
        end

        local oldLen = #oldText
        local newLen = #updatedText

        local prefixLen = 0
        local minLen = math.min(oldLen, newLen)
        while prefixLen < minLen do
            local i = prefixLen + 1
            if oldText:sub(i, i) ~= updatedText:sub(i, i) then
                break
            end
            prefixLen += 1
        end

        local oldEnd = oldLen
        local newEnd = newLen
        while oldEnd > prefixLen and newEnd > prefixLen do
            if oldText:sub(oldEnd, oldEnd) ~= updatedText:sub(newEnd, newEnd) then
                break
            end
            oldEnd -= 1
            newEnd -= 1
        end

        local startPos = prefixLen + 1
        local removedText = oldText:sub(startPos, oldEnd)
        local insertedText = updatedText:sub(startPos, newEnd)
        return startPos, removedText, insertedText
    end

    local insertedNewlinePos

    if prevText ~= newText then
        -- Text changed: detect a single-splice replacement with a lone '\n'.
        local startPos, _removedText, insertedText = findSingleSplice(prevText, newText)
        if insertedText ~= "\n" then
            return ""
        end

        -- Only insert auto-indent when the cursor is at the insertion point.
        if newCursor ~= startPos + 1 then
            return ""
        end

        insertedNewlinePos = startPos
    else
        return ""
    end

    -- Previous line is the line that ends at the inserted newline.
    local lineStart = findLineStart(newText, insertedNewlinePos)
    local prevLine = newText:sub(lineStart, insertedNewlinePos - 1)

    local leadingWhitespace = prevLine:match("^([ \t]*)") or ""
    local trimmed = prevLine:match("^(.-)%s*$") or ""

    local indent = leadingWhitespace
    if trimmed ~= "" and trimmed:sub(-1) == "{" then
        indent ..= tabText
    end

    return indent
end

return EditorTextUtils
