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

    local endPosExclusive = cursorPos

    if segmentKind == "punctuation" then
        endPosExclusive += 1
    else
        while endPosExclusive <= #text do
            local currentChar = text:sub(endPosExclusive, endPosExclusive)
            local currentKind = classifyForwardDeleteChar(currentChar)

            if currentKind == "newline" or currentKind ~= segmentKind then
                break
            end

            endPosExclusive += 1
        end
    end

    if endPosExclusive == cursorPos then
        return text, cursorPos, false
    end

    local newText = text:sub(1, cursorPos - 1) .. text:sub(endPosExclusive)
    return newText, cursorPos, true
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

function EditorTextUtils.isUndoRedoShortcut(keyCode, ctrlDown, altDown)
    if ctrlDown ~= true or altDown == true then
        return false
    end

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

    keyName = string.upper(keyName)
    return keyName == "Z" or keyName == "Y"
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

function EditorTextUtils.computeCompletionReplacementRange(text, cursorPos)
    text = text or ""
    cursorPos = clampCursor(text, cursorPos)

    local before = text:sub(1, cursorPos - 1)
    local prefix = before:match("([%a_][%w_]*)$") or ""

    local startPos = cursorPos - #prefix
    local endPos = cursorPos

    return startPos, endPos
end

function EditorTextUtils.resolveCompletionReplacementRange(text, cursorPos, anchorStart, anchorEnd)
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
    if clampedCursor < anchorStart or clampedCursor > anchorEnd then
        return fallbackStart, fallbackEnd
    end

    return anchorStart, anchorEnd
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

return EditorTextUtils
