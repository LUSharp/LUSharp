local UserInputService = game:GetService("UserInputService")
local TextService = game:GetService("TextService")
local RunService = game:GetService("RunService")
local GuiService = game:GetService("GuiService")

local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local SyntaxHighlighter = requireModule("SyntaxHighlighter")
local EditorTextUtils = requireModule("EditorTextUtils")

local Editor = {}
Editor.__index = Editor

local LARGE_SOURCE_WORK_LIMIT = 30000
local MAX_RENDERED_ERROR_SQUIGGLES = 120
local HOVER_REQUEST_DELAY_SECONDS = 0.18
local HOVER_POLL_INTERVAL_SECONDS = 0.2
local COMPLETION_INFO_MOUSE_INTENT_WINDOW = 0.25

local cursorPositionFromScreenPoint

local function countLines(text)
    local _, newlines = text:gsub("\n", "\n")
    return newlines + 1
end

local function buildLineNumbersText(text)
    local lineCount = countLines(text)
    local out = table.create(lineCount)
    for i = 1, lineCount do
        out[i] = tostring(i)
    end
    return table.concat(out, "\n")
end

local function getLineCol(text, cursorPosition)
    if cursorPosition == nil or cursorPosition < 1 then
        return 1, 1
    end

    local maxPos = math.min(cursorPosition - 1, #text)
    local line = 1
    local col = 1

    for i = 1, maxPos do
        if text:sub(i, i) == "\n" then
            line += 1
            col = 1
        else
            col += 1
        end
    end

    return line, col
end

local function getLeadingWhitespace(text)
    return text:match("^[ \t]*") or ""
end

local function trimRight(text)
    return (text:gsub("[ \t]+$", ""))
end

local function escapeRichText(text)
    return (text:gsub("&", "&amp;"):gsub("<", "&lt;"):gsub(">", "&gt;"))
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

local function updateStatus(self)
    local line, col = getLineCol(self.textBox.Text, self.textBox.CursorPosition)
    local errorCount = tonumber(self.errorCount) or 0

    local text
    if errorCount > 0 then
        text = string.format("%s | Ln %d, Col %d | Errors: %d", self.fileName, line, col, errorCount)
    else
        text = string.format("%s | Ln %d, Col %d", self.fileName, line, col)
    end

    if self.options and self.options.showHoverDebug == true then
        local hoverDebug = tostring(self.hoverDebugText or "")
        if hoverDebug ~= "" then
            text = text .. " | " .. hoverDebug
        end
    end

    self.statusLabel.Text = text
end

local function setHoverDebug(self, message)
    local nextMessage = tostring(message or "")
    if self.hoverDebugText == nextMessage then
        return
    end

    self.hoverDebugText = nextMessage
    updateStatus(self)
end

local function formatOutsideEditorDebug(self)
    local details = tostring(self._lastHoverOutsideDebug or "")
    if details == "" then
        return "Hover: outside editor"
    end

    return "Hover: outside editor " .. details
end

local function isValidMousePoint(point)
    if not point then
        return false
    end

    local x = tonumber(point.X)
    local y = tonumber(point.Y)
    return x ~= nil and y ~= nil and x >= 0 and y >= 0
end

local function rememberMousePosition(self, point)
    if isValidMousePoint(point) then
        self._lastValidMousePosition = Vector2.new(point.X, point.Y)
    end

    return self._lastValidMousePosition
end

local function getPreferredMousePosition(self)
    local pos = rememberMousePosition(self, UserInputService:GetMouseLocation())
    if pos then
        return pos
    end

    local pluginMouse = self and self._pluginMouse
    if pluginMouse then
        pos = rememberMousePosition(self, Vector2.new(pluginMouse.X, pluginMouse.Y))
        if pos then
            return pos
        end
    end

    return self and self._lastValidMousePosition or nil
end

local function getHoverAnchorScreenPosition(self)
    local mousePos = getPreferredMousePosition(self)
    if mousePos then
        return mousePos
    end

    if self and self._lastResolvedHoverPoint then
        return self._lastResolvedHoverPoint
    end

    if self and self.caret and self.caret.AbsolutePosition then
        return Vector2.new(
            self.caret.AbsolutePosition.X + 8,
            self.caret.AbsolutePosition.Y + math.max(10, self.options and self.options.lineHeight or 18)
        )
    end

    if self and self.root and self.root.AbsolutePosition then
        return Vector2.new(self.root.AbsolutePosition.X + 24, self.root.AbsolutePosition.Y + 24)
    end

    return Vector2.new(0, 0)
end

local function resolveHoverCursorFromCandidates(self)
    local candidates = {}
    local seen = {}

    local function addCandidate(label, point)
        if not isValidMousePoint(point) then
            return
        end

        local x = math.floor(point.X)
        local y = math.floor(point.Y)
        local key = tostring(x) .. ":" .. tostring(y)
        if seen[key] then
            return
        end

        seen[key] = true
        table.insert(candidates, { label = label, point = Vector2.new(point.X, point.Y) })
    end

    addCandidate("preferred", getPreferredMousePosition(self))

    if self and self._pluginMouse then
        local pluginPoint = Vector2.new(self._pluginMouse.X, self._pluginMouse.Y)
        addCandidate("pluginRaw", pluginPoint)

        if self.root and self.root.AbsolutePosition then
            local rootPos = self.root.AbsolutePosition
            addCandidate("plugin+root", Vector2.new(rootPos.X + pluginPoint.X, rootPos.Y + pluginPoint.Y))
        end

        if self.codeContainer and self.codeContainer.AbsolutePosition then
            local codePos = self.codeContainer.AbsolutePosition
            addCandidate("plugin+code", Vector2.new(codePos.X + pluginPoint.X, codePos.Y + pluginPoint.Y))
        end
    end

    for _, candidate in ipairs(candidates) do
        local cursorPos = cursorPositionFromScreenPoint(self, candidate.point)
        if cursorPos then
            self._lastResolvedHoverPoint = candidate.point
            return cursorPos, candidate.point, candidate.label
        end
    end

    return nil, nil, nil
end

local _fallbackIntelliSense = nil
local _fallbackIntelliSenseAttempted = false

local function requestHoverInfo(self, cursorPos)
    if self.onRequestHoverInfo then
        return self.onRequestHoverInfo(cursorPos)
    end

    if not _fallbackIntelliSenseAttempted then
        _fallbackIntelliSenseAttempted = true
        local ok, moduleOrErr = pcall(requireModule, "IntelliSense")
        if ok and type(moduleOrErr) == "table" then
            _fallbackIntelliSense = moduleOrErr
        end
    end

    if not _fallbackIntelliSense then
        return nil
    end

    local source = self.textBox and self.textBox.Text or ""
    if source == "" then
        return nil
    end

    return _fallbackIntelliSense.getHoverInfo(source, cursorPos, {
        searchNearby = true,
        nearbyRadius = 20,
    })
end

local function measureCharWidth(textSize, font)
    local size = TextService:GetTextSize("M", textSize, font, Vector2.new(1000, 1000))
    return size.X
end

local function measureLineHeight(textSize, font)
    local twoLines = TextService:GetTextSize("M\nM", textSize, font, Vector2.new(1000, 1000))
    local single = TextService:GetTextSize("M", textSize, font, Vector2.new(1000, 1000))

    local perLine = twoLines.Y / 2
    if perLine < single.Y then
        perLine = single.Y
    end

    return math.ceil(perLine)
end

local function caretShouldShow(textBox)
    if not textBox or not textBox:IsFocused() then
        return false
    end

    local sel = textBox.SelectionStart
    return sel == -1 or sel == textBox.CursorPosition
end

local function computeCaretLocalPosition(text, cursorPos, textSize, lineHeight)
    text = text or ""
    if type(cursorPos) ~= "number" or cursorPos < 1 then
        cursorPos = #text + 1
    end

    local line = 1
    local prefix = text:sub(1, math.max(0, cursorPos - 1))
    for i = 1, #prefix do
        if prefix:sub(i, i) == "\n" then
            line += 1
        end
    end

    local lastNewline = prefix:match(".*()\n")
    local lineStart = lastNewline and (lastNewline + 1) or 1
    local linePrefix = text:sub(lineStart, math.max(lineStart - 1, cursorPos - 1))

    local measured = TextService:GetTextSize(linePrefix, textSize, Enum.Font.Code, Vector2.new(100000, 100000))
    local x = measured.X
    local y = (line - 1) * lineHeight

    return x, y
end

local function updateCaretPosition(self)
    if not self.caret or not self.textBox then
        return
    end

    local x, y = computeCaretLocalPosition(self.textBox.Text, self.textBox.CursorPosition, self.options.textSize, self.options.lineHeight)

    self.caret.Position = UDim2.new(0, x, 0, y)
    self.caret.Size = UDim2.new(0, 1, 0, self.options.lineHeight)
    self._caretLocalX = x
    self._caretLocalY = y
end

local function stopCaretBlink(self)
    self._caretBlinkToken = (self._caretBlinkToken or 0) + 1
    if self.caret then
        self.caret.Visible = false
    end
end

local function startCaretBlink(self)
    self._caretBlinkToken = (self._caretBlinkToken or 0) + 1
    local token = self._caretBlinkToken

    task.spawn(function()
        local visible = true

        while self._caretBlinkToken == token and self.caret and self.textBox do
            updateCaretPosition(self)

            if caretShouldShow(self.textBox) then
                self.caret.Visible = visible
                visible = not visible
            else
                self.caret.Visible = false
                visible = true
            end

            task.wait(0.5)
        end
    end)
end

local function insertAtCursor(textBox, insertText)
    local text = textBox.Text
    local cursor = textBox.CursorPosition

    if cursor < 1 then
        cursor = #text + 1
    end

    local startPos = cursor
    local endPos = cursor

    if textBox.SelectionStart ~= -1 and textBox.SelectionStart ~= cursor then
        startPos = math.min(cursor, textBox.SelectionStart)
        endPos = math.max(cursor, textBox.SelectionStart)
    end

    local before = text:sub(1, startPos - 1)
    local after = text:sub(endPos)

    textBox.Text = before .. insertText .. after
    textBox.CursorPosition = startPos + #insertText
    textBox.SelectionStart = -1
end

local function replaceTextRange(textBox, startPos, endPosExclusive, insertText)
    local text = textBox.Text

    startPos = math.max(1, startPos)
    endPosExclusive = math.max(startPos, endPosExclusive)

    local before = text:sub(1, startPos - 1)
    local after = text:sub(endPosExclusive)

    textBox.Text = before .. insertText .. after
    textBox.CursorPosition = startPos + #insertText
    textBox.SelectionStart = -1
end

local function getCompletionKindVisual(kind)
    local key = string.lower(tostring(kind or "symbol"))

    local map = {
        class = { text = "C", color = Color3.fromRGB(78, 201, 176) },
        struct = { text = "S", color = Color3.fromRGB(156, 220, 254) },
        type = { text = "T", color = Color3.fromRGB(78, 201, 176) },
        namespace = { text = "N", color = Color3.fromRGB(201, 201, 201) },
        enum = { text = "E", color = Color3.fromRGB(181, 206, 168) },
        method = { text = "M", color = Color3.fromRGB(220, 220, 170) },
        constructor = { text = "M", color = Color3.fromRGB(220, 220, 170) },
        property = { text = "P", color = Color3.fromRGB(156, 220, 254) },
        field = { text = "F", color = Color3.fromRGB(156, 220, 254) },
        event = { text = "E", color = Color3.fromRGB(255, 198, 109) },
        variable = { text = "V", color = Color3.fromRGB(214, 186, 125) },
        service = { text = "S", color = Color3.fromRGB(134, 223, 168) },
        keyword = { text = "K", color = Color3.fromRGB(197, 134, 192) },
    }

    return map[key] or { text = "•", color = Color3.fromRGB(200, 200, 200) }
end

local function clearLayerChildren(layer)
    if not layer then
        return
    end

    for _, child in ipairs(layer:GetChildren()) do
        child:Destroy()
    end
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

    local col = math.max(1, math.floor(column or 1))
    return lineStart + col - 1
end

cursorPositionFromScreenPoint = function(self, screenPosition)
    if not self or not self.codeContainer or not self.scroller or not self.textBox then
        return nil
    end

    local source = self.textBox.Text or ""
    local sourceLength = #source

    local containerPos = self.codeContainer.AbsolutePosition
    local containerSize = self.codeContainer.AbsoluteSize
    local canvas = self.scroller.CanvasPosition

    local lineStarts = buildLineStarts(source)

    local function isInside(pointX, pointY)
        return pointX >= containerPos.X
            and pointY >= containerPos.Y
            and pointX <= (containerPos.X + containerSize.X)
            and pointY <= (containerPos.Y + containerSize.Y)
    end

    local function resolveAtPoint(pointX, pointY)
        local x = (pointX - containerPos.X) + canvas.X
        local y = (pointY - containerPos.Y) + canvas.Y

        if x < 0 or y < 0 then
            return nil
        end

        local line = math.max(1, math.floor(y / math.max(1, self.options.lineHeight)) + 1)
        local lineStart = lineStarts[line]
        if not lineStart then
            return sourceLength + 1
        end

        local nextLineStart = lineStarts[line + 1]
        local lineEndExclusive = nextLineStart and (nextLineStart - 1) or (sourceLength + 1)
        local lineLength = math.max(0, lineEndExclusive - lineStart)

        local charWidth = math.max(1, self._charWidth or 8)
        local column = math.floor((x / charWidth) + 1)
        column = math.clamp(column, 1, lineLength + 1)

        local index = lineStart + column - 1
        return math.clamp(index, 1, sourceLength + 1)
    end

    local rawX = tonumber(screenPosition.X)
    local rawY = tonumber(screenPosition.Y)
    if not rawX or not rawY then
        return nil
    end

    local insetTopLeft = GuiService:GetGuiInset()
    local insetX = insetTopLeft and insetTopLeft.X or 0
    local insetY = insetTopLeft and insetTopLeft.Y or 0

    local candidates = {
        { label = "raw", x = rawX, y = rawY },
        { label = "minusInset", x = rawX - insetX, y = rawY - insetY },
        { label = "plusInset", x = rawX + insetX, y = rawY + insetY },
    }

    for _, candidate in ipairs(candidates) do
        if isInside(candidate.x, candidate.y) then
            self._lastHoverOutsideDebug = nil
            return resolveAtPoint(candidate.x, candidate.y)
        end
    end

    self._lastHoverOutsideDebug = string.format(
        "(mouse %d,%d | box %d,%d..%d,%d | inset %d,%d)",
        math.floor(rawX),
        math.floor(rawY),
        math.floor(containerPos.X),
        math.floor(containerPos.Y),
        math.floor(containerPos.X + containerSize.X),
        math.floor(containerPos.Y + containerSize.Y),
        math.floor(insetX),
        math.floor(insetY)
    )

    return nil
end

local function renderDiagnosticSquiggle(layer, x1, x2, y, color, zIndex)
    local width = math.max(4, x2 - x1)
    local step = 4
    local count = math.max(1, math.floor(width / step))

    for i = 0, count - 1 do
        local segment = Instance.new("Frame")
        segment.BorderSizePixel = 0
        segment.BackgroundColor3 = color
        segment.Size = UDim2.new(0, 3, 0, 1)
        segment.Position = UDim2.new(0, x1 + (i * step), 0, y + ((i % 2 == 0) and 0 or 1))
        segment.ZIndex = zIndex
        segment.Parent = layer
    end
end

local function renderDiagnostics(self)
    local layer = self.diagnosticLayer
    clearLayerChildren(layer)

    if not layer or type(self.diagnostics) ~= "table" or #self.diagnostics == 0 then
        return
    end

    local source = self.textBox.Text or ""
    local sourceLength = #source
    local lineStarts = buildLineStarts(source)

    local rendered = 0

    for _, diagnostic in ipairs(self.diagnostics) do
        if rendered >= MAX_RENDERED_ERROR_SQUIGGLES then
            break
        end

        local severity = string.lower(tostring(diagnostic.severity or ""))
        if severity == "error" then
            local startLine = math.max(1, math.floor(tonumber(diagnostic.line) or 1))
            local startColumn = math.max(1, math.floor(tonumber(diagnostic.column) or 1))
            local endLine = math.max(startLine, math.floor(tonumber(diagnostic.endLine) or startLine))

            local endColumn = tonumber(diagnostic.endColumn)
            if type(endColumn) ~= "number" then
                local length = tonumber(diagnostic.length) or 1
                endColumn = startColumn + math.max(1, math.floor(length))
            end
            endColumn = math.max(startColumn + 1, math.floor(endColumn))

            for line = startLine, endLine do
                local lineStartIndex = lineStarts[line]
                if lineStartIndex then
                    local nextLineStart = lineStarts[line + 1]
                    local lineEndExclusive = nextLineStart and (nextLineStart - 1) or (sourceLength + 1)

                    local segStartColumn = (line == startLine) and startColumn or 1
                    local segEndColumn = (line == endLine) and endColumn or (lineEndExclusive - lineStartIndex + 1)

                    local segStartIndex = math.clamp(
                        lineColumnToIndex(lineStarts, line, segStartColumn, sourceLength),
                        lineStartIndex,
                        lineEndExclusive
                    )

                    local desiredSegEndIndexExclusive = lineColumnToIndex(lineStarts, line, segEndColumn, sourceLength)
                    local minSegEndIndexExclusive = math.min(segStartIndex + 1, sourceLength + 1)
                    local maxSegEndIndexExclusive = math.max(minSegEndIndexExclusive, lineEndExclusive)
                    local segEndIndexExclusive = math.clamp(
                        desiredSegEndIndexExclusive,
                        minSegEndIndexExclusive,
                        maxSegEndIndexExclusive
                    )

                    local beforeText = source:sub(lineStartIndex, segStartIndex - 1)
                    local uptoText = source:sub(lineStartIndex, segEndIndexExclusive - 1)

                    local x1 = TextService:GetTextSize(beforeText, self.options.textSize, Enum.Font.Code, Vector2.new(100000, 100000)).X
                    local x2 = TextService:GetTextSize(uptoText, self.options.textSize, Enum.Font.Code, Vector2.new(100000, 100000)).X
                    local y = ((line - 1) * self.options.lineHeight) + self.options.lineHeight - 2

                    local color = self.options.theme == "Light"
                        and Color3.fromRGB(215, 58, 73)
                        or Color3.fromRGB(244, 71, 71)

                    renderDiagnosticSquiggle(layer, x1, x2, y, color, layer.ZIndex)
                    rendered += 1
                    if rendered >= MAX_RENDERED_ERROR_SQUIGGLES then
                        break
                    end
                end
            end
        end
    end
end

local function refresh(self)
    local source = self.textBox.Text or ""
    local sourceLength = #source
    local largeSourceMode = sourceLength >= LARGE_SOURCE_WORK_LIMIT

    if largeSourceMode then
        self.overlay.Text = escapeRichText(source)
    else
        self.overlay.Text = SyntaxHighlighter.highlight(source, self.options.highlight)
    end

    renderDiagnostics(self)

    local gutterWidth = self.options.showLineNumbers and self.options.gutterWidth or 0
    if self.completionFrame and not self.completionFrame.Visible then
        self.completionFrame.Position = UDim2.new(0, gutterWidth + 8, 0, 36)
    end
    self.lineNumbers.Visible = gutterWidth > 0
    if gutterWidth > 0 then
        self.lineNumbers.Text = buildLineNumbersText(source)
    else
        self.lineNumbers.Text = ""
    end

    local lineCount = countLines(source)
    local contentHeight = math.max(self.scroller.AbsoluteSize.Y, (lineCount * self.options.lineHeight) + 8)

    self.textBox.Size = UDim2.new(1, 0, 0, contentHeight)
    self.overlay.Size = UDim2.new(1, 0, 0, contentHeight)
    self.lineNumbers.Size = UDim2.new(0, math.max(0, gutterWidth - 6), 0, contentHeight)
    self.codeContainer.Position = UDim2.new(0, gutterWidth, 0, 4)
    self.codeContainer.Size = UDim2.new(1, -gutterWidth, 0, contentHeight)
    self.content.Size = UDim2.new(1, -4, 0, contentHeight)
    self.scroller.CanvasSize = UDim2.new(0, 0, 0, contentHeight)

    updateStatus(self)
    if self._updateSelectionHighlight then
        self._updateSelectionHighlight()
    end
    updateCaretPosition(self)
end

local function scheduleRefresh(self)
    self._refreshToken = (self._refreshToken or 0) + 1
    local token = self._refreshToken

    task.defer(function()
        if self._refreshToken ~= token then
            return
        end
        if not self.widget or not self.textBox then
            return
        end
        refresh(self)
    end)
end

function Editor.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, Editor)
    self.plugin = pluginObject

    local tabWidth = options.tabWidth or 4
    local textSize = options.textSize or 16
    local lineHeight = options.lineHeight or measureLineHeight(textSize, Enum.Font.Code)

    self.options = {
        gutterWidth = options.gutterWidth or 52,
        lineHeight = lineHeight,
        textSize = textSize,
        tabWidth = tabWidth,
        tabText = string.rep(" ", tabWidth),
        autoIndent = options.autoIndent ~= false,
        showLineNumbers = options.showLineNumbers ~= false,
        intellisenseEnabled = options.intellisenseEnabled ~= false,
        intellisenseAutoTrigger = options.intellisenseAutoTrigger ~= false,
        intellisenseAutoTriggerOnDot = options.intellisenseAutoTriggerOnDot ~= false,
        intellisenseDebounceMs = options.intellisenseDebounceMs or 250,
        theme = options.theme or "Dark",
        highlight = options.highlight,
        showHoverDebug = options.showHoverDebug == true,
    }
    self.fileName = options.fileName or "<untitled.cs>"
    self.onSourceChanged = nil
    self.onRequestCompletions = nil
    self.onRequestHoverInfo = nil
    self._connections = {}
    self._completionConnections = {}
    self.diagnostics = {}
    self.errorCount = 0
    self.hoverDebugText = ""
    self._pluginMouse = nil

    if self.plugin and self.plugin.GetMouse then
        pcall(function()
            self._pluginMouse = self.plugin:GetMouse()
        end)
    end

    local widgetInfo = DockWidgetPluginGuiInfo.new(
        Enum.InitialDockState.Float,
        true,
        false,
        options.width or 650,
        options.height or 520,
        320,
        260
    )

    local widget = pluginObject:CreateDockWidgetPluginGui("LUSharpEditor", widgetInfo)
    widget.Title = "LUSharp Editor"
    self.widget = widget

    local root = Instance.new("Frame")
    root.Name = "Root"
    root.Size = UDim2.fromScale(1, 1)
    root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    root.BorderSizePixel = 0
    root.Parent = widget
    self.root = root

    local statusBar = Instance.new("TextLabel")
    statusBar.Name = "StatusBar"
    statusBar.Size = UDim2.new(1, 0, 0, 24)
    statusBar.Position = UDim2.new(0, 0, 1, -24)
    statusBar.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
    statusBar.BorderSizePixel = 0
    statusBar.TextXAlignment = Enum.TextXAlignment.Left
    statusBar.TextYAlignment = Enum.TextYAlignment.Center
    statusBar.Font = Enum.Font.Code
    statusBar.TextSize = 14
    statusBar.TextColor3 = Color3.fromRGB(220, 220, 220)
    statusBar.Text = ""
    statusBar.Parent = root
    self.statusLabel = statusBar

    local scroller = Instance.new("ScrollingFrame")
    scroller.Name = "Scroller"
    scroller.Size = UDim2.new(1, 0, 1, -24)
    scroller.Position = UDim2.new(0, 0, 0, 0)
    scroller.CanvasSize = UDim2.new(0, 0, 0, 0)
    scroller.ScrollBarThickness = 10
    scroller.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    scroller.BorderSizePixel = 0
    scroller.AutomaticCanvasSize = Enum.AutomaticSize.None
    scroller.Parent = root
    self.scroller = scroller

    local content = Instance.new("Frame")
    content.Name = "Content"
    content.Size = UDim2.new(1, -4, 1, 0)
    content.Position = UDim2.new(0, 0, 0, 0)
    content.BackgroundTransparency = 1
    content.BorderSizePixel = 0
    content.Parent = scroller
    self.content = content

    local lineNumbers = Instance.new("TextLabel")
    lineNumbers.Name = "LineNumbers"
    lineNumbers.Size = UDim2.new(0, self.options.gutterWidth - 6, 1, 0)
    lineNumbers.Position = UDim2.new(0, 4, 0, 4)
    lineNumbers.BackgroundTransparency = 1
    lineNumbers.BorderSizePixel = 0
    lineNumbers.Font = Enum.Font.Code
    lineNumbers.TextSize = self.options.textSize
    lineNumbers.LineHeight = self.options.lineHeight / self.options.textSize
    lineNumbers.TextColor3 = Color3.fromRGB(128, 128, 128)
    lineNumbers.TextXAlignment = Enum.TextXAlignment.Right
    lineNumbers.TextYAlignment = Enum.TextYAlignment.Top
    lineNumbers.TextWrapped = false
    lineNumbers.RichText = false
    lineNumbers.Text = "1"
    lineNumbers.Parent = content
    self.lineNumbers = lineNumbers

    local codeContainer = Instance.new("Frame")
    codeContainer.Name = "CodeContainer"
    codeContainer.Size = UDim2.new(1, -self.options.gutterWidth, 1, 0)
    codeContainer.Position = UDim2.new(0, self.options.gutterWidth, 0, 4)
    codeContainer.BackgroundTransparency = 1
    codeContainer.BorderSizePixel = 0
    codeContainer.Parent = content
    self.codeContainer = codeContainer

    local selectionLayer = Instance.new("Frame")
    selectionLayer.Name = "Selection"
    selectionLayer.Size = UDim2.fromScale(1, 1)
    selectionLayer.BackgroundTransparency = 1
    selectionLayer.BorderSizePixel = 0
    selectionLayer.ClipsDescendants = true
    selectionLayer.ZIndex = 1
    selectionLayer.Parent = codeContainer
    self.selectionLayer = selectionLayer

    local diagnosticLayer = Instance.new("Frame")
    diagnosticLayer.Name = "Diagnostics"
    diagnosticLayer.Size = UDim2.fromScale(1, 1)
    diagnosticLayer.BackgroundTransparency = 1
    diagnosticLayer.BorderSizePixel = 0
    diagnosticLayer.ClipsDescendants = true
    diagnosticLayer.ZIndex = 4
    diagnosticLayer.Parent = codeContainer
    self.diagnosticLayer = diagnosticLayer

    local overlay = Instance.new("TextLabel")
    overlay.Name = "Overlay"
    overlay.Size = UDim2.fromScale(1, 1)
    overlay.BackgroundTransparency = 1
    overlay.BorderSizePixel = 0
    overlay.TextXAlignment = Enum.TextXAlignment.Left
    overlay.TextYAlignment = Enum.TextYAlignment.Top
    overlay.TextWrapped = false
    overlay.Font = Enum.Font.Code
    overlay.TextSize = self.options.textSize
    overlay.LineHeight = self.options.lineHeight / self.options.textSize
    overlay.TextColor3 = Color3.fromRGB(220, 220, 220)
    overlay.RichText = true
    overlay.Text = ""
    overlay.ZIndex = 3
    overlay.Parent = codeContainer
    self.overlay = overlay

    local textBox = Instance.new("TextBox")
    textBox.Name = "Input"
    textBox.Size = UDim2.fromScale(1, 1)
    textBox.BackgroundTransparency = 1
    textBox.BorderSizePixel = 0
    textBox.ClearTextOnFocus = false
    textBox.MultiLine = true
    textBox.TextXAlignment = Enum.TextXAlignment.Left
    textBox.TextYAlignment = Enum.TextYAlignment.Top
    textBox.TextWrapped = false
    textBox.Font = Enum.Font.Code
    textBox.TextSize = self.options.textSize
    textBox.LineHeight = self.options.lineHeight / self.options.textSize
    textBox.TextColor3 = Color3.fromRGB(255, 255, 255)
    textBox.TextTransparency = 1
    textBox.CursorPosition = 1
    textBox.SelectionStart = -1
    textBox.Text = ""
    textBox.ZIndex = 5
    textBox.Parent = codeContainer
    self.textBox = textBox

    local function clearSelectionHighlight()
        for _, child in ipairs(selectionLayer:GetChildren()) do
            child:Destroy()
        end
    end

    local function updateSelectionHighlight()
        clearSelectionHighlight()

        local sel = textBox.SelectionStart
        local cursor = textBox.CursorPosition
        if sel == -1 or sel == cursor then
            return
        end

        local text = textBox.Text
        if cursor == nil or cursor < 1 then
            cursor = #text + 1
        end
        if sel < 1 then
            sel = #text + 1
        end

        local startPos = math.clamp(math.min(sel, cursor), 1, #text + 1)
        local endPos = math.clamp(math.max(sel, cursor), 1, #text + 1)
        if endPos <= startPos then
            return
        end

        local selectionColor = self.options.theme == "Light" and Color3.fromRGB(173, 214, 255) or Color3.fromRGB(38, 79, 120)
        local selectionTransparency = self.options.theme == "Light" and 0.35 or 0.6

        local lineStarts = { 1 }
        for i = 1, #text do
            if text:sub(i, i) == "\n" then
                table.insert(lineStarts, i + 1)
            end
        end

        local function findLine(pos)
            local lo = 1
            local hi = #lineStarts
            while lo <= hi do
                local mid = math.floor((lo + hi) / 2)
                local s = lineStarts[mid]
                local nextS = lineStarts[mid + 1]
                if s <= pos and (nextS == nil or pos < nextS) then
                    return mid
                elseif pos < s then
                    hi = mid - 1
                else
                    lo = mid + 1
                end
            end
            return #lineStarts
        end

        local startLine = findLine(startPos)
        local endLine = findLine(endPos)

        for line = startLine, endLine do
            local lineStart = lineStarts[line]
            local nextStart = lineStarts[line + 1]
            local lineEndExclusive = nextStart and (nextStart - 1) or (#text + 1)

            local segStart = (line == startLine) and startPos or lineStart
            local segEnd = (line == endLine) and endPos or lineEndExclusive

            segStart = math.clamp(segStart, lineStart, lineEndExclusive)
            segEnd = math.clamp(segEnd, segStart, lineEndExclusive)

            if segEnd > segStart then
                local before = text:sub(lineStart, segStart - 1)
                local upto = text:sub(lineStart, segEnd - 1)

                local x1 = TextService:GetTextSize(before, self.options.textSize, Enum.Font.Code, Vector2.new(100000, 100000)).X
                local x2 = TextService:GetTextSize(upto, self.options.textSize, Enum.Font.Code, Vector2.new(100000, 100000)).X

                local frame = Instance.new("Frame")
                frame.Name = "Sel" .. tostring(line)
                frame.BorderSizePixel = 0
                frame.BackgroundColor3 = selectionColor
                frame.BackgroundTransparency = selectionTransparency
                frame.Position = UDim2.new(0, x1, 0, (line - 1) * self.options.lineHeight)
                frame.Size = UDim2.new(0, math.max(1, x2 - x1), 0, self.options.lineHeight)
                frame.ZIndex = selectionLayer.ZIndex
                frame.Parent = selectionLayer
            end
        end
    end

    self._updateSelectionHighlight = updateSelectionHighlight
    updateSelectionHighlight()

    table.insert(self._connections, textBox:GetPropertyChangedSignal("SelectionStart"):Connect(updateSelectionHighlight))
    table.insert(self._connections, textBox:GetPropertyChangedSignal("CursorPosition"):Connect(updateSelectionHighlight))

    self._caretBlinkToken = 0
    self._charWidth = measureCharWidth(self.options.textSize, Enum.Font.Code)
    self._suppressTextChange = false
    self._lastText = textBox.Text
    self._lastCursorPos = textBox.CursorPosition
    self._pendingAutoIndent = nil
    self._completionRequestToken = 0
    self._hoverRequestToken = 0
    self._nextHoverPollAt = 0
    self._lastMouseMoveAt = 0
    self._completionShownAt = 0
    self._activeCompletionIndex = nil
    self._completionAnchorStart = nil
    self._completionAnchorEnd = nil
    self._completionAnchorText = nil
    self._refreshToken = 0
    self._deferNextSuppressedRefresh = false

    local caret = Instance.new("Frame")
    caret.Name = "Caret"
    caret.BackgroundColor3 = Color3.fromRGB(235, 235, 235)
    caret.BorderSizePixel = 0
    caret.ZIndex = 6
    caret.Visible = false
    caret.Parent = codeContainer
    self.caret = caret

    local completionFrame = Instance.new("Frame")
    completionFrame.Name = "Completions"
    completionFrame.BackgroundColor3 = Color3.fromRGB(45, 45, 45)
    completionFrame.BorderSizePixel = 0
    completionFrame.Position = UDim2.new(0, self.options.gutterWidth + 8, 0, 36)
    completionFrame.Size = UDim2.new(0, 240, 0, 0)
    completionFrame.Visible = false
    completionFrame.ZIndex = 20
    completionFrame.Parent = root
    self.completionFrame = completionFrame

    local completionLayout = Instance.new("UIListLayout")
    completionLayout.SortOrder = Enum.SortOrder.LayoutOrder
    completionLayout.Padding = UDim.new(0, 2)
    completionLayout.Parent = completionFrame
    self.completionLayout = completionLayout

    local completionInfoFrame = Instance.new("Frame")
    completionInfoFrame.Name = "CompletionInfo"
    completionInfoFrame.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
    completionInfoFrame.BorderSizePixel = 0
    completionInfoFrame.Size = UDim2.new(0, 340, 0, 84)
    completionInfoFrame.Visible = false
    completionInfoFrame.ZIndex = 25
    completionInfoFrame.Parent = root
    self.completionInfoFrame = completionInfoFrame

    local completionInfoIcon = Instance.new("TextLabel")
    completionInfoIcon.Name = "Icon"
    completionInfoIcon.Size = UDim2.new(0, 24, 0, 24)
    completionInfoIcon.Position = UDim2.new(0, 8, 0, 8)
    completionInfoIcon.BackgroundColor3 = Color3.fromRGB(66, 66, 66)
    completionInfoIcon.BorderSizePixel = 0
    completionInfoIcon.Font = Enum.Font.SourceSansBold
    completionInfoIcon.TextSize = 14
    completionInfoIcon.TextColor3 = Color3.fromRGB(230, 230, 230)
    completionInfoIcon.Text = "•"
    completionInfoIcon.ZIndex = completionInfoFrame.ZIndex + 1
    completionInfoIcon.Parent = completionInfoFrame
    self.completionInfoIcon = completionInfoIcon

    local completionInfoText = Instance.new("TextLabel")
    completionInfoText.Name = "Text"
    completionInfoText.Size = UDim2.new(1, -44, 1, -12)
    completionInfoText.Position = UDim2.new(0, 36, 0, 6)
    completionInfoText.BackgroundTransparency = 1
    completionInfoText.BorderSizePixel = 0
    completionInfoText.Font = Enum.Font.Code
    completionInfoText.TextSize = 13
    completionInfoText.TextColor3 = Color3.fromRGB(220, 220, 220)
    completionInfoText.TextXAlignment = Enum.TextXAlignment.Left
    completionInfoText.TextYAlignment = Enum.TextYAlignment.Top
    completionInfoText.TextWrapped = true
    completionInfoText.Text = ""
    completionInfoText.ZIndex = completionInfoFrame.ZIndex + 1
    completionInfoText.Parent = completionInfoFrame
    self.completionInfoText = completionInfoText

    local hoverInfoFrame = Instance.new("Frame")
    hoverInfoFrame.Name = "HoverInfo"
    hoverInfoFrame.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
    hoverInfoFrame.BorderSizePixel = 0
    hoverInfoFrame.Size = UDim2.new(0, 340, 0, 84)
    hoverInfoFrame.Visible = false
    hoverInfoFrame.ZIndex = 26
    hoverInfoFrame.Parent = root
    self.hoverInfoFrame = hoverInfoFrame

    local hoverInfoIcon = Instance.new("TextLabel")
    hoverInfoIcon.Name = "Icon"
    hoverInfoIcon.Size = UDim2.new(0, 24, 0, 24)
    hoverInfoIcon.Position = UDim2.new(0, 8, 0, 8)
    hoverInfoIcon.BackgroundColor3 = Color3.fromRGB(66, 66, 66)
    hoverInfoIcon.BorderSizePixel = 0
    hoverInfoIcon.Font = Enum.Font.SourceSansBold
    hoverInfoIcon.TextSize = 14
    hoverInfoIcon.TextColor3 = Color3.fromRGB(230, 230, 230)
    hoverInfoIcon.Text = "•"
    hoverInfoIcon.ZIndex = hoverInfoFrame.ZIndex + 1
    hoverInfoIcon.Parent = hoverInfoFrame
    self.hoverInfoIcon = hoverInfoIcon

    local hoverInfoText = Instance.new("TextLabel")
    hoverInfoText.Name = "Text"
    hoverInfoText.Size = UDim2.new(1, -44, 1, -12)
    hoverInfoText.Position = UDim2.new(0, 36, 0, 6)
    hoverInfoText.BackgroundTransparency = 1
    hoverInfoText.BorderSizePixel = 0
    hoverInfoText.Font = Enum.Font.Code
    hoverInfoText.TextSize = 13
    hoverInfoText.TextColor3 = Color3.fromRGB(220, 220, 220)
    hoverInfoText.TextXAlignment = Enum.TextXAlignment.Left
    hoverInfoText.TextYAlignment = Enum.TextYAlignment.Top
    hoverInfoText.TextWrapped = true
    hoverInfoText.Text = ""
    hoverInfoText.ZIndex = hoverInfoFrame.ZIndex + 1
    hoverInfoText.Parent = hoverInfoFrame
    self.hoverInfoText = hoverInfoText

    table.insert(self._connections, textBox:GetPropertyChangedSignal("Text"):Connect(function()
        if self._suppressTextChange then
            self._suppressTextChange = false
            self._pendingAutoIndent = nil
            self._lastText = textBox.Text
            self._lastCursorPos = textBox.CursorPosition

            local deferRefresh = self._deferNextSuppressedRefresh
            self._deferNextSuppressedRefresh = false

            if deferRefresh then
                scheduleRefresh(self)
            else
                refresh(self)
            end

            if self.onSourceChanged then
                self.onSourceChanged(textBox.Text)
            end
            self:hideCompletions()
            self:hideHoverInfo()
            return
        end

        local prevText = self._lastText or ""
        local prevCursor = self._lastCursorPos

        local skipEditorTransforms = self._skipEditorTransformsOnce == true
        if skipEditorTransforms then
            self._skipEditorTransformsOnce = false
            self._pendingAutoIndent = nil
        else
            local newText, newCursor, changed = EditorTextUtils.autoDedentClosingBrace(textBox.Text, textBox.CursorPosition, self.options.tabText)
            if changed then
                self._suppressTextChange = true
                textBox.Text = newText
                textBox.CursorPosition = newCursor
                return
            end
        end

        local pendingIndent = false
        if not skipEditorTransforms and self.options.autoIndent and prevText ~= textBox.Text then
            local indent = EditorTextUtils.computeAutoIndentInsertion(prevText, prevCursor, textBox.Text, textBox.CursorPosition, self.options.tabText)
            if indent ~= "" then
                self._pendingAutoIndent = nil
                self._suppressTextChange = true
                insertAtCursor(textBox, indent)
                return
            end

            local startPos, _removedText, insertedText = findSingleSplice(prevText, textBox.Text)
            if insertedText == "\n" then
                self._pendingAutoIndent = {
                    newText = textBox.Text,
                    expectedCursor = startPos and (startPos + 1) or nil,
                }
                pendingIndent = true
            else
                self._pendingAutoIndent = nil
            end
        else
            self._pendingAutoIndent = nil
        end

        if not pendingIndent then
            self._lastText = textBox.Text
            self._lastCursorPos = textBox.CursorPosition
        end

        refresh(self)
        if self.onSourceChanged then
            self.onSourceChanged(textBox.Text)
        end
        self:hideHoverInfo()

        if pendingIndent then
            self:hideCompletions()
            return
        end

        if self.options.intellisenseEnabled == false or not self.onRequestCompletions then
            self:hideCompletions()
            return
        end

        local _startPos, removedText, insertedText = findSingleSplice(prevText, textBox.Text)

        local isDeletion = type(removedText) == "string" and removedText ~= "" and (insertedText == nil or insertedText == "")
        if isDeletion then
            self._completionRequestToken = (self._completionRequestToken or 0) + 1
            self:hideCompletions()
            return
        end

        local insertedDot = insertedText == "."

        local triggerOnDot = insertedDot and self.options.intellisenseAutoTriggerOnDot ~= false
        local autoTrigger = self.options.intellisenseAutoTrigger ~= false
        local shouldAutoTrigger = autoTrigger and EditorTextUtils.shouldAutoTriggerCompletions(textBox.Text, textBox.CursorPosition)

        if triggerOnDot or shouldAutoTrigger then
            self._completionRequestToken = (self._completionRequestToken or 0) + 1
            local token = self._completionRequestToken

            if self.completionFrame then
                self.completionFrame.Visible = false
            end

            local delaySeconds = 0
            if not triggerOnDot then
                local ms = tonumber(self.options.intellisenseDebounceMs) or 250
                delaySeconds = math.max(0, ms) / 1000
            end

            task.delay(delaySeconds, function()
                if self._completionRequestToken ~= token then
                    return
                end

                if self.textBox and self.textBox:IsFocused() and self.onRequestCompletions then
                    self.onRequestCompletions()
                end
            end)
        else
            self:hideCompletions()
        end
    end))

    table.insert(self._connections, textBox:GetPropertyChangedSignal("CursorPosition"):Connect(function()
        if self._pendingAutoIndent and self.options.autoIndent then
            local pending = self._pendingAutoIndent

            if textBox.Text == pending.newText then
                local atExpectedCursor = pending.expectedCursor == nil or textBox.CursorPosition == pending.expectedCursor
                if atExpectedCursor then
                    local indent = EditorTextUtils.computeAutoIndentInsertion(self._lastText, self._lastCursorPos, textBox.Text, textBox.CursorPosition, self.options.tabText)
                    self._pendingAutoIndent = nil

                    if indent ~= "" then
                        self._suppressTextChange = true
                        insertAtCursor(textBox, indent)
                        return
                    end

                    self._lastText = textBox.Text
                    self._lastCursorPos = textBox.CursorPosition
                end
            else
                self._pendingAutoIndent = nil
                self._lastText = textBox.Text
                self._lastCursorPos = textBox.CursorPosition
            end
        elseif not self._pendingAutoIndent then
            self._lastCursorPos = textBox.CursorPosition
        end

        updateStatus(self)
        updateCaretPosition(self)
    end))

    table.insert(self._connections, textBox.Focused:Connect(function()
        updateStatus(self)
        updateCaretPosition(self)
        startCaretBlink(self)
    end))

    table.insert(self._connections, textBox.FocusLost:Connect(function()
        updateStatus(self)
        self:hideCompletions()
        self:hideHoverInfo()
        stopCaretBlink(self)
    end))

    table.insert(self._connections, UserInputService.InputChanged:Connect(function(input)
        if input.UserInputType ~= Enum.UserInputType.MouseMovement then
            return
        end

        self._lastMouseMoveAt = os.clock()

        if not self.widget or not self.widget.Enabled then
            self:hideHoverInfo()
            return
        end

        if not self.onRequestHoverInfo then
            setHoverDebug(self, "Hover: no provider")
            self:hideHoverInfo()
            return
        end

        local screenPosition = rememberMousePosition(self, input.Position)
        if not screenPosition then
            setHoverDebug(self, "Hover: waiting mouse")
            self:hideHoverInfo()
            return
        end

        local cursorPos = cursorPositionFromScreenPoint(self, screenPosition)
        if not cursorPos then
            setHoverDebug(self, formatOutsideEditorDebug(self))
            self:hideHoverInfo()
            return
        end

        setHoverDebug(self, string.format("Hover: probing @%d", cursorPos))

        self._hoverRequestToken = (self._hoverRequestToken or 0) + 1
        local token = self._hoverRequestToken

        task.delay(HOVER_REQUEST_DELAY_SECONDS, function()
            if self._hoverRequestToken ~= token then
                return
            end

            if not self.widget or not self.widget.Enabled then
                self:hideHoverInfo()
                return
            end

            local latestCursorPos, latestAnchorPos, latestSource = resolveHoverCursorFromCandidates(self)

            if not latestCursorPos then
                setHoverDebug(self, "Hover: waiting mouse")
                self:hideHoverInfo()
                return
            end

            setHoverDebug(self, string.format("Hover: probing @%d (%s)", latestCursorPos, tostring(latestSource or "mouse")))

            local info = self.onRequestHoverInfo and self.onRequestHoverInfo(latestCursorPos)
            if type(info) == "table" then
                self:_showHoverInfo(info, latestAnchorPos or getHoverAnchorScreenPosition(self))
            else
                setHoverDebug(self, string.format("Hover: miss @%d", latestCursorPos))
                self:hideHoverInfo()
            end
        end)
    end))

    table.insert(self._connections, RunService.Heartbeat:Connect(function()
        if not self.widget or not self.widget.Enabled then
            return
        end

        if not self.onRequestHoverInfo then
            setHoverDebug(self, "Hover: no provider")
            return
        end

        local now = os.clock()
        if now < (self._nextHoverPollAt or 0) then
            return
        end
        self._nextHoverPollAt = now + HOVER_POLL_INTERVAL_SECONDS

        local cursorPos, anchorPos, sourceLabel = resolveHoverCursorFromCandidates(self)

        if not cursorPos then
            setHoverDebug(self, "Hover: waiting mouse")
            self:hideHoverInfo()
            return
        end

        setHoverDebug(self, string.format("Hover: probing @%d (%s)", cursorPos, tostring(sourceLabel or "mouse")))

        local info = self.onRequestHoverInfo(cursorPos)
        if type(info) == "table" then
            self:_showHoverInfo(info, anchorPos or getHoverAnchorScreenPosition(self))
        else
            setHoverDebug(self, string.format("Hover: miss @%d", cursorPos))
            self:hideHoverInfo()
        end
    end))

    table.insert(self._connections, UserInputService.InputBegan:Connect(function(input, _gameProcessed)
        local completionVisibleNow = self.completionFrame and self.completionFrame.Visible
        if not textBox:IsFocused() then
            local isCompletionAcceptKey = input.KeyCode == Enum.KeyCode.Tab or input.KeyCode == Enum.KeyCode.Return or input.KeyCode == Enum.KeyCode.KeypadEnter
            if not completionVisibleNow or not isCompletionAcceptKey then
                return
            end
        end

        self:hideHoverInfo()

        local ctrlDown = UserInputService:IsKeyDown(Enum.KeyCode.LeftControl) or UserInputService:IsKeyDown(Enum.KeyCode.RightControl)
        local altDown = UserInputService:IsKeyDown(Enum.KeyCode.LeftAlt) or UserInputService:IsKeyDown(Enum.KeyCode.RightAlt)

        if EditorTextUtils.isUndoRedoShortcut(input.KeyCode, ctrlDown, altDown) then
            self._skipEditorTransformsOnce = true
            self._pendingAutoIndent = nil
            self:hideCompletions()

            task.defer(function()
                if self.textBox and self.textBox.Parent then
                    self._lastText = self.textBox.Text
                    self._lastCursorPos = self.textBox.CursorPosition
                    updateStatus(self)
                    updateCaretPosition(self)
                end
            end)
            return
        end

        local function requestCompletions(defer)
            if self.options.intellisenseEnabled == false then
                self:hideCompletions()
                return
            end

            if not self.onRequestCompletions then
                return
            end

            self._completionRequestToken = (self._completionRequestToken or 0) + 1

            if defer then
                task.defer(function()
                    if self.textBox and self.textBox:IsFocused() and self.onRequestCompletions then
                        self.onRequestCompletions()
                    end
                end)
            else
                self.onRequestCompletions()
            end
        end

        if input.KeyCode == Enum.KeyCode.Tab then
            local completionVisible = self.completionFrame and self.completionFrame.Visible
            local completionCandidates = self._visibleCompletions and #self._visibleCompletions > 0

            local requestedCompletions = nil
            if not completionVisible and not completionCandidates and self.options.intellisenseEnabled ~= false and self.onRequestCompletions then
                local cursorPos = textBox.CursorPosition
                if type(cursorPos) ~= "number" or cursorPos < 1 then
                    cursorPos = #textBox.Text + 1
                end

                local before = textBox.Text:sub(1, math.max(0, cursorPos - 1))
                local canRequest = before:match("[%a_][%w_]*$") or before:match("%.[%a_][%w_]*$") or before:match("%.$")
                if canRequest then
                    requestedCompletions = self.onRequestCompletions()
                    completionVisible = self.completionFrame and self.completionFrame.Visible
                    completionCandidates = (type(requestedCompletions) == "table" and #requestedCompletions > 0) or (self._visibleCompletions and #self._visibleCompletions > 0)
                end
            end

            if completionVisible or completionCandidates then
                local label = EditorTextUtils.resolveCompletionLabel(requestedCompletions, self._activeCompletionIndex) or self:_getFirstCompletionLabel()
                if label then
                    local beforeText = textBox.Text
                    local beforeCursor = textBox.CursorPosition

                    task.defer(function()
                        if self.textBox and self.textBox.Parent then
                            if self.textBox.Text ~= beforeText then
                                self._pendingAutoIndent = nil
                                self._suppressTextChange = true
                                self.textBox.Text = beforeText
                                self.textBox.CursorPosition = beforeCursor
                                self.textBox.SelectionStart = -1
                            end

                            self:_applyCompletion(label)
                            self:hideCompletions()
                            self:focus()
                        end
                    end)
                    return
                end
                return
            end

            insertAtCursor(textBox, self.options.tabText)
            self:hideCompletions()
            updateCaretPosition(self)
            textBox:CaptureFocus()
        elseif input.KeyCode == Enum.KeyCode.Return or input.KeyCode == Enum.KeyCode.KeypadEnter then
            local completionVisible = self.completionFrame and self.completionFrame.Visible
            local completionCandidates = self._visibleCompletions and #self._visibleCompletions > 0

            if completionVisible or completionCandidates then
                local label = self:_getFirstCompletionLabel()
                if label then
                    local beforeText = textBox.Text
                    local beforeCursor = textBox.CursorPosition

                    task.defer(function()
                        if self.textBox and self.textBox.Parent then
                            if self.textBox.Text ~= beforeText then
                                self._pendingAutoIndent = nil
                                self._suppressTextChange = true
                                self.textBox.Text = beforeText
                                self.textBox.CursorPosition = beforeCursor
                                self.textBox.SelectionStart = -1
                            end

                            self:_applyCompletion(label)
                            self:hideCompletions()
                            self:focus()
                        end
                    end)
                    return
                end
            end
        elseif input.KeyCode == Enum.KeyCode.Delete and (ctrlDown or altDown) then
            local beforeText = textBox.Text
            local beforeCursor = textBox.CursorPosition
            local beforeSelection = textBox.SelectionStart

            task.defer(function()
                if not self.textBox or not self.textBox.Parent or not self.textBox:IsFocused() then
                    return
                end

                local nextText, nextCursor, nextSelection, changed = EditorTextUtils.computeCtrlDeleteEdit(beforeText, beforeCursor, beforeSelection)

                if changed then
                    self._pendingAutoIndent = nil

                    if self.textBox.Text ~= nextText then
                        self._suppressTextChange = true
                        self.textBox.Text = nextText
                    end

                    if self.textBox.CursorPosition ~= nextCursor then
                        self.textBox.CursorPosition = nextCursor
                    end

                    if self.textBox.SelectionStart ~= nextSelection then
                        self.textBox.SelectionStart = nextSelection
                    end
                end
            end)
        elseif input.KeyCode == Enum.KeyCode.F1 then
            requestCompletions(false)
        elseif input.KeyCode == Enum.KeyCode.Period and ctrlDown then
            requestCompletions(true)
        elseif input.KeyCode == Enum.KeyCode.Space and ctrlDown then
            requestCompletions(true)
        end
    end))

    table.insert(self._connections, widget:GetPropertyChangedSignal("AbsoluteSize"):Connect(function()
        refresh(self)
    end))

    refresh(self)

    return self
end

function Editor:setSource(source, options)
    source = source or ""
    options = options or {}

    local changed = self.textBox.Text ~= source
    local deferRefresh = options.deferRefresh == true

    if changed then
        self._pendingAutoIndent = nil
        self._deferNextSuppressedRefresh = deferRefresh
        self._suppressTextChange = true
    end

    self.textBox.Text = source
    self.textBox.CursorPosition = #self.textBox.Text + 1

    if not changed then
        if deferRefresh then
            scheduleRefresh(self)
        else
            refresh(self)
        end
    end
end

function Editor:getSource()
    return self.textBox.Text
end

function Editor:setFilename(name)
    self.fileName = name or "<untitled.cs>"
    updateStatus(self)
end

function Editor:setOnSourceChanged(callback)
    self.onSourceChanged = callback
end

function Editor:setOnRequestCompletions(callback)
    self.onRequestCompletions = callback
end

function Editor:setOnRequestHoverInfo(callback)
    self.onRequestHoverInfo = callback
end

function Editor:hideHoverInfo()
    self._hoverRequestToken = (self._hoverRequestToken or 0) + 1

    if self.hoverInfoFrame then
        self.hoverInfoFrame.Visible = false
    end
end

function Editor:_showHoverInfo(info, screenPosition)
    if not self.hoverInfoFrame or not self.hoverInfoIcon or not self.hoverInfoText or not self.root then
        return
    end

    local label = tostring((info and info.label) or "")
    if label == "" then
        self:hideHoverInfo()
        return
    end

    local kind = tostring((info and info.kind) or "symbol")
    local detail = tostring((info and info.detail) or "")
    local documentation = tostring((info and info.documentation) or "")

    local visual = getCompletionKindVisual(kind)
    self.hoverInfoIcon.Text = visual.text
    self.hoverInfoIcon.TextColor3 = visual.color

    local lines = { label }
    if detail ~= "" then
        table.insert(lines, detail)
    end
    if documentation ~= "" then
        table.insert(lines, documentation)
    end
    self.hoverInfoText.Text = table.concat(lines, "\n")

    local rootPos = self.root.AbsolutePosition
    local frame = self.hoverInfoFrame

    local x = screenPosition.X - rootPos.X + 14
    local y = screenPosition.Y - rootPos.Y + 18

    local maxX = math.max(4, self.root.AbsoluteSize.X - frame.AbsoluteSize.X - 4)
    local maxY = math.max(4, self.root.AbsoluteSize.Y - frame.AbsoluteSize.Y - 4)

    x = math.clamp(x, 4, maxX)
    y = math.clamp(y, 4, maxY)

    frame.Position = UDim2.new(0, x, 0, y)
    frame.Visible = true

    setHoverDebug(self, string.format("Hover: %s [%s]", label, kind))
end

function Editor:hideCompletions()
    self._completionRequestToken = (self._completionRequestToken or 0) + 1
    self._completionShownAt = 0
    self._activeCompletionIndex = nil
    self._completionRows = nil
    self._completionAnchorStart = nil
    self._completionAnchorEnd = nil
    self._completionAnchorText = nil
    self._visibleCompletions = nil

    if self.completionInfoFrame then
        self.completionInfoFrame.Visible = false
    end

    if not self.completionFrame then
        return
    end

    self.completionFrame.Visible = false

    for _, connection in ipairs(self._completionConnections) do
        connection:Disconnect()
    end
    self._completionConnections = {}

    for _, child in ipairs(self.completionFrame:GetChildren()) do
        if not child:IsA("UIListLayout") then
            child:Destroy()
        end
    end

    self.completionFrame.Size = UDim2.new(0, 240, 0, 0)
end

function Editor:_getFirstCompletionLabel()
    if self._visibleCompletions and type(self._visibleCompletions) == "table" then
        local label = EditorTextUtils.resolveCompletionLabel(self._visibleCompletions, self._activeCompletionIndex)
        if label then
            return label
        end
    end

    if self.completionFrame then
        local rowIndex = tonumber(self._activeCompletionIndex) or 1
        local rowName = "Completion" .. tostring(math.max(1, math.floor(rowIndex)))
        local row = self.completionFrame:FindFirstChild(rowName) or self.completionFrame:FindFirstChild("Completion1")
        if row then
            local labelNode = row:FindFirstChild("Label")
            if labelNode and labelNode:IsA("TextLabel") then
                local text = labelNode.Text
                if type(text) == "string" and text ~= "" then
                    return text
                end
            end
        end
    end

    return nil
end

function Editor:_setActiveCompletionIndex(index)
    if type(index) ~= "number" then
        self._activeCompletionIndex = nil
    else
        index = math.floor(index)
        if index < 1 then
            self._activeCompletionIndex = nil
        else
            self._activeCompletionIndex = index
        end
    end

    if not self._completionRows then
        return
    end

    for i, row in ipairs(self._completionRows) do
        if row and row.Parent then
            if self._activeCompletionIndex and i == self._activeCompletionIndex then
                row.BackgroundColor3 = Color3.fromRGB(70, 70, 70)
            else
                row.BackgroundColor3 = Color3.fromRGB(55, 55, 55)
            end
        end
    end
end

function Editor:_showCompletionInfo(item, rowButton)
    if not self.completionInfoFrame or not self.completionInfoIcon or not self.completionInfoText then
        return
    end

    local label = tostring((item and item.label) or "")
    local kind = tostring((item and item.kind) or "symbol")
    local detail = tostring((item and item.detail) or "")
    local documentation = tostring((item and item.documentation) or "")

    local visual = getCompletionKindVisual(kind)
    self.completionInfoIcon.Text = visual.text
    self.completionInfoIcon.TextColor3 = visual.color

    local lines = { label }
    if detail ~= "" then
        table.insert(lines, detail)
    end
    if documentation ~= "" then
        table.insert(lines, documentation)
    end
    self.completionInfoText.Text = table.concat(lines, "\n")

    local frame = self.completionInfoFrame
    if rowButton and rowButton.AbsolutePosition and self.root and self.root.AbsolutePosition then
        local rowPos = rowButton.AbsolutePosition
        local rowSize = rowButton.AbsoluteSize
        local rootPos = self.root.AbsolutePosition

        local x = rowPos.X - rootPos.X + rowSize.X + 6
        local y = rowPos.Y - rootPos.Y

        local maxX = math.max(4, self.root.AbsoluteSize.X - frame.AbsoluteSize.X - 4)
        local maxY = math.max(4, self.root.AbsoluteSize.Y - frame.AbsoluteSize.Y - 4)

        x = math.clamp(x, 4, maxX)
        y = math.clamp(y, 4, maxY)

        frame.Position = UDim2.new(0, x, 0, y)
    end

    frame.Visible = true
end

function Editor:setDiagnostics(diagnostics)
    if type(diagnostics) ~= "table" then
        self.diagnostics = {}
    else
        self.diagnostics = diagnostics
    end

    local errorCount = 0
    for _, diagnostic in ipairs(self.diagnostics) do
        if string.lower(tostring(diagnostic.severity or "")) == "error" then
            errorCount += 1
        end
    end
    self.errorCount = errorCount

    refresh(self)
end

function Editor:_applyCompletion(label)
    local text = self.textBox.Text
    local cursor = self.textBox.CursorPosition

    local startPos = nil
    local endPos = nil

    if self._completionAnchorStart and self._completionAnchorEnd and self._completionAnchorText == text then
        startPos = self._completionAnchorStart
        endPos = self._completionAnchorEnd
    else
        startPos, endPos = EditorTextUtils.computeCompletionReplacementRange(text, cursor)
    end

    self._pendingAutoIndent = nil
    self._suppressTextChange = true
    replaceTextRange(self.textBox, startPos, endPos, label)
    updateCaretPosition(self)
end

function Editor:showCompletions(items)
    self:hideCompletions()

    if type(items) ~= "table" or #items == 0 then
        return
    end

    self._visibleCompletions = items
    self._completionRows = {}
    self:_setActiveCompletionIndex(nil)

    local anchorStart, anchorEnd = EditorTextUtils.computeCompletionReplacementRange(self.textBox.Text, self.textBox.CursorPosition)
    self._completionAnchorStart = anchorStart
    self._completionAnchorEnd = anchorEnd
    self._completionAnchorText = self.textBox.Text

    local maxItems = math.min(#items, 8)
    self.completionFrame.Size = UDim2.new(0, 240, 0, (maxItems * 22) + (maxItems - 1) * 2 + 4)

    do
        local caret = self.caret
        local root = self.root
        if caret and root then
            local rootSize = root.AbsoluteSize
            if rootSize.X > 0 and rootSize.Y > 0 then
                updateCaretPosition(self)

                local rootPos = root.AbsolutePosition
                local localCaretX = nil
                local localCaretY = nil

                if caret.AbsolutePosition then
                    local caretAbs = caret.AbsolutePosition
                    localCaretX = caretAbs.X - rootPos.X
                    localCaretY = caretAbs.Y - rootPos.Y
                else
                    local containerPos = self.codeContainer and self.codeContainer.AbsolutePosition or rootPos
                    local caretX, caretY = computeCaretLocalPosition(self.textBox.Text, self.textBox.CursorPosition, self.options.textSize, self.options.lineHeight)
                    self._caretLocalX = caretX
                    self._caretLocalY = caretY
                    localCaretX = (containerPos.X - rootPos.X) + caretX
                    localCaretY = (containerPos.Y - rootPos.Y) + caretY
                end

                local x = localCaretX
                local y = localCaretY + self.options.lineHeight + 2

                local frameW = self.completionFrame.AbsoluteSize.X
                local frameH = self.completionFrame.AbsoluteSize.Y

                local maxX = rootSize.X - frameW - 4
                local maxY = (rootSize.Y - 24) - frameH - 4

                x = math.clamp(x, 4, math.max(4, maxX))

                if y > maxY and localCaretY - frameH - 2 >= 4 then
                    y = localCaretY - frameH - 2
                end
                y = math.clamp(y, 4, math.max(4, maxY))

                self.completionFrame.Position = UDim2.new(0, x, 0, y)
            end
        end
    end

    self._completionShownAt = os.clock()
    self.completionFrame.Visible = true

    for i = 1, maxItems do
        local item = items[i]
        local label = item and item.label or nil
        if type(label) ~= "string" or label == "" then
            label = tostring(item)
        end

        local detail = item and item.detail or nil
        if type(detail) ~= "string" then
            detail = ""
        end

        local visual = getCompletionKindVisual(item and item.kind)

        local button = Instance.new("TextButton")
        button.Name = "Completion" .. tostring(i)
        button.Size = UDim2.new(1, 0, 0, 22)
        button.BackgroundColor3 = Color3.fromRGB(55, 55, 55)
        button.BorderSizePixel = 0
        button.AutoButtonColor = false
        button.Font = Enum.Font.Code
        button.TextSize = 14
        button.Text = ""
        button.LayoutOrder = i
        button.ZIndex = self.completionFrame.ZIndex + 1
        button.Parent = self.completionFrame
        table.insert(self._completionRows, button)

        local icon = Instance.new("TextLabel")
        icon.Name = "Icon"
        icon.Size = UDim2.new(0, 20, 0, 18)
        icon.Position = UDim2.new(0, 2, 0.5, -9)
        icon.BackgroundColor3 = Color3.fromRGB(66, 66, 66)
        icon.BorderSizePixel = 0
        icon.Font = Enum.Font.SourceSansBold
        icon.TextSize = 12
        icon.TextColor3 = visual.color
        icon.Text = visual.text
        icon.ZIndex = button.ZIndex + 1
        icon.Parent = button

        local labelText = Instance.new("TextLabel")
        labelText.Name = "Label"
        labelText.Size = UDim2.new(1, -112, 1, 0)
        labelText.Position = UDim2.new(0, 26, 0, 0)
        labelText.BackgroundTransparency = 1
        labelText.BorderSizePixel = 0
        labelText.Font = Enum.Font.Code
        labelText.TextSize = 14
        labelText.TextXAlignment = Enum.TextXAlignment.Left
        labelText.TextColor3 = Color3.fromRGB(235, 235, 235)
        labelText.Text = label
        labelText.ZIndex = button.ZIndex + 1
        labelText.Parent = button

        local detailText = Instance.new("TextLabel")
        detailText.Name = "Detail"
        detailText.Size = UDim2.new(0, 86, 1, 0)
        detailText.Position = UDim2.new(1, -88, 0, 0)
        detailText.BackgroundTransparency = 1
        detailText.BorderSizePixel = 0
        detailText.Font = Enum.Font.SourceSans
        detailText.TextSize = 13
        detailText.TextXAlignment = Enum.TextXAlignment.Right
        detailText.TextColor3 = Color3.fromRGB(170, 170, 170)
        detailText.Text = detail
        detailText.ZIndex = button.ZIndex + 1
        detailText.Parent = button

        table.insert(self._completionConnections, button.Activated:Connect(function()
            self:_setActiveCompletionIndex(i)
            self:_applyCompletion(label)
            self:hideCompletions()
            self:focus()
        end))

        table.insert(self._completionConnections, button.MouseEnter:Connect(function()
            self:_setActiveCompletionIndex(i)

            local shouldShow = EditorTextUtils.shouldShowCompletionInfo(
                self._lastMouseMoveAt,
                self._completionShownAt,
                os.clock(),
                COMPLETION_INFO_MOUSE_INTENT_WINDOW
            )

            if shouldShow then
                self:_showCompletionInfo(item, button)
            elseif self.completionInfoFrame then
                self.completionInfoFrame.Visible = false
            end
        end))

        table.insert(self._completionConnections, button.InputChanged:Connect(function(input)
            if input.UserInputType ~= Enum.UserInputType.MouseMovement then
                return
            end

            self:_setActiveCompletionIndex(i)

            local now = os.clock()
            local shouldShow = EditorTextUtils.shouldShowCompletionInfo(
                self._lastMouseMoveAt,
                self._completionShownAt,
                now,
                COMPLETION_INFO_MOUSE_INTENT_WINDOW
            )

            if shouldShow then
                self:_showCompletionInfo(item, button)
            elseif self.completionInfoFrame then
                self.completionInfoFrame.Visible = false
            end
        end))

        table.insert(self._completionConnections, button.MouseLeave:Connect(function()
            if self.completionInfoFrame then
                self.completionInfoFrame.Visible = false
            end
        end))
    end

    self:_setActiveCompletionIndex(1)
end

function Editor:applySettings(settings)
    if type(settings) ~= "table" then
        return
    end

    if type(settings.tabWidth) == "number" then
        self.options.tabWidth = math.clamp(math.floor(settings.tabWidth), 2, 8)
        self.options.tabText = string.rep(" ", self.options.tabWidth)
    end

    if type(settings.autoIndent) == "boolean" then
        self.options.autoIndent = settings.autoIndent
    end

    if type(settings.lineNumbers) == "boolean" then
        self.options.showLineNumbers = settings.lineNumbers
    end

    if type(settings.intellisenseEnabled) == "boolean" then
        self.options.intellisenseEnabled = settings.intellisenseEnabled
    end

    if type(settings.intellisenseAutoTrigger) == "boolean" then
        self.options.intellisenseAutoTrigger = settings.intellisenseAutoTrigger
    end

    if type(settings.intellisenseAutoTriggerOnDot) == "boolean" then
        self.options.intellisenseAutoTriggerOnDot = settings.intellisenseAutoTriggerOnDot
    end

    if type(settings.intellisenseDebounceMs) == "number" then
        self.options.intellisenseDebounceMs = math.clamp(math.floor(settings.intellisenseDebounceMs), 0, 2000)
    end

    if type(settings.fontSize) == "number" then
        self.options.textSize = math.clamp(math.floor(settings.fontSize), 10, 32)
        self.options.lineHeight = measureLineHeight(self.options.textSize, Enum.Font.Code)

        local lineHeightScale = self.options.lineHeight / self.options.textSize

        self.textBox.TextSize = self.options.textSize
        self.textBox.LineHeight = lineHeightScale

        self.overlay.TextSize = self.options.textSize
        self.overlay.LineHeight = lineHeightScale

        self.lineNumbers.TextSize = self.options.textSize
        self.lineNumbers.LineHeight = lineHeightScale

        self._charWidth = measureCharWidth(self.options.textSize, Enum.Font.Code)
    end

    self.options.theme = settings.theme == "Light" and "Light" or "Dark"
    if type(self.options.highlight) ~= "table" then
        self.options.highlight = {}
    end
    self.options.highlight.theme = self.options.theme

    if self.options.theme == "Light" then
        self.root.BackgroundColor3 = Color3.fromRGB(245, 245, 245)
        self.scroller.BackgroundColor3 = Color3.fromRGB(245, 245, 245)
        self.statusLabel.BackgroundColor3 = Color3.fromRGB(230, 230, 230)
        self.statusLabel.TextColor3 = Color3.fromRGB(20, 20, 20)

        self.textBox.TextColor3 = Color3.fromRGB(20, 20, 20)

        self.overlay.TextColor3 = Color3.fromRGB(20, 20, 20)
        self.lineNumbers.TextColor3 = Color3.fromRGB(110, 110, 110)

        if self.completionFrame then
            self.completionFrame.BackgroundColor3 = Color3.fromRGB(240, 240, 240)
        end
        if self.completionInfoFrame then
            self.completionInfoFrame.BackgroundColor3 = Color3.fromRGB(234, 234, 234)
        end
        if self.completionInfoText then
            self.completionInfoText.TextColor3 = Color3.fromRGB(20, 20, 20)
        end
        if self.hoverInfoFrame then
            self.hoverInfoFrame.BackgroundColor3 = Color3.fromRGB(234, 234, 234)
        end
        if self.hoverInfoText then
            self.hoverInfoText.TextColor3 = Color3.fromRGB(20, 20, 20)
        end

        if self.caret then
            self.caret.BackgroundColor3 = Color3.fromRGB(20, 20, 20)
        end
    else
        self.root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
        self.scroller.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
        self.statusLabel.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
        self.statusLabel.TextColor3 = Color3.fromRGB(220, 220, 220)

        self.textBox.TextColor3 = Color3.fromRGB(255, 255, 255)

        self.overlay.TextColor3 = Color3.fromRGB(220, 220, 220)
        self.lineNumbers.TextColor3 = Color3.fromRGB(128, 128, 128)

        if self.completionFrame then
            self.completionFrame.BackgroundColor3 = Color3.fromRGB(45, 45, 45)
        end
        if self.completionInfoFrame then
            self.completionInfoFrame.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
        end
        if self.completionInfoText then
            self.completionInfoText.TextColor3 = Color3.fromRGB(220, 220, 220)
        end
        if self.hoverInfoFrame then
            self.hoverInfoFrame.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
        end
        if self.hoverInfoText then
            self.hoverInfoText.TextColor3 = Color3.fromRGB(220, 220, 220)
        end

        if self.caret then
            self.caret.BackgroundColor3 = Color3.fromRGB(235, 235, 235)
        end
    end

    refresh(self)
end

function Editor:focus()
    self.textBox:CaptureFocus()
end

function Editor:show()
    self.widget.Enabled = true
end

function Editor:hide()
    self:hideHoverInfo()
    self.widget.Enabled = false
end

function Editor:toggle()
    self.widget.Enabled = not self.widget.Enabled
end

function Editor:destroy()
    self:hideCompletions()
    self:hideHoverInfo()
    stopCaretBlink(self)

    for _, connection in ipairs(self._connections) do
        connection:Disconnect()
    end
    self._connections = {}

    if self.caret then
        self.caret:Destroy()
        self.caret = nil
    end

    if self.completionInfoFrame then
        self.completionInfoFrame:Destroy()
        self.completionInfoFrame = nil
    end

    if self.hoverInfoFrame then
        self.hoverInfoFrame:Destroy()
        self.hoverInfoFrame = nil
    end

    if self.widget then
        self.widget:Destroy()
        self.widget = nil
    end
end

return Editor
