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

local function classifyForwardDeleteChar(char)
    if char == "\n" then
        return "newline"
    end

    if char == " " or char == "\t" or char == "\r" or char == "\f" or char == "\v" then
        return "space"
    end

    if char:match("[%w_]") then
        return "word"
    end

    return "punctuation"
end

local function findLineEndExclusive(text, cursorPos)
    local nextNewline = text:find("\n", math.max(1, math.floor(cursorPos or 1)), true)
    return nextNewline or (#text + 1)
end

local function isHorizontalWhitespace(char)
    return char == " " or char == "\t" or char == "\r" or char == "\f" or char == "\v"
end

local function findTokenCoveringColumn(tokens, column)
    for _, token in ipairs(tokens) do
        if token.type ~= "eof" then
            local value = tostring(token.value or "")
            if #value > 0 then
                local startCol = tonumber(token.column) or 0
                local endCol = startCol + #value - 1
                if column >= startCol and column <= endCol then
                    return token, startCol, endCol
                end
            end
        end
    end

    return nil, nil, nil
end

local function deleteLineColumnRange(text, lineStart, startCol, endCol)
    local globalStart = lineStart + startCol - 1
    local globalEndExclusive = lineStart + endCol
    local newText = text:sub(1, globalStart - 1) .. text:sub(globalEndExclusive)
    return newText, globalStart, true
end

function EditorTextUtils.deleteNextWordSegment(text, cursorPos)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    if cursorPos > #text then
        return text, cursorPos, false
    end

    local firstChar = text:sub(cursorPos, cursorPos)
    local segmentKind = classifyForwardDeleteChar(firstChar)

    if segmentKind == "newline" then
        return text, cursorPos, false
    end

    local lineStart = findLineStart(text, cursorPos)
    local lineEndExclusive = findLineEndExclusive(text, cursorPos)
    local lineText = text:sub(lineStart, lineEndExclusive - 1)
    local column = cursorPos - lineStart + 1

    if isHorizontalWhitespace(firstChar) then
        local endCol = column
        while endCol < #lineText do
            local nextChar = lineText:sub(endCol + 1, endCol + 1)
            if not isHorizontalWhitespace(nextChar) then
                break
            end
            endCol += 1
        end

        return deleteLineColumnRange(text, lineStart, column, endCol)
    end

    local tokens = Lexer.tokenize(lineText)
    local _token, tokenStartCol, tokenEndCol = findTokenCoveringColumn(tokens, column)

    if type(tokenStartCol) == "number" and type(tokenEndCol) == "number" then
        local endCol = math.max(column, tokenEndCol)
        return deleteLineColumnRange(text, lineStart, column, endCol)
    end

    return deleteLineColumnRange(text, lineStart, column, column)
end

function EditorTextUtils.computeCtrlDeleteEdit(text, cursorPos, selectionStart)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    local normalizedSelection = -1
    if type(selectionStart) == "number" then
        selectionStart = math.floor(selectionStart)
        if selectionStart ~= -1 then
            normalizedSelection = clampCursor(text, selectionStart)
        end
    end

    if normalizedSelection ~= -1 and normalizedSelection ~= cursorPos then
        local startPos = math.min(cursorPos, normalizedSelection)
        local endPos = math.max(cursorPos, normalizedSelection)
        local newText = text:sub(1, startPos - 1) .. text:sub(endPos)
        return newText, startPos, -1, true
    end

    local newText, newCursor, changed = EditorTextUtils.deleteNextWordSegment(text, cursorPos)
    return newText, newCursor, -1, changed
end

function EditorTextUtils.deletePrevWordSegment(text, cursorPos)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    if cursorPos <= 1 then
        return text, cursorPos, false
    end

    local firstCharPos = cursorPos - 1
    local firstChar = text:sub(firstCharPos, firstCharPos)
    local segmentKind = classifyForwardDeleteChar(firstChar)

    if segmentKind == "newline" then
        return text, cursorPos, false
    end

    local lineStart = findLineStart(text, cursorPos)
    local lineEndExclusive = findLineEndExclusive(text, cursorPos)
    local lineText = text:sub(lineStart, lineEndExclusive - 1)
    local column = cursorPos - lineStart + 1
    local leftColumn = column - 1

    if isHorizontalWhitespace(firstChar) then
        local startCol = leftColumn
        while startCol > 1 do
            local prevChar = lineText:sub(startCol - 1, startCol - 1)
            if not isHorizontalWhitespace(prevChar) then
                break
            end
            startCol -= 1
        end

        return deleteLineColumnRange(text, lineStart, startCol, leftColumn)
    end

    local tokens = Lexer.tokenize(lineText)
    local _token, tokenStartCol, tokenEndCol = findTokenCoveringColumn(tokens, leftColumn)

    if type(tokenStartCol) == "number" and type(tokenEndCol) == "number" then
        local startCol = tokenStartCol
        local endCol = math.min(leftColumn, tokenEndCol)
        return deleteLineColumnRange(text, lineStart, startCol, endCol)
    end

    return deleteLineColumnRange(text, lineStart, leftColumn, leftColumn)
end

function EditorTextUtils.computeCtrlBackspaceEdit(text, cursorPos, selectionStart)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    local normalizedSelection = -1
    if type(selectionStart) == "number" then
        selectionStart = math.floor(selectionStart)
        if selectionStart ~= -1 then
            normalizedSelection = clampCursor(text, selectionStart)
        end
    end

    if normalizedSelection ~= -1 and normalizedSelection ~= cursorPos then
        local startPos = math.min(cursorPos, normalizedSelection)
        local endPos = math.max(cursorPos, normalizedSelection)
        local newText = text:sub(1, startPos - 1) .. text:sub(endPos)
        return newText, startPos, -1, true
    end

    local newText, newCursor, changed = EditorTextUtils.deletePrevWordSegment(text, cursorPos)
    return newText, newCursor, -1, changed
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

    if type(indentText) == "string" and indentText ~= "" then
        local tokens = Lexer.tokenize(text:sub(1, lineStart - 1))
        local depth = 0
        for _, token in ipairs(tokens) do
            if token.type == "punctuation" then
                if token.value == "{" then
                    depth += 1
                elseif token.value == "}" then
                    depth = math.max(0, depth - 1)
                end
            end
        end

        local targetIndentLength = math.max(0, depth - 1) * #indentText
        if depth > 0 and #indent <= targetIndentLength then
            return text, cursorPos, false
        end
    end

    local newIndent

    if type(indentText) == "string" and indentText ~= "" and #indent >= #indentText and indent:sub(-#indentText) == indentText then
        newIndent = indent:sub(1, #indent - #indentText)
    elseif indent:sub(-1) == "\t" then
        newIndent = indent:sub(1, #indent - 1)
    else
        local removeCount = math.min(#indent, #indentText)
        newIndent = indent:sub(1, #indent - removeCount)
    end

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
        return indent
    end

    if trimmed == "" then
        local tokens = Lexer.tokenize(newText:sub(1, insertedNewlinePos - 1))
        local depth = 0
        for _, token in ipairs(tokens) do
            if token.type == "punctuation" then
                if token.value == "{" then
                    depth += 1
                elseif token.value == "}" then
                    depth = math.max(0, depth - 1)
                end
            end
        end

        if depth > 0 then
            return string.rep(tabText, depth)
        end
    end

    return indent
end

local function normalizeKeyName(keyCode)
    local keyName = ""
    if type(keyCode) == "string" then
        keyName = keyCode
    elseif keyCode ~= nil then
        keyName = tostring(keyCode)
        local enumName = keyName:match("Enum%.KeyCode%.([%w_]+)$")
        if enumName then
            keyName = enumName
        end
    end

    return string.upper(keyName)
end

function EditorTextUtils.isUndoRedoShortcut(keyCode, ctrlDown, altDown)
    if ctrlDown ~= true or altDown == true then
        return false
    end

    local keyName = normalizeKeyName(keyCode)
    return keyName == "Z" or keyName == "Y"
end

function EditorTextUtils.isWordDeleteShortcut(keyCode, ctrlDown, altDown)
    local keyName = normalizeKeyName(keyCode)

    if keyName == "BACKSPACE" then
        return ctrlDown == true
    end

    if keyName == "DELETE" then
        return ctrlDown == true or altDown == true
    end

    return false
end

function EditorTextUtils.sanitizeWordDeleteSelection(cursorPos, selectionStart, keyCode, ctrlDown, altDown)
    if not EditorTextUtils.isWordDeleteShortcut(keyCode, ctrlDown, altDown) then
        return selectionStart
    end

    -- Force token-based behavior for word-delete shortcuts.
    -- This prevents stale SelectionStart state from deleting full lines.
    return -1
end

function EditorTextUtils.isSelectionDeleteSplice(cursorPos, selectionStart, spliceStart)
    if type(cursorPos) ~= "number" or type(selectionStart) ~= "number" then
        return false
    end

    if selectionStart == -1 or selectionStart == cursorPos then
        return false
    end

    local expectedStart = math.min(cursorPos, selectionStart)
    if type(spliceStart) == "number" then
        return spliceStart == expectedStart
    end

    return true
end

function EditorTextUtils.shouldRepairWordDelete(keyCode, hadSelection, removedText, insertedText, shortcutDetected)
    if hadSelection == true then
        return false
    end

    local keyName = normalizeKeyName(keyCode)
    if keyName ~= "BACKSPACE" and keyName ~= "DELETE" then
        return false
    end

    if type(removedText) ~= "string" or removedText == "" then
        return false
    end

    if insertedText ~= nil and insertedText ~= "" then
        return false
    end

    if shortcutDetected == true then
        -- Shortcut Delete should repair suspicious multi-char native tails,
        -- but not valid single-char punctuation deletes (e.g. ';').
        if keyName == "DELETE" then
            return #removedText > 1
        end

        -- Backspace repair stays conservative to avoid stale-cursor overcorrections.
        return #removedText <= 1
    end

    -- Non-shortcut fallback: Delete can miss modifier-state and wipe expression tails.
    -- Repair suspicious multi-char deletes; plain Delete remains single-char.
    if keyName == "DELETE" then
        return #removedText > 1
    end

    -- Backspace fallback remains strict: only newline-spanning corruption.
    return removedText:find("\n", 1, true) ~= nil
end

function EditorTextUtils.resolveWordDeleteDirection(beforeCursor, afterCursor)
    if type(beforeCursor) ~= "number" or type(afterCursor) ~= "number" then
        return "backward"
    end

    beforeCursor = math.floor(beforeCursor)
    afterCursor = math.floor(afterCursor)

    if afterCursor < beforeCursor then
        return "backward"
    end

    return "forward"
end

function EditorTextUtils.resolveWordDeleteCursorFromSplice(startPos, removedText, direction)
    if type(startPos) ~= "number" then
        return nil
    end

    startPos = math.floor(startPos)
    if startPos < 1 then
        return nil
    end

    removedText = type(removedText) == "string" and removedText or ""
    direction = direction == "forward" and "forward" or "backward"

    if direction == "forward" then
        return startPos
    end

    return startPos + #removedText
end

local function isEscapedQuote(text, index, lineStart)
    local slashCount = 0
    local i = index - 1
    while i >= lineStart do
        if text:sub(i, i) ~= "\\" then
            break
        end
        slashCount += 1
        i -= 1
    end

    return (slashCount % 2) == 1
end

local function isIdentifierChar(char)
    if type(char) ~= "string" or #char == 0 then
        return false
    end

    return char:match("[%w_]") ~= nil
end

local function buildStopCharSet(stopChars)
    if type(stopChars) == "table" then
        local set = {}
        for key, value in pairs(stopChars) do
            if type(key) == "string" and #key == 1 then
                if value == true then
                    set[key] = true
                end
            elseif value == true and type(key) == "number" then
                -- ignore numeric keys with boolean true
            elseif type(value) == "string" and #value == 1 then
                set[value] = true
            end
        end
        return set
    end

    if type(stopChars) == "string" then
        local set = {}
        for i = 1, #stopChars do
            set[stopChars:sub(i, i)] = true
        end
        return set
    end

    return nil
end

local function isScanStopChar(ch, stopCharSet)
    if ch == "\n" or isHorizontalWhitespace(ch) then
        return true
    end

    return type(stopCharSet) == "table" and stopCharSet[ch] == true
end

local function resolveIdentifierPivot(text, cursorPos, minPos, maxPos, stopCharSet)
    minPos = math.max(1, math.floor(minPos or 1))
    maxPos = math.max(minPos, math.floor(maxPos or #text))

    if type(cursorPos) ~= "number" then
        return nil
    end

    cursorPos = math.floor(cursorPos)
    if cursorPos < minPos or cursorPos > maxPos then
        return nil
    end

    local function isIdentifierAt(pos)
        return pos >= minPos and pos <= maxPos and isIdentifierChar(text:sub(pos, pos))
    end

    local function scanForIdentifier(direction)
        local pos = cursorPos + direction
        while pos >= minPos and pos <= maxPos do
            local ch = text:sub(pos, pos)
            if isIdentifierChar(ch) then
                return pos
            end
            if isScanStopChar(ch, stopCharSet) then
                break
            end
            pos += direction
        end
        return nil
    end

    if isIdentifierAt(cursorPos) then
        return cursorPos
    end

    local currentChar = text:sub(cursorPos, cursorPos)
    local leftPos = cursorPos - 1
    local rightPos = cursorPos + 1

    if type(stopCharSet) == "table" and stopCharSet[currentChar] == true then
        return nil
    end

    if currentChar == "." or currentChar == ":" then
        return (isIdentifierAt(rightPos) and rightPos)
            or (isIdentifierAt(leftPos) and leftPos)
            or scanForIdentifier(1)
            or scanForIdentifier(-1)
    end

    if currentChar == "(" or currentChar == ")" or currentChar == "," or currentChar == ";" then
        return scanForIdentifier(-1) or scanForIdentifier(1)
    end

    if currentChar == "\"" or currentChar == "'" or currentChar == "$" then
        return scanForIdentifier(-1) or scanForIdentifier(1)
    end

    if currentChar == "{" then
        return scanForIdentifier(1) or scanForIdentifier(-1)
    end

    if currentChar == "}" then
        return scanForIdentifier(-1) or scanForIdentifier(1)
    end

    return (isIdentifierAt(rightPos) and rightPos)
        or (isIdentifierAt(leftPos) and leftPos)
        or scanForIdentifier(1)
        or scanForIdentifier(-1)
end

local function resolveIdentifierRange(text, pivot, minPos, maxPos)
    if type(pivot) ~= "number" then
        return nil, nil
    end

    pivot = math.floor(pivot)
    minPos = math.max(1, math.floor(minPos or 1))
    maxPos = math.max(minPos, math.floor(maxPos or #text))

    if pivot < minPos or pivot > maxPos or not isIdentifierChar(text:sub(pivot, pivot)) then
        return nil, nil
    end

    local startPos = pivot
    while startPos > minPos do
        local prevChar = text:sub(startPos - 1, startPos - 1)
        if not isIdentifierChar(prevChar) then
            break
        end
        startPos -= 1
    end

    local endPosExclusive = pivot + 1
    while endPosExclusive <= maxPos do
        local nextChar = text:sub(endPosExclusive, endPosExclusive)
        if not isIdentifierChar(nextChar) then
            break
        end
        endPosExclusive += 1
    end

    return startPos, endPosExclusive
end

local function resolveTokenRangeAtCursor(text, cursorPos, minPos, maxPos, stopCharSet)
    if type(cursorPos) ~= "number" then
        return nil, nil
    end

    cursorPos = math.floor(cursorPos)
    minPos = math.max(1, math.floor(minPos or 1))
    maxPos = math.max(minPos, math.floor(maxPos or #text))

    if cursorPos < minPos or cursorPos > maxPos then
        return nil, nil
    end

    local cursorChar = text:sub(cursorPos, cursorPos)
    if isScanStopChar(cursorChar, stopCharSet) then
        return nil, nil
    end

    if not isIdentifierChar(cursorChar) then
        return nil, nil
    end

    local pivot = resolveIdentifierPivot(text, cursorPos, minPos, maxPos, stopCharSet)
    return resolveIdentifierRange(text, pivot, minPos, maxPos)
end

function EditorTextUtils.resolveRetokenizeCursorForSelection(text, selectionStart, selectionEndExclusive, preferredCursors, stopChars)
    if type(text) ~= "string" then
        return nil
    end

    local textLength = #text
    if textLength <= 0 then
        return nil
    end

    if type(selectionStart) ~= "number" or type(selectionEndExclusive) ~= "number" then
        return nil
    end

    local startPos = math.max(1, math.floor(selectionStart))
    local endPosExclusive = math.min(textLength + 1, math.floor(selectionEndExclusive))
    if endPosExclusive <= startPos then
        return nil
    end

    local endPosInclusive = endPosExclusive - 1
    local stopCharSet = buildStopCharSet(stopChars)

    local candidates = {}
    if type(preferredCursors) == "table" then
        local numericIndexes = {}
        for key, _ in pairs(preferredCursors) do
            if type(key) == "number" and key >= 1 and key % 1 == 0 then
                table.insert(numericIndexes, key)
            end
        end
        table.sort(numericIndexes)

        for _, key in ipairs(numericIndexes) do
            local value = preferredCursors[key]
            if type(value) == "number" then
                table.insert(candidates, math.floor(value))
            end
        end
    elseif type(preferredCursors) == "number" then
        table.insert(candidates, math.floor(preferredCursors))
    end

    if #candidates == 0 then
        table.insert(candidates, startPos)
    end

    local function clampToSelection(pos)
        if pos < startPos then
            return startPos
        end
        if pos > endPosInclusive then
            return endPosInclusive
        end
        return pos
    end

    local firstDirectCandidate = nil
    for _, rawCandidate in ipairs(candidates) do
        local candidate = clampToSelection(rawCandidate)
        local ch = text:sub(candidate, candidate)
        if isIdentifierChar(ch) and not (type(stopCharSet) == "table" and stopCharSet[ch] == true) then
            if firstDirectCandidate == nil then
                firstDirectCandidate = candidate
            end

            local tokenStart, tokenEndExclusive = resolveIdentifierRange(text, candidate, startPos, endPosInclusive)
            if type(tokenStart) == "number" and type(tokenEndExclusive) == "number" then
                if startPos >= tokenStart and startPos < tokenEndExclusive then
                    return candidate
                end
            end
        end
    end

    if firstDirectCandidate ~= nil then
        return firstDirectCandidate
    end

    for _, rawCandidate in ipairs(candidates) do
        local candidate = clampToSelection(rawCandidate)
        for distance = 1, (endPosInclusive - startPos) do
            local rightPos = candidate + distance
            if rightPos <= endPosInclusive then
                local rightChar = text:sub(rightPos, rightPos)
                if isIdentifierChar(rightChar) and not (type(stopCharSet) == "table" and stopCharSet[rightChar] == true) then
                    return rightPos
                end
            end

            local leftPos = candidate - distance
            if leftPos >= startPos then
                local leftChar = text:sub(leftPos, leftPos)
                if isIdentifierChar(leftChar) and not (type(stopCharSet) == "table" and stopCharSet[leftChar] == true) then
                    return leftPos
                end
            end
        end
    end

    return nil
end

function EditorTextUtils.resolveRetokenizeSearchRange(text, selectionStart, selectionEndExclusive, preferredCursors)
    if type(text) ~= "string" then
        return nil, nil
    end

    local textLength = #text
    if textLength <= 0 then
        return nil, nil
    end

    if type(selectionStart) ~= "number" or type(selectionEndExclusive) ~= "number" then
        return nil, nil
    end

    local startPos = math.max(1, math.floor(selectionStart))
    local endPosExclusive = math.min(textLength + 1, math.floor(selectionEndExclusive))
    if endPosExclusive <= startPos then
        return nil, nil
    end

    local shouldExpandToLine = false

    local function evaluatePreferredCursor(value)
        if type(value) ~= "number" then
            return
        end

        local cursor = math.floor(value)
        if cursor < 1 or cursor > textLength then
            return
        end

        if cursor < startPos or cursor >= endPosExclusive then
            shouldExpandToLine = true
        end
    end

    if type(preferredCursors) == "table" then
        for _, value in pairs(preferredCursors) do
            evaluatePreferredCursor(value)
            if shouldExpandToLine then
                break
            end
        end
    else
        evaluatePreferredCursor(preferredCursors)
    end

    if shouldExpandToLine then
        local lineStart = findLineStart(text, startPos)
        local lineEndExclusive = findLineEndExclusive(text, startPos)
        return lineStart, lineEndExclusive
    end

    return startPos, endPosExclusive
end

function EditorTextUtils.findMethodIdentifierCursorInRange(text, rangeStart, rangeEndExclusive)
    if type(text) ~= "string" then
        return nil
    end

    local textLength = #text
    if textLength <= 0 then
        return nil
    end

    if type(rangeStart) ~= "number" or type(rangeEndExclusive) ~= "number" then
        return nil
    end

    local startPos = math.max(1, math.floor(rangeStart))
    local endPosExclusive = math.min(textLength + 1, math.floor(rangeEndExclusive))
    if endPosExclusive <= startPos then
        return nil
    end

    local openParenPos = nil
    for pos = startPos, endPosExclusive - 1 do
        if text:sub(pos, pos) == "(" then
            openParenPos = pos
            break
        end
    end

    if type(openParenPos) ~= "number" or openParenPos <= startPos then
        return nil
    end

    local cursor = openParenPos - 1
    while cursor >= startPos do
        local ch = text:sub(cursor, cursor)
        if isIdentifierChar(ch) then
            return cursor
        end
        if not isHorizontalWhitespace(ch) then
            return nil
        end
        cursor -= 1
    end

    return nil
end

function EditorTextUtils.resolvePreferredSelectionCursor(initialCursorPos, liveCursorPos, textLength, preferInitialCursor)
    textLength = tonumber(textLength) or 0
    textLength = math.max(0, math.floor(textLength))

    local function normalizeCursor(value)
        if type(value) ~= "number" then
            return nil
        end

        if textLength <= 0 then
            return nil
        end

        local cursor = math.floor(value)
        if cursor < 1 or cursor > (textLength + 1) then
            return nil
        end
        if cursor > textLength then
            cursor = textLength
        end
        if cursor < 1 then
            return nil
        end
        return cursor
    end

    local resolvedInitialCursor = normalizeCursor(initialCursorPos)
    local resolvedLiveCursor = normalizeCursor(liveCursorPos)

    if preferInitialCursor == true then
        if type(resolvedInitialCursor) == "number" then
            return resolvedInitialCursor
        end
        return resolvedLiveCursor
    end

    if type(resolvedLiveCursor) == "number" then
        return resolvedLiveCursor
    end

    return resolvedInitialCursor
end

function EditorTextUtils.computeStringDoubleClickSelection(text, cursorPos, expandToLine, stopChars)
    text = text or ""
    if text == "" then
        return nil, nil
    end

    cursorPos = clampCursor(text, cursorPos)
    if cursorPos > #text then
        cursorPos = #text
    end

    if cursorPos < 1 then
        return nil, nil
    end

    local stopCharSet = buildStopCharSet(stopChars)

    local lineStart = findLineStart(text, cursorPos)
    local lineEndExclusive = findLineEndExclusive(text, cursorPos)
    local lineEndInclusive = lineEndExclusive - 1
    if lineEndInclusive < lineStart then
        return nil, nil
    end

    local contentStart = nil
    local contentEnd = nil
    local inString = false
    local openQuote = nil

    for i = lineStart, lineEndInclusive do
        local ch = text:sub(i, i)
        if ch == "\"" and not isEscapedQuote(text, i, lineStart) then
            if inString and openQuote then
                local currentContentStart = openQuote + 1
                local currentContentEnd = i - 1
                if cursorPos >= currentContentStart and cursorPos <= currentContentEnd then
                    contentStart = currentContentStart
                    contentEnd = currentContentEnd
                    break
                end
                inString = false
                openQuote = nil
            else
                inString = true
                openQuote = i
            end
        end
    end

    if not contentStart and inString and openQuote then
        local currentContentStart = openQuote + 1
        local currentContentEnd = lineEndInclusive
        if cursorPos >= currentContentStart and cursorPos <= currentContentEnd then
            contentStart = currentContentStart
            contentEnd = currentContentEnd
        end
    end

    if expandToLine == true then
        return lineStart, lineEndExclusive
    end

    if type(contentStart) == "number" and type(contentEnd) == "number" and contentEnd >= contentStart then
        local contentIdentifierStart, contentIdentifierEndExclusive =
            resolveTokenRangeAtCursor(text, cursorPos, contentStart, contentEnd, stopCharSet)
        if type(contentIdentifierStart) == "number" and type(contentIdentifierEndExclusive) == "number" then
            return contentIdentifierStart, contentIdentifierEndExclusive
        end

        local cursorChar = text:sub(cursorPos, cursorPos)
        if isScanStopChar(cursorChar, stopCharSet) then
            return nil, nil
        end

        local segmentStart = cursorPos
        while segmentStart > contentStart do
            local prevChar = text:sub(segmentStart - 1, segmentStart - 1)
            if isScanStopChar(prevChar, stopCharSet) then
                break
            end
            segmentStart -= 1
        end

        local segmentEndExclusive = cursorPos + 1
        while segmentEndExclusive <= contentEnd do
            local nextChar = text:sub(segmentEndExclusive, segmentEndExclusive)
            if isScanStopChar(nextChar, stopCharSet) then
                break
            end
            segmentEndExclusive += 1
        end

        if segmentEndExclusive <= segmentStart then
            return nil, nil
        end

        return segmentStart, segmentEndExclusive
    end

    return resolveTokenRangeAtCursor(text, cursorPos, lineStart, lineEndInclusive, stopCharSet)
end

function EditorTextUtils.resolveSelectionRangeForMode(text, cursorPos, selectionMode, stopChars, includeLineTerminator)
    text = text or ""
    if text == "" then
        return nil, nil
    end

    cursorPos = clampCursor(text, cursorPos)
    if cursorPos > #text then
        cursorPos = #text
    end

    if cursorPos < 1 then
        return nil, nil
    end

    if selectionMode == "line" then
        local lineStart = findLineStart(text, cursorPos)
        local lineEndExclusive = findLineEndExclusive(text, cursorPos)

        if includeLineTerminator == true and lineEndExclusive <= #text and text:sub(lineEndExclusive, lineEndExclusive) == "\n" then
            lineEndExclusive += 1
        end

        if lineEndExclusive <= lineStart then
            return nil, nil
        end

        return lineStart, lineEndExclusive
    end

    if selectionMode == "word" then
        return EditorTextUtils.computeStringDoubleClickSelection(text, cursorPos, false, stopChars)
    end

    return cursorPos, math.min(#text + 1, cursorPos + 1)
end

function EditorTextUtils.resolveDragSelectionForMode(text, anchorStart, anchorEndExclusive, cursorPos, selectionMode, stopChars, includeLineTerminator)
    text = text or ""
    if text == "" then
        return nil, nil
    end

    if type(anchorStart) ~= "number" or type(anchorEndExclusive) ~= "number" then
        return nil, nil
    end

    anchorStart = math.max(1, math.floor(anchorStart))
    anchorEndExclusive = math.min(#text + 1, math.floor(anchorEndExclusive))
    if anchorEndExclusive <= anchorStart then
        return nil, nil
    end

    local currentStart, currentEndExclusive = EditorTextUtils.resolveSelectionRangeForMode(
        text,
        cursorPos,
        selectionMode,
        stopChars,
        includeLineTerminator
    )

    if type(currentStart) ~= "number" or type(currentEndExclusive) ~= "number" or currentEndExclusive <= currentStart then
        return nil, nil
    end

    return math.min(anchorStart, currentStart), math.max(anchorEndExclusive, currentEndExclusive)
end

function EditorTextUtils.shouldConsumeCompletionAcceptInput(keyCode, completionVisible, completionCount)
    completionCount = tonumber(completionCount) or 0
    completionCount = math.max(0, math.floor(completionCount))

    local completionsAvailable = completionVisible == true or completionCount > 0
    if not completionsAvailable then
        return false
    end

    local keyName = normalizeKeyName(keyCode)
    return keyName == "TAB" or keyName == "RETURN" or keyName == "KEYPADENTER"
end

function EditorTextUtils.shouldAutoTriggerCompletions(text, cursorPos)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    local lineStart = findLineStart(text, cursorPos)
    local linePrefix = text:sub(lineStart, cursorPos - 1)

    if linePrefix:match("^%s*$") then
        return false
    end

    if linePrefix:match("[%a_][%w_]*$") then
        return true
    end

    return false
end

function EditorTextUtils.extractCompletionLabel(item)
    if type(item) == "table" then
        local label = item.label
        if type(label) == "string" and label ~= "" then
            return label
        end
        return nil
    end

    if type(item) == "string" and item ~= "" then
        return item
    end

    return nil
end

function EditorTextUtils.resolveCompletionLabel(completions, activeIndex)
    if type(completions) ~= "table" then
        return nil
    end

    local function labelAt(index)
        if type(index) ~= "number" then
            return nil
        end
        index = math.floor(index)
        if index < 1 then
            return nil
        end
        return EditorTextUtils.extractCompletionLabel(completions[index])
    end

    local activeLabel = labelAt(activeIndex)
    if activeLabel then
        return activeLabel
    end

    return labelAt(1)
end

function EditorTextUtils.resolveNextCompletionIndex(activeIndex, completionCount, direction)
    completionCount = tonumber(completionCount) or 0
    completionCount = math.max(0, math.floor(completionCount))
    if completionCount == 0 then
        return nil
    end

    direction = tonumber(direction) or 0
    direction = direction < 0 and -1 or 1

    if type(activeIndex) ~= "number" then
        return direction < 0 and completionCount or 1
    end

    activeIndex = math.floor(activeIndex)
    if activeIndex < 1 or activeIndex > completionCount then
        return direction < 0 and completionCount or 1
    end

    local nextIndex = activeIndex + direction
    if nextIndex < 1 then
        return completionCount
    end
    if nextIndex > completionCount then
        return 1
    end

    return nextIndex
end

function EditorTextUtils.getCompletionKindIcon(kind)
    local key = string.lower(tostring(kind or ""))
    key = key:gsub("[%s_%-]", "")

    local map = {
        property = "rbxassetid://90821796318409",
        method = "rbxassetid://138609826896991",
        constructor = "rbxassetid://138609826896991",
        variable = "rbxassetid://109131190629355",
        localvariable = "rbxassetid://109131190629355",
        event = "rbxassetid://79508262463966",
        enum = "rbxassetid://122090598292533",
        enumitem = "rbxassetid://120188564562495",
        class = "rbxassetid://109871698753471",
        namespace = "rbxassetid://99768122416664",
        struct = "rbxassetid://121562580658819",
        type = "rbxassetid://109871698753471",
    }

    return map[key] or "rbxassetid://109871698753471"
end

function EditorTextUtils.getHoverKindBadgeText(kind)
    local key = string.lower(tostring(kind or ""))
    key = key:gsub("[%s_%-]", "")

    local map = {
        class = "C",
        struct = "S",
        type = "T",
        namespace = "N",
        enum = "E",
        enumitem = "E",
        method = "M",
        constructor = "M",
        property = "P",
        field = "F",
        event = "E",
        variable = "V",
        localvariable = "V",
        service = "S",
        keyword = "K",
    }

    return map[key] or "•"
end

function EditorTextUtils.getCompletionViewportCount(totalCount, maxVisible)
    totalCount = tonumber(totalCount) or 0
    totalCount = math.max(0, math.floor(totalCount))
    if totalCount == 0 then
        return 0
    end

    maxVisible = tonumber(maxVisible) or 0
    maxVisible = math.max(1, math.floor(maxVisible))
    return math.min(totalCount, maxVisible)
end

function EditorTextUtils.resolveCursorColumnFromX(lineText, targetX, fallbackCharWidth, measureWidth)
    lineText = tostring(lineText or "")
    local lineLength = #lineText

    targetX = tonumber(targetX) or 0
    if targetX <= 0 then
        return 1
    end

    local maxColumn = lineLength + 1
    local fallbackWidth = tonumber(fallbackCharWidth) or 8
    if fallbackWidth < 1 then
        fallbackWidth = 1
    end

    local widthCache = {}
    local function widthForCharCount(charCount)
        charCount = math.max(0, math.min(math.floor(charCount or 0), lineLength))
        if charCount == 0 then
            return 0
        end

        local cached = widthCache[charCount]
        if cached ~= nil then
            return cached
        end

        local width = nil
        if type(measureWidth) == "function" then
            local ok, measured = pcall(measureWidth, lineText:sub(1, charCount))
            if ok and type(measured) == "number" then
                width = math.max(0, measured)
            end
        end

        if width == nil then
            width = charCount * fallbackWidth
        end

        widthCache[charCount] = width
        return width
    end

    local endWidth = widthForCharCount(lineLength)
    if targetX >= endWidth then
        return maxColumn
    end

    local lo = 0
    local hi = lineLength
    while lo < hi do
        local mid = math.floor((lo + hi + 1) / 2)
        if widthForCharCount(mid) <= targetX then
            lo = mid
        else
            hi = mid - 1
        end
    end

    local leftCount = lo
    local rightCount = math.min(lineLength, leftCount + 1)
    local leftWidth = widthForCharCount(leftCount)
    local rightWidth = widthForCharCount(rightCount)

    local chosenCount = leftCount
    if rightCount > leftCount and math.abs(targetX - rightWidth) < math.abs(targetX - leftWidth) then
        chosenCount = rightCount
    end

    return math.clamp(chosenCount + 1, 1, maxColumn)
end

function EditorTextUtils.computeCompletionReplacementRange(text, cursorPos)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    local before = text:sub(1, cursorPos - 1)
    local prefix = before:match("([%a_][%w_]*)$") or ""

    local startPos = cursorPos - #prefix
    local endPos = cursorPos

    return startPos, endPos
end

function EditorTextUtils.resolveCompletionReplacementRange(text, cursorPos, anchorStart, anchorEnd, activeCursorPos)
    text = text or ""

    local fallbackStart, fallbackEnd = EditorTextUtils.computeCompletionReplacementRange(text, cursorPos)

    if type(anchorStart) ~= "number" or type(anchorEnd) ~= "number" then
        return fallbackStart, fallbackEnd
    end

    anchorStart = math.floor(anchorStart)
    anchorEnd = math.floor(anchorEnd)

    local maxPos = #text + 1
    if anchorStart < 1 or anchorEnd < 1 or anchorStart > maxPos or anchorEnd > maxPos or anchorStart > anchorEnd then
        return fallbackStart, fallbackEnd
    end

    local clampedCursor = clampCursor(text, cursorPos)
    if clampedCursor >= anchorStart and clampedCursor <= anchorEnd then
        return anchorStart, anchorEnd
    end

    if type(activeCursorPos) == "number" then
        local clampedActiveCursor = clampCursor(text, activeCursorPos)
        if clampedActiveCursor >= anchorStart and clampedActiveCursor <= anchorEnd then
            return anchorStart, anchorEnd
        end
    end

    return fallbackStart, fallbackEnd
end

function EditorTextUtils.isSmartDoubleClick(nowAt, lastAt, cursorPos, lastCursorPos, windowSeconds, maxCursorDelta)
    if type(nowAt) ~= "number" or type(lastAt) ~= "number" then
        return false
    end

    windowSeconds = tonumber(windowSeconds) or 0
    if windowSeconds <= 0 then
        return false
    end

    local age = nowAt - lastAt
    if age < 0 or age > windowSeconds then
        return false
    end

    if type(cursorPos) ~= "number" or type(lastCursorPos) ~= "number" then
        return false
    end

    maxCursorDelta = tonumber(maxCursorDelta) or 0
    if maxCursorDelta < 0 then
        maxCursorDelta = 0
    end

    return math.abs(math.floor(cursorPos) - math.floor(lastCursorPos)) <= maxCursorDelta
end

function EditorTextUtils.shouldExpandSmartDoubleClickCycle(nowAt, cursorPos, cycle, windowSeconds, maxCursorDelta)
    if type(cycle) ~= "table" then
        return false
    end

    return EditorTextUtils.isSmartDoubleClick(
        nowAt,
        cycle.lastAt,
        cursorPos,
        cycle.cursorPos,
        windowSeconds,
        maxCursorDelta
    )
end

function EditorTextUtils.shouldPromoteSmartDoubleClickCycleExpand(nowAt, cursorPos, cycle, windowSeconds, maxCursorDelta)
    if type(cycle) ~= "table" then
        return false
    end

    if cycle.pending == true then
        return false
    end

    if cycle.expandToLine == true then
        return false
    end

    return EditorTextUtils.isSmartDoubleClick(
        nowAt,
        cycle.lastAt,
        cursorPos,
        cycle.cursorPos,
        windowSeconds,
        maxCursorDelta
    )
end

function EditorTextUtils.resolveSmartSelectionClickState(nowAt, cursorPos, lastState, doubleWindowSeconds, lineWindowSeconds, maxCursorDelta, lineModeMaxCursorDelta, duplicateWindowSeconds, duplicateCursorDelta)
    local defaultState = {
        count = 1,
        mode = "character",
        inSequence = false,
    }

    if type(nowAt) ~= "number" or type(cursorPos) ~= "number" then
        return defaultState
    end

    local previousCount = 0
    local lastAt = nil
    local lastCursorPos = nil
    local burstStartAt = nil

    if type(lastState) == "table" then
        previousCount = math.max(0, math.floor(tonumber(lastState.count) or 0))
        lastAt = tonumber(lastState.at)
        lastCursorPos = tonumber(lastState.cursorPos)
        burstStartAt = tonumber(lastState.burstStartAt)
    end

    if type(burstStartAt) ~= "number" then
        burstStartAt = lastAt
    end

    local duplicateWindow = tonumber(duplicateWindowSeconds)
    if type(duplicateWindow) == "number" and duplicateWindow > 0 and type(lastAt) == "number" and type(lastCursorPos) == "number" then
        local duplicateDeltaThreshold = tonumber(duplicateCursorDelta)
        if type(duplicateDeltaThreshold) ~= "number" then
            duplicateDeltaThreshold = tonumber(maxCursorDelta)
        end
        duplicateDeltaThreshold = math.max(0, math.floor(duplicateDeltaThreshold or 0))

        local age = nowAt - lastAt
        local cursorDelta = math.abs(math.floor(cursorPos) - math.floor(lastCursorPos))
        if age >= 0 and age <= duplicateWindow and cursorDelta <= duplicateDeltaThreshold then
            local preservedCount = math.min(3, math.max(1, previousCount))
            local preservedMode = "character"
            if preservedCount == 2 then
                preservedMode = "word"
            elseif preservedCount >= 3 then
                preservedMode = "line"
            end

            return {
                count = preservedCount,
                mode = preservedMode,
                inSequence = false,
                previousCount = previousCount,
                duplicate = true,
                burstStartAt = burstStartAt,
            }
        end
    end

    if previousCount >= 3 then
        return {
            count = 1,
            mode = "character",
            inSequence = false,
            previousCount = previousCount,
            burstStartAt = nowAt,
        }
    end

    local windowSeconds = doubleWindowSeconds
    local cursorDeltaThreshold = maxCursorDelta
    local baseDoubleWindow = tonumber(doubleWindowSeconds) or 0
    if previousCount >= 2 then
        windowSeconds = lineWindowSeconds
        cursorDeltaThreshold = tonumber(lineModeMaxCursorDelta)
        if type(cursorDeltaThreshold) ~= "number" then
            cursorDeltaThreshold = maxCursorDelta
        end
    elseif previousCount == 1 then
        local secondClickGraceWindow = 1.0
        local secondClickGraceCursorDelta = 4
        windowSeconds = math.max(tonumber(windowSeconds) or 0, secondClickGraceWindow)

        local age = type(lastAt) == "number" and (nowAt - lastAt) or nil
        if type(age) == "number" and age > math.max(0, baseDoubleWindow) then
            local numericDelta = tonumber(cursorDeltaThreshold)
            if type(numericDelta) ~= "number" then
                numericDelta = secondClickGraceCursorDelta
            end
            cursorDeltaThreshold = math.min(numericDelta, secondClickGraceCursorDelta)
        end
    end

    local inSequence = EditorTextUtils.isSmartDoubleClick(
        nowAt,
        lastAt,
        cursorPos,
        lastCursorPos,
        windowSeconds,
        cursorDeltaThreshold
    )

    if inSequence and type(burstStartAt) == "number" then
        local burstWindow = tonumber(doubleWindowSeconds) or 0
        if previousCount == 1 then
            local secondClickGraceWindow = 1.0
            burstWindow = math.max(burstWindow, secondClickGraceWindow)
        end
        if burstWindow > 0 then
            local burstAge = nowAt - burstStartAt
            if burstAge < 0 or burstAge > burstWindow then
                inSequence = false
            end
        end
    end

    local count = 1
    if inSequence then
        count = math.min(3, math.max(1, previousCount + 1))
    end

    local mode = "character"
    if count == 2 then
        mode = "word"
    elseif count >= 3 then
        mode = "line"
    end

    local nextBurstStartAt = nowAt
    if inSequence then
        nextBurstStartAt = burstStartAt or lastAt or nowAt
    end

    return {
        count = count,
        mode = mode,
        inSequence = inSequence,
        previousCount = previousCount,
        burstStartAt = nextBurstStartAt,
    }
end

function EditorTextUtils.shouldUseCaretHoverCursor(cursorPos, selectionStart)
    if type(cursorPos) ~= "number" then
        return false
    end

    if type(selectionStart) == "number" and selectionStart ~= -1 and selectionStart ~= cursorPos then
        return false
    end

    return true
end

function EditorTextUtils.resolveHoverCursorFromSelection(selectionStart, cursorPos, textLength)
    if type(selectionStart) ~= "number" or type(cursorPos) ~= "number" then
        return nil
    end

    if selectionStart == -1 or selectionStart == cursorPos then
        return nil
    end

    local maxCursor = math.max(1, math.floor(tonumber(textLength) or 0) + 1)
    local fromPos = math.max(1, math.min(selectionStart, cursorPos))
    local toPos = math.max(fromPos, math.max(selectionStart, cursorPos))

    fromPos = math.clamp(fromPos, 1, maxCursor)
    toPos = math.clamp(toPos, 1, maxCursor)
    if toPos <= fromPos then
        return nil
    end

    return fromPos
end

function EditorTextUtils.shouldApplySmartSelectionForCycle(activeCycle, expectedCycleId)
    if type(activeCycle) ~= "table" then
        return false
    end

    if type(expectedCycleId) ~= "number" then
        return false
    end

    return tonumber(activeCycle.id) == expectedCycleId
end

function EditorTextUtils.shouldUseCachedHoverSample(sampleSource, cursorPos, selectionStart)
    if sampleSource == "caret" then
        return EditorTextUtils.shouldUseCaretHoverCursor(cursorPos, selectionStart)
    end

    return true
end

function EditorTextUtils.shouldApplyCharacterNormalization(expectedId, activeId)
    local expected = tonumber(expectedId)
    local active = tonumber(activeId)
    if type(expected) ~= "number" or type(active) ~= "number" then
        return false
    end

    return expected == active
end

function EditorTextUtils.shouldNormalizeCharacterClickSelection(selectionStart, cursorPosition, clickCursorPos, shiftDown)
    if shiftDown == true then
        return false
    end

    if type(selectionStart) ~= "number" or type(cursorPosition) ~= "number" then
        return false
    end

    if selectionStart == -1 or selectionStart == cursorPosition then
        return false
    end

    return type(clickCursorPos) == "number"
end

function EditorTextUtils.shouldIgnoreDuplicatePrimaryMouseDown(lastAt, lastCursorPos, nowAt, cursorPos, dedupeSeconds, maxCursorDelta)
    return EditorTextUtils.isSmartDoubleClick(nowAt, lastAt, cursorPos, lastCursorPos, dedupeSeconds, maxCursorDelta)
end

function EditorTextUtils.shouldRunNativeRetokenizeFallback(nowAt, lastAt, cursorPos, lastCursorPos, windowSeconds, maxCursorDelta)
    if type(nowAt) ~= "number" or type(lastAt) ~= "number" then
        return false
    end

    windowSeconds = tonumber(windowSeconds) or 0
    if windowSeconds <= 0 then
        return false
    end

    local age = nowAt - lastAt
    if age < 0 or age > windowSeconds then
        return false
    end

    if type(cursorPos) ~= "number" or type(lastCursorPos) ~= "number" then
        return false
    end

    maxCursorDelta = tonumber(maxCursorDelta) or 0
    if maxCursorDelta < 0 then
        maxCursorDelta = 0
    end

    if math.abs(math.floor(cursorPos) - math.floor(lastCursorPos)) > maxCursorDelta then
        return false
    end

    return true
end

function EditorTextUtils.shouldShowCompletionInfo(lastMouseMoveAt, shownAtOrNow, nowOrThreshold, thresholdSeconds)
    if type(lastMouseMoveAt) ~= "number" then
        return false
    end

    local shownAt = nil
    local now = nil
    local threshold = nil

    if type(thresholdSeconds) == "number" then
        shownAt = shownAtOrNow
        now = nowOrThreshold
        threshold = thresholdSeconds
    else
        now = shownAtOrNow
        threshold = nowOrThreshold
    end

    now = type(now) == "number" and now or os.clock()
    threshold = type(threshold) == "number" and threshold or 0.25

    if type(shownAt) == "number" and lastMouseMoveAt < shownAt then
        return false
    end

    return (now - lastMouseMoveAt) <= threshold
end

function EditorTextUtils.shouldSuppressHoverInfo(completionVisible, lastTypedAt, now, typingThresholdSeconds, editorFocused)
    if editorFocused == false then
        return true
    end

    if completionVisible == true then
        return true
    end

    if type(lastTypedAt) == "number" then
        now = type(now) == "number" and now or os.clock()
        typingThresholdSeconds = type(typingThresholdSeconds) == "number" and typingThresholdSeconds or 0.25

        if (now - lastTypedAt) <= typingThresholdSeconds then
            return true
        end
    end

    return false
end

return EditorTextUtils
