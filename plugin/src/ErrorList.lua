-- LUSharp Error List: VS-style diagnostic panel
-- Displays errors, warnings, and messages from the editor

local ErrorList = {}
ErrorList.__index = ErrorList

local ROW_HEIGHT = 22
local FILTER_BAR_HEIGHT = 28
local ICON_SIZE = 14

local SEVERITY_COLORS = {
    error = Color3.fromRGB(244, 71, 71),
    warning = Color3.fromRGB(255, 199, 54),
    info = Color3.fromRGB(100, 180, 255),
}

local SEVERITY_LETTERS = {
    error = "X",
    warning = "!",
    info = "i",
}

local SEVERITY_ORDER = {
    error = 1,
    warning = 2,
    info = 3,
}

function ErrorList.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, ErrorList)
    self.plugin = pluginObject
    self._connections = {}
    self._rowConnections = {}

    self._onDiagnosticSelected = nil
    self._diagnosticsByScript = {}
    self._currentScript = nil

    self._showErrors = true
    self._showWarnings = true
    self._showInfo = true

    local widgetInfo = DockWidgetPluginGuiInfo.new(
        Enum.InitialDockState.Bottom,
        false,
        false,
        options.width or 600,
        options.height or 180,
        300,
        120
    )

    local widget = pluginObject:CreateDockWidgetPluginGui("LUSharpErrorList", widgetInfo)
    widget.Title = "Error List"
    self.widget = widget

    local root = Instance.new("Frame")
    root.Name = "Root"
    root.Size = UDim2.fromScale(1, 1)
    root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    root.BorderSizePixel = 0
    root.Parent = widget
    self.root = root

    -- Filter bar
    local filterBar = Instance.new("Frame")
    filterBar.Name = "FilterBar"
    filterBar.Size = UDim2.new(1, 0, 0, FILTER_BAR_HEIGHT)
    filterBar.Position = UDim2.new(0, 0, 0, 0)
    filterBar.BackgroundColor3 = Color3.fromRGB(37, 37, 37)
    filterBar.BorderSizePixel = 0
    filterBar.Parent = root

    local filterBorder = Instance.new("Frame")
    filterBorder.Name = "Border"
    filterBorder.Size = UDim2.new(1, 0, 0, 1)
    filterBorder.Position = UDim2.new(0, 0, 1, -1)
    filterBorder.BackgroundColor3 = Color3.fromRGB(55, 55, 55)
    filterBorder.BorderSizePixel = 0
    filterBorder.Parent = filterBar

    local function makeFilterButton(parent, text, xOffset, color, defaultOn)
        local btn = Instance.new("TextButton")
        btn.Size = UDim2.new(0, 120, 0, 22)
        btn.Position = UDim2.new(0, xOffset, 0, 3)
        btn.BackgroundColor3 = defaultOn and Color3.fromRGB(50, 50, 50) or Color3.fromRGB(37, 37, 37)
        btn.BorderSizePixel = 1
        btn.BorderColor3 = Color3.fromRGB(60, 60, 60)
        btn.Font = Enum.Font.SourceSans
        btn.TextSize = 13
        btn.TextColor3 = color
        btn.Text = text
        btn.Parent = parent

        local corner = Instance.new("UICorner")
        corner.CornerRadius = UDim.new(0, 3)
        corner.Parent = btn

        return btn
    end

    self._errorFilterBtn = makeFilterButton(filterBar, "Errors (0)", 4, SEVERITY_COLORS.error, true)
    self._warningFilterBtn = makeFilterButton(filterBar, "Warnings (0)", 128, SEVERITY_COLORS.warning, true)
    self._infoFilterBtn = makeFilterButton(filterBar, "Messages (0)", 252, SEVERITY_COLORS.info, true)

    local function updateFilterButtonStyle(btn, active)
        btn.BackgroundColor3 = active and Color3.fromRGB(50, 50, 50) or Color3.fromRGB(37, 37, 37)
    end

    table.insert(self._connections, self._errorFilterBtn.Activated:Connect(function()
        self._showErrors = not self._showErrors
        updateFilterButtonStyle(self._errorFilterBtn, self._showErrors)
        self:refresh()
    end))

    table.insert(self._connections, self._warningFilterBtn.Activated:Connect(function()
        self._showWarnings = not self._showWarnings
        updateFilterButtonStyle(self._warningFilterBtn, self._showWarnings)
        self:refresh()
    end))

    table.insert(self._connections, self._infoFilterBtn.Activated:Connect(function()
        self._showInfo = not self._showInfo
        updateFilterButtonStyle(self._infoFilterBtn, self._showInfo)
        self:refresh()
    end))

    -- Scrolling list
    local scroller = Instance.new("ScrollingFrame")
    scroller.Name = "Scroller"
    scroller.Size = UDim2.new(1, 0, 1, -FILTER_BAR_HEIGHT)
    scroller.Position = UDim2.new(0, 0, 0, FILTER_BAR_HEIGHT)
    scroller.BackgroundTransparency = 1
    scroller.BorderSizePixel = 0
    scroller.ScrollBarThickness = 6
    scroller.CanvasSize = UDim2.new(0, 0, 0, 0)
    scroller.AutomaticCanvasSize = Enum.AutomaticSize.None
    scroller.Parent = root
    self.scroller = scroller

    local content = Instance.new("Frame")
    content.Name = "Content"
    content.Size = UDim2.new(1, 0, 0, 0)
    content.BackgroundTransparency = 1
    content.BorderSizePixel = 0
    content.Parent = scroller
    self.content = content

    local listLayout = Instance.new("UIListLayout")
    listLayout.Padding = UDim.new(0, 0)
    listLayout.FillDirection = Enum.FillDirection.Vertical
    listLayout.SortOrder = Enum.SortOrder.LayoutOrder
    listLayout.Parent = content
    self.listLayout = listLayout

    table.insert(self._connections, listLayout:GetPropertyChangedSignal("AbsoluteContentSize"):Connect(function()
        content.Size = UDim2.new(1, 0, 0, listLayout.AbsoluteContentSize.Y)
        scroller.CanvasSize = UDim2.new(0, 0, 0, listLayout.AbsoluteContentSize.Y + 4)
    end))

    return self
end

function ErrorList:setOnDiagnosticSelected(callback)
    self._onDiagnosticSelected = callback
end

function ErrorList:setDiagnostics(scriptInstance, diagnostics)
    if not scriptInstance then
        return
    end

    self._diagnosticsByScript[scriptInstance] = diagnostics
    self._currentScript = scriptInstance
    self:refresh()
end

function ErrorList:clear()
    self._diagnosticsByScript = {}
    self._currentScript = nil
    self:refresh()
end

function ErrorList:_clearRows()
    for _, conn in self._rowConnections do
        conn:Disconnect()
    end
    self._rowConnections = {}

    for _, child in self.content:GetChildren() do
        if not child:IsA("UIListLayout") then
            child:Destroy()
        end
    end
end

function ErrorList:refresh()
    self:_clearRows()

    -- Collect all diagnostics with script info
    local allDiagnostics = {}
    local errorCount = 0
    local warningCount = 0
    local infoCount = 0

    for scriptInstance, diagnostics in self._diagnosticsByScript do
        if type(diagnostics) ~= "table" then
            continue
        end

        local scriptName = "unknown"
        local ok, name = pcall(function() return scriptInstance.Name end)
        if ok and type(name) == "string" then
            scriptName = name
        end

        for _, diagnostic in diagnostics do
            local severity = string.lower(tostring(diagnostic.severity or ""))
            if severity == "error" then
                errorCount += 1
            elseif severity == "warning" then
                warningCount += 1
            else
                severity = "info"
                infoCount += 1
            end

            table.insert(allDiagnostics, {
                severity = severity,
                message = diagnostic.message or "",
                code = diagnostic.code or "",
                line = diagnostic.line or 1,
                column = diagnostic.column or 1,
                scriptInstance = scriptInstance,
                scriptName = scriptName,
            })
        end
    end

    -- Update filter button text
    self._errorFilterBtn.Text = "Errors (" .. tostring(errorCount) .. ")"
    self._warningFilterBtn.Text = "Warnings (" .. tostring(warningCount) .. ")"
    self._infoFilterBtn.Text = "Messages (" .. tostring(infoCount) .. ")"

    -- Sort: errors first, then warnings, then info; within each group sort by file then line
    table.sort(allDiagnostics, function(a, b)
        local oa = SEVERITY_ORDER[a.severity] or 99
        local ob = SEVERITY_ORDER[b.severity] or 99
        if oa ~= ob then
            return oa < ob
        end
        if a.scriptName ~= b.scriptName then
            return a.scriptName < b.scriptName
        end
        return a.line < b.line
    end)

    -- Render rows
    local layoutOrder = 0
    for _, entry in allDiagnostics do
        local show = false
        if entry.severity == "error" and self._showErrors then show = true end
        if entry.severity == "warning" and self._showWarnings then show = true end
        if entry.severity == "info" and self._showInfo then show = true end

        if not show then
            continue
        end

        layoutOrder += 1

        local row = Instance.new("TextButton")
        row.Name = "DiagRow"
        row.Size = UDim2.new(1, 0, 0, ROW_HEIGHT)
        row.BackgroundColor3 = (layoutOrder % 2 == 0) and Color3.fromRGB(35, 35, 35) or Color3.fromRGB(30, 30, 30)
        row.BorderSizePixel = 0
        row.Text = ""
        row.AutoButtonColor = false
        row.LayoutOrder = layoutOrder
        row.Parent = self.content

        -- Severity icon (colored circle with letter)
        local iconBg = Instance.new("Frame")
        iconBg.Size = UDim2.new(0, ICON_SIZE, 0, ICON_SIZE)
        iconBg.Position = UDim2.new(0, 6, 0.5, -math.floor(ICON_SIZE / 2))
        iconBg.BackgroundColor3 = SEVERITY_COLORS[entry.severity] or Color3.fromRGB(100, 100, 100)
        iconBg.BorderSizePixel = 0
        iconBg.Parent = row

        local iconCorner = Instance.new("UICorner")
        iconCorner.CornerRadius = UDim.new(1, 0)
        iconCorner.Parent = iconBg

        local iconLabel = Instance.new("TextLabel")
        iconLabel.Size = UDim2.fromScale(1, 1)
        iconLabel.BackgroundTransparency = 1
        iconLabel.Font = Enum.Font.SourceSansBold
        iconLabel.TextSize = 10
        iconLabel.TextColor3 = Color3.fromRGB(20, 20, 20)
        iconLabel.Text = SEVERITY_LETTERS[entry.severity] or "?"
        iconLabel.Parent = iconBg

        -- Code label
        local codeText = tostring(entry.code or "")
        local codeWidth = 0
        if codeText ~= "" then
            codeWidth = 80
            local codeLabel = Instance.new("TextLabel")
            codeLabel.Size = UDim2.new(0, codeWidth, 1, 0)
            codeLabel.Position = UDim2.new(0, 24, 0, 0)
            codeLabel.BackgroundTransparency = 1
            codeLabel.Font = Enum.Font.Code
            codeLabel.TextSize = 12
            codeLabel.TextColor3 = Color3.fromRGB(140, 140, 140)
            codeLabel.TextXAlignment = Enum.TextXAlignment.Left
            codeLabel.Text = codeText
            codeLabel.TextTruncate = Enum.TextTruncate.AtEnd
            codeLabel.Parent = row
        end

        -- Message label
        local msgLeft = 24 + codeWidth
        local msgLabel = Instance.new("TextLabel")
        msgLabel.Size = UDim2.new(1, -(msgLeft + 160), 1, 0)
        msgLabel.Position = UDim2.new(0, msgLeft, 0, 0)
        msgLabel.BackgroundTransparency = 1
        msgLabel.Font = Enum.Font.SourceSans
        msgLabel.TextSize = 13
        msgLabel.TextColor3 = Color3.fromRGB(210, 210, 210)
        msgLabel.TextXAlignment = Enum.TextXAlignment.Left
        msgLabel.Text = entry.message
        msgLabel.TextTruncate = Enum.TextTruncate.AtEnd
        msgLabel.Parent = row

        -- Script name + line info (right side)
        local locationLabel = Instance.new("TextLabel")
        locationLabel.Size = UDim2.new(0, 152, 1, 0)
        locationLabel.Position = UDim2.new(1, -156, 0, 0)
        locationLabel.BackgroundTransparency = 1
        locationLabel.Font = Enum.Font.SourceSans
        locationLabel.TextSize = 12
        locationLabel.TextColor3 = Color3.fromRGB(140, 140, 140)
        locationLabel.TextXAlignment = Enum.TextXAlignment.Right
        locationLabel.Text = entry.scriptName .. "  Ln " .. tostring(entry.line) .. ", Col " .. tostring(entry.column)
        locationLabel.TextTruncate = Enum.TextTruncate.AtEnd
        locationLabel.Parent = row

        -- Click handler
        local capturedScript = entry.scriptInstance
        local capturedLine = entry.line
        local capturedColumn = entry.column

        table.insert(self._rowConnections, row.Activated:Connect(function()
            if self._onDiagnosticSelected then
                self._onDiagnosticSelected(capturedScript, capturedLine, capturedColumn)
            end
        end))

        -- Hover highlight
        table.insert(self._rowConnections, row.MouseEnter:Connect(function()
            row.BackgroundColor3 = Color3.fromRGB(50, 50, 50)
        end))

        local originalColor = row.BackgroundColor3
        table.insert(self._rowConnections, row.MouseLeave:Connect(function()
            row.BackgroundColor3 = originalColor
        end))
    end

    -- Update widget title with counts
    local parts = {}
    if errorCount > 0 then
        table.insert(parts, tostring(errorCount) .. " Error" .. (errorCount ~= 1 and "s" or ""))
    end
    if warningCount > 0 then
        table.insert(parts, tostring(warningCount) .. " Warning" .. (warningCount ~= 1 and "s" or ""))
    end
    if #parts > 0 then
        self.widget.Title = "Error List — " .. table.concat(parts, ", ")
    else
        self.widget.Title = "Error List"
    end
end

function ErrorList:show()
    self.widget.Enabled = true
end

function ErrorList:hide()
    self.widget.Enabled = false
end

function ErrorList:toggle()
    self.widget.Enabled = not self.widget.Enabled
end

function ErrorList:destroy()
    self:_clearRows()

    for _, conn in self._connections do
        conn:Disconnect()
    end
    self._connections = {}

    if self.widget then
        self.widget:Destroy()
        self.widget = nil
    end
end

return ErrorList
