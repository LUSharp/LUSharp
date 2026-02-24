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
        -- Repair only when the native path behaved like a plain char delete.
        -- If a full token was already removed, keep native behavior.
        return #removedText <= 1
    end

    -- Non-shortcut fallback: only repair newline-spanning corruption.
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
