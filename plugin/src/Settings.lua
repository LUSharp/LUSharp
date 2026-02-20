local Settings = {}
Settings.__index = Settings

local SETTINGS_KEY = "LUSharp.Settings"

local DEFAULTS = {
    theme = "Dark",
    fontSize = 16,
    tabWidth = 4,
    autoIndent = true,
    lineNumbers = true,
}

local function copyTable(input)
    local out = {}
    for k, v in pairs(input) do
        out[k] = v
    end
    return out
end

local function sanitize(input)
    local settings = copyTable(DEFAULTS)

    if type(input) ~= "table" then
        return settings
    end

    if input.theme == "Dark" or input.theme == "Light" then
        settings.theme = input.theme
    end

    if type(input.fontSize) == "number" then
        settings.fontSize = math.clamp(math.floor(input.fontSize), 10, 32)
    end

    if type(input.tabWidth) == "number" then
        settings.tabWidth = math.clamp(math.floor(input.tabWidth), 2, 8)
    end

    if type(input.autoIndent) == "boolean" then
        settings.autoIndent = input.autoIndent
    end

    if type(input.lineNumbers) == "boolean" then
        settings.lineNumbers = input.lineNumbers
    end

    return settings
end

function Settings.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, Settings)
    self.plugin = pluginObject
    self._connections = {}
    self._onChanged = nil

    local persisted = pluginObject:GetSetting(SETTINGS_KEY)
    self.values = sanitize(persisted)

    local widgetInfo = DockWidgetPluginGuiInfo.new(
        Enum.InitialDockState.Float,
        false,
        false,
        options.width or 320,
        options.height or 280,
        280,
        240
    )

    local widget = pluginObject:CreateDockWidgetPluginGui("LUSharpSettings", widgetInfo)
    widget.Title = "LUSharp Settings"
    self.widget = widget

    local root = Instance.new("Frame")
    root.Name = "Root"
    root.Size = UDim2.fromScale(1, 1)
    root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    root.BorderSizePixel = 0
    root.Parent = widget
    self.root = root

    local list = Instance.new("UIListLayout")
    list.Padding = UDim.new(0, 8)
    list.HorizontalAlignment = Enum.HorizontalAlignment.Left
    list.VerticalAlignment = Enum.VerticalAlignment.Top
    list.SortOrder = Enum.SortOrder.LayoutOrder
    list.Parent = root

    local padding = Instance.new("UIPadding")
    padding.PaddingLeft = UDim.new(0, 12)
    padding.PaddingRight = UDim.new(0, 12)
    padding.PaddingTop = UDim.new(0, 12)
    padding.Parent = root

    local layoutOrder = 0

    local function nextOrder()
        layoutOrder += 1
        return layoutOrder
    end

    local function makeLabel(text)
        local label = Instance.new("TextLabel")
        label.Size = UDim2.new(1, 0, 0, 20)
        label.BackgroundTransparency = 1
        label.TextColor3 = Color3.fromRGB(220, 220, 220)
        label.Font = Enum.Font.SourceSans
        label.TextSize = 16
        label.TextXAlignment = Enum.TextXAlignment.Left
        label.Text = text
        label.LayoutOrder = nextOrder()
        label.Parent = root
        return label
    end

    local function makeTextInput(initialValue)
        local input = Instance.new("TextBox")
        input.Size = UDim2.new(1, 0, 0, 28)
        input.BackgroundColor3 = Color3.fromRGB(45, 45, 45)
        input.TextColor3 = Color3.fromRGB(235, 235, 235)
        input.BorderSizePixel = 0
        input.Font = Enum.Font.Code
        input.TextSize = 15
        input.ClearTextOnFocus = false
        input.TextXAlignment = Enum.TextXAlignment.Left
        input.Text = tostring(initialValue)
        input.LayoutOrder = nextOrder()
        input.Parent = root
        return input
    end

    local function makeToggleButton(label, value)
        local button = Instance.new("TextButton")
        button.Size = UDim2.new(1, 0, 0, 28)
        button.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
        button.TextColor3 = Color3.fromRGB(235, 235, 235)
        button.BorderSizePixel = 0
        button.Font = Enum.Font.SourceSans
        button.TextSize = 15
        button.TextXAlignment = Enum.TextXAlignment.Left
        button.Text = string.format("%s: %s", label, value and "On" or "Off")
        button.LayoutOrder = nextOrder()
        button.Parent = root
        return button
    end

    local function makeChoiceButton(label, value)
        local button = Instance.new("TextButton")
        button.Size = UDim2.new(1, 0, 0, 28)
        button.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
        button.TextColor3 = Color3.fromRGB(235, 235, 235)
        button.BorderSizePixel = 0
        button.Font = Enum.Font.SourceSans
        button.TextSize = 15
        button.TextXAlignment = Enum.TextXAlignment.Left
        button.Text = string.format("%s: %s", label, value)
        button.LayoutOrder = nextOrder()
        button.Parent = root
        return button
    end

    makeLabel("Theme")
    local themeButton = makeChoiceButton("Theme", self.values.theme)

    makeLabel("Font Size")
    local fontSizeInput = makeTextInput(self.values.fontSize)

    makeLabel("Tab Width")
    local tabWidthInput = makeTextInput(self.values.tabWidth)

    local autoIndentButton = makeToggleButton("Auto-indent", self.values.autoIndent)
    local lineNumbersButton = makeToggleButton("Line numbers", self.values.lineNumbers)

    self.controls = {
        themeButton = themeButton,
        fontSizeInput = fontSizeInput,
        tabWidthInput = tabWidthInput,
        autoIndentButton = autoIndentButton,
        lineNumbersButton = lineNumbersButton,
    }

    local function persistAndNotify()
        self.values = sanitize(self.values)
        self.plugin:SetSetting(SETTINGS_KEY, self.values)
        if self._onChanged then
            self._onChanged(copyTable(self.values))
        end
    end

    local function refreshControls()
        themeButton.Text = string.format("Theme: %s", self.values.theme)
        fontSizeInput.Text = tostring(self.values.fontSize)
        tabWidthInput.Text = tostring(self.values.tabWidth)
        autoIndentButton.Text = string.format("Auto-indent: %s", self.values.autoIndent and "On" or "Off")
        lineNumbersButton.Text = string.format("Line numbers: %s", self.values.lineNumbers and "On" or "Off")
    end

    table.insert(self._connections, themeButton.Activated:Connect(function()
        self.values.theme = self.values.theme == "Dark" and "Light" or "Dark"
        persistAndNotify()
        refreshControls()
    end))

    table.insert(self._connections, fontSizeInput.FocusLost:Connect(function(enterPressed)
        if not enterPressed then
            refreshControls()
            return
        end

        local value = tonumber(fontSizeInput.Text)
        if value then
            self.values.fontSize = value
            persistAndNotify()
            refreshControls()
        else
            refreshControls()
        end
    end))

    table.insert(self._connections, tabWidthInput.FocusLost:Connect(function(enterPressed)
        if not enterPressed then
            refreshControls()
            return
        end

        local value = tonumber(tabWidthInput.Text)
        if value then
            self.values.tabWidth = value
            persistAndNotify()
            refreshControls()
        else
            refreshControls()
        end
    end))

    table.insert(self._connections, autoIndentButton.Activated:Connect(function()
        self.values.autoIndent = not self.values.autoIndent
        persistAndNotify()
        refreshControls()
    end))

    table.insert(self._connections, lineNumbersButton.Activated:Connect(function()
        self.values.lineNumbers = not self.values.lineNumbers
        persistAndNotify()
        refreshControls()
    end))

    refreshControls()

    return self
end

function Settings:setOnChanged(callback)
    self._onChanged = callback
end

function Settings:get()
    return copyTable(self.values)
end

function Settings:set(values)
    if type(values) ~= "table" then
        return false
    end

    for key, value in pairs(values) do
        self.values[key] = value
    end

    self.values = sanitize(self.values)
    self.plugin:SetSetting(SETTINGS_KEY, self.values)

    if self._onChanged then
        self._onChanged(copyTable(self.values))
    end

    if self.controls then
        self.controls.themeButton.Text = string.format("Theme: %s", self.values.theme)
        self.controls.fontSizeInput.Text = tostring(self.values.fontSize)
        self.controls.tabWidthInput.Text = tostring(self.values.tabWidth)
        self.controls.autoIndentButton.Text = string.format("Auto-indent: %s", self.values.autoIndent and "On" or "Off")
        self.controls.lineNumbersButton.Text = string.format("Line numbers: %s", self.values.lineNumbers and "On" or "Off")
    end

    return true
end

function Settings:show()
    self.widget.Enabled = true
end

function Settings:hide()
    self.widget.Enabled = false
end

function Settings:toggle()
    self.widget.Enabled = not self.widget.Enabled
end

function Settings:destroy()
    for _, connection in ipairs(self._connections) do
        connection:Disconnect()
    end
    self._connections = {}

    if self.widget then
        self.widget:Destroy()
        self.widget = nil
    end
end

return Settings
