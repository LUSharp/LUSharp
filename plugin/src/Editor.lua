local UserInputService = game:GetService("UserInputService")
local TextService = game:GetService("TextService")

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
    self.statusLabel.Text = string.format("%s | Ln %d, Col %d", self.fileName, line, col)
end

local function measureCharWidth(textSize, font)
    local size = TextService:GetTextSize("M", textSize, font, Vector2.new(1000, 1000))
    return size.X
end

local function caretShouldShow(textBox)
    if not textBox or not textBox:IsFocused() then
        return false
    end

    local sel = textBox.SelectionStart
    return sel == -1 or sel == textBox.CursorPosition
end

local function updateCaretPosition(self)
    if not self.caret or not self.textBox then
        return
    end

    local text = self.textBox.Text
    local cursorPos = self.textBox.CursorPosition
    if cursorPos == nil or cursorPos < 1 then
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

    local measured = TextService:GetTextSize(linePrefix, self.options.textSize, Enum.Font.Code, Vector2.new(100000, 100000))
    local x = measured.X
    local y = (line - 1) * self.options.lineHeight

    self.caret.Position = UDim2.new(0, x, 0, y)
    self.caret.Size = UDim2.new(0, 1, 0, self.options.lineHeight)
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


local function refresh(self)
    local source = self.textBox.Text
    self.overlay.Text = SyntaxHighlighter.highlight(source, self.options.highlight)

    local gutterWidth = self.options.showLineNumbers and self.options.gutterWidth or 0
    if self.completionFrame then
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
    updateCaretPosition(self)
end

function Editor.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, Editor)
    self.plugin = pluginObject

    local tabWidth = options.tabWidth or 4

    self.options = {
        gutterWidth = options.gutterWidth or 52,
        lineHeight = options.lineHeight or 18,
        textSize = options.textSize or 16,
        tabWidth = tabWidth,
        tabText = string.rep(" ", tabWidth),
        autoIndent = options.autoIndent ~= false,
        showLineNumbers = options.showLineNumbers ~= false,
        theme = options.theme or "Dark",
        highlight = options.highlight,
    }
    self.fileName = options.fileName or "<untitled.cs>"
    self.onSourceChanged = nil
    self.onRequestCompletions = nil
    self._connections = {}
    self._completionConnections = {}

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
    overlay.TextColor3 = Color3.fromRGB(220, 220, 220)
    overlay.RichText = true
    overlay.Text = ""
    overlay.ZIndex = 1
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
    textBox.TextColor3 = Color3.fromRGB(255, 255, 255)
    textBox.TextTransparency = 1
    textBox.CursorPosition = 1
    textBox.SelectionStart = -1
    textBox.Text = ""
    textBox.ZIndex = 2
    textBox.Parent = codeContainer
    self.textBox = textBox

    self._caretBlinkToken = 0
    self._charWidth = measureCharWidth(self.options.textSize, Enum.Font.Code)
    self._suppressTextChange = false
    self._lastText = textBox.Text
    self._lastCursorPos = textBox.CursorPosition
    self._pendingAutoIndent = nil

    local caret = Instance.new("Frame")
    caret.Name = "Caret"
    caret.BackgroundColor3 = Color3.fromRGB(235, 235, 235)
    caret.BorderSizePixel = 0
    caret.ZIndex = 3
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

    table.insert(self._connections, textBox:GetPropertyChangedSignal("Text"):Connect(function()
        if self._suppressTextChange then
            self._suppressTextChange = false
            self._pendingAutoIndent = nil
            self._lastText = textBox.Text
            self._lastCursorPos = textBox.CursorPosition

            refresh(self)
            if self.onSourceChanged then
                self.onSourceChanged(textBox.Text)
            end
            self:hideCompletions()
            return
        end

        local prevText = self._lastText or ""
        local prevCursor = self._lastCursorPos

        local newText, newCursor, changed = EditorTextUtils.autoDedentClosingBrace(textBox.Text, textBox.CursorPosition, self.options.tabText)
        if changed then
            self._suppressTextChange = true
            textBox.Text = newText
            textBox.CursorPosition = newCursor
            return
        end

        local pendingIndent = false
        if self.options.autoIndent and prevText ~= textBox.Text then
            local indent = EditorTextUtils.computeAutoIndentInsertion(prevText, prevCursor, textBox.Text, textBox.CursorPosition, self.options.tabText)
            if indent ~= "" then
                self._pendingAutoIndent = nil
                self._suppressTextChange = true
                insertAtCursor(textBox, indent)
                return
            end

            local startPos, _removedText, insertedText = findSingleSplice(prevText, textBox.Text)
            if insertedText == "\n" then
                self._pendingAutoIndent = { newText = textBox.Text }
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
        self:hideCompletions()
    end))

    table.insert(self._connections, textBox:GetPropertyChangedSignal("CursorPosition"):Connect(function()
        if self._pendingAutoIndent and self.options.autoIndent then
            if textBox.Text == self._pendingAutoIndent.newText then
                local indent = EditorTextUtils.computeAutoIndentInsertion(self._lastText, self._lastCursorPos, textBox.Text, textBox.CursorPosition, self.options.tabText)
                if indent ~= "" then
                    self._pendingAutoIndent = nil
                    self._suppressTextChange = true
                    insertAtCursor(textBox, indent)
                    return
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
        stopCaretBlink(self)
    end))

    table.insert(self._connections, UserInputService.InputBegan:Connect(function(input, _gameProcessed)
        if not textBox:IsFocused() then
            return
        end

        if input.KeyCode == Enum.KeyCode.Tab then
            insertAtCursor(textBox, self.options.tabText)
            updateCaretPosition(self)
            textBox:CaptureFocus()
        elseif input.KeyCode == Enum.KeyCode.Space
            and (UserInputService:IsKeyDown(Enum.KeyCode.LeftControl) or UserInputService:IsKeyDown(Enum.KeyCode.RightControl)) then
            if self.onRequestCompletions then
                self.onRequestCompletions()
            end
        end
    end))

    table.insert(self._connections, widget:GetPropertyChangedSignal("AbsoluteSize"):Connect(function()
        refresh(self)
    end))

    refresh(self)

    return self
end

function Editor:setSource(source)
    source = source or ""
    local changed = self.textBox.Text ~= source

    self.textBox.Text = source
    self.textBox.CursorPosition = #self.textBox.Text + 1

    if not changed then
        refresh(self)
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

function Editor:hideCompletions()
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

function Editor:_applyCompletion(label)
    local text = self.textBox.Text
    local cursor = self.textBox.CursorPosition

    if cursor < 1 then
        cursor = #text + 1
    end

    local before = text:sub(1, cursor - 1)
    local prefix = before:match("([%a_][%w_]*)$") or ""

    local startPos = cursor - #prefix
    replaceTextRange(self.textBox, startPos, cursor, label)
    updateCaretPosition(self)
end

function Editor:showCompletions(items)
    self:hideCompletions()

    if type(items) ~= "table" or #items == 0 then
        return
    end

    local maxItems = math.min(#items, 8)
    self.completionFrame.Visible = true
    self.completionFrame.Size = UDim2.new(0, 240, 0, (maxItems * 22) + (maxItems - 1) * 2 + 4)

    for i = 1, maxItems do
        local item = items[i]
        local label = item and item.label or nil
        if type(label) ~= "string" or label == "" then
            label = tostring(item)
        end

        local button = Instance.new("TextButton")
        button.Name = "Completion" .. tostring(i)
        button.Size = UDim2.new(1, 0, 0, 22)
        button.BackgroundColor3 = Color3.fromRGB(55, 55, 55)
        button.BorderSizePixel = 0
        button.AutoButtonColor = true
        button.Font = Enum.Font.Code
        button.TextSize = 14
        button.TextXAlignment = Enum.TextXAlignment.Left
        button.TextColor3 = Color3.fromRGB(235, 235, 235)
        button.Text = "  " .. label
        button.LayoutOrder = i
        button.ZIndex = self.completionFrame.ZIndex + 1
        button.Parent = self.completionFrame

        table.insert(self._completionConnections, button.Activated:Connect(function()
            self:_applyCompletion(label)
            self:hideCompletions()
            self:focus()
        end))
    end
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

    if type(settings.fontSize) == "number" then
        self.options.textSize = math.clamp(math.floor(settings.fontSize), 10, 32)
        self.options.lineHeight = self.options.textSize + 2

        self.textBox.TextSize = self.options.textSize
        self.overlay.TextSize = self.options.textSize
        self.lineNumbers.TextSize = self.options.textSize

        self._charWidth = measureCharWidth(self.options.textSize, Enum.Font.Code)
    end

    if settings.theme == "Light" then
        self.root.BackgroundColor3 = Color3.fromRGB(245, 245, 245)
        self.scroller.BackgroundColor3 = Color3.fromRGB(245, 245, 245)
        self.statusLabel.BackgroundColor3 = Color3.fromRGB(230, 230, 230)
        self.statusLabel.TextColor3 = Color3.fromRGB(20, 20, 20)
        if self.caret then
            self.caret.BackgroundColor3 = Color3.fromRGB(20, 20, 20)
        end
    else
        self.root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
        self.scroller.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
        self.statusLabel.BackgroundColor3 = Color3.fromRGB(37, 37, 38)
        self.statusLabel.TextColor3 = Color3.fromRGB(220, 220, 220)
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
    self.widget.Enabled = false
end

function Editor:toggle()
    self.widget.Enabled = not self.widget.Enabled
end

function Editor:destroy()
    self:hideCompletions()
    stopCaretBlink(self)

    for _, connection in ipairs(self._connections) do
        connection:Disconnect()
    end
    self._connections = {}

    if self.caret then
        self.caret:Destroy()
        self.caret = nil
    end

    if self.widget then
        self.widget:Destroy()
        self.widget = nil
    end
end

return Editor
