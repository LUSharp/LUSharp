local Settings = {}
Settings.__index = Settings

local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local TypeDatabase = requireModule("TypeDatabase")

local SETTINGS_KEY = "LUSharp.Settings"
local ROBLOX_VALIDITY_PROFILE_CACHE_KEY = "LUSharp.RobloxValidityProfileCache"

local DEFAULT_VISIBLE_SERVICES = {
    "Workspace",
    "Players",
    "Lighting",
    "MaterialService",
    "ReplicatedFirst",
    "ReplicatedStorage",
    "ServerScriptService",
    "ServerStorage",
    "StarterGui",
    "StarterPack",
    "StarterPlayer",
    "SoundService",
    "TextChatService",
    "Chat",
    "Teams",
}

local function getAllServiceNames()
    local out = {}
    local seen = {}

    local function addName(name)
        if type(name) ~= "string" or name == "" then
            return
        end

        local key = string.lower(name)
        if seen[key] then
            return
        end

        seen[key] = true
        table.insert(out, name)
    end

    for _, defaultName in ipairs(DEFAULT_VISIBLE_SERVICES) do
        addName(defaultName)
    end

    for alias, fullName in pairs(TypeDatabase.aliases or {}) do
        if type(alias) == "string" and alias ~= "" and type(fullName) == "string" and fullName:find("%.Services%.") then
            addName(alias)
        end
    end

    table.sort(out, function(a, b)
        return string.lower(a) < string.lower(b)
    end)

    return out
end

local ALL_SERVICE_NAMES = getAllServiceNames()
local ALL_SERVICE_NAME_SET = {}
for _, name in ipairs(ALL_SERVICE_NAMES) do
    ALL_SERVICE_NAME_SET[string.lower(name)] = name
end

local DEFAULTS = {
    theme = "Dark",
    fontSize = 16,
    tabWidth = 4,
    autoIndent = true,
    lineNumbers = true,

    intellisenseEnabled = true,
    intellisenseAutoTrigger = true,
    intellisenseAutoTriggerOnDot = true,
    intellisenseDebounceMs = 250,
    intellisenseVisibleServices = DEFAULT_VISIBLE_SERVICES,
}

local function copyTable(input)
    local out = {}
    for k, v in pairs(input) do
        if type(v) == "table" then
            local nested = {}
            for nk, nv in pairs(v) do
                nested[nk] = nv
            end
            out[k] = nested
        else
            out[k] = v
        end
    end
    return out
end

local function sanitizeVisibleServices(input)
    if type(input) ~= "table" then
        return copyTable(DEFAULT_VISIBLE_SERVICES)
    end

    local out = {}
    local seen = {}

    for _, name in ipairs(input) do
        if type(name) == "string" and name ~= "" then
            local key = string.lower(name)
            local canonical = ALL_SERVICE_NAME_SET[key]
            if canonical and not seen[key] then
                seen[key] = true
                table.insert(out, canonical)
            end
        end
    end

    if #out == 0 then
        return copyTable(DEFAULT_VISIBLE_SERVICES)
    end

    table.sort(out, function(a, b)
        return string.lower(a) < string.lower(b)
    end)

    return out
end

local function sanitize(input)
    local settings = copyTable(DEFAULTS)
    settings.intellisenseVisibleServices = copyTable(DEFAULT_VISIBLE_SERVICES)

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

    if type(input.intellisenseEnabled) == "boolean" then
        settings.intellisenseEnabled = input.intellisenseEnabled
    end

    if type(input.intellisenseAutoTrigger) == "boolean" then
        settings.intellisenseAutoTrigger = input.intellisenseAutoTrigger
    end

    if type(input.intellisenseAutoTriggerOnDot) == "boolean" then
        settings.intellisenseAutoTriggerOnDot = input.intellisenseAutoTriggerOnDot
    end

    if type(input.intellisenseDebounceMs) == "number" then
        settings.intellisenseDebounceMs = math.clamp(math.floor(input.intellisenseDebounceMs), 0, 2000)
    end

    settings.intellisenseVisibleServices = sanitizeVisibleServices(input.intellisenseVisibleServices)

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

    local root = Instance.new("ScrollingFrame")
    root.Name = "Root"
    root.Size = UDim2.fromScale(1, 1)
    root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    root.BorderSizePixel = 0
    root.CanvasSize = UDim2.new(0, 0, 0, 0)
    root.ScrollBarThickness = 10
    root.AutomaticCanvasSize = Enum.AutomaticSize.None
    root.ScrollingDirection = Enum.ScrollingDirection.Y
    root.Parent = widget
    self.root = root

    local content = Instance.new("Frame")
    content.Name = "Content"
    content.Size = UDim2.new(1, -root.ScrollBarThickness, 0, 0)
    content.BackgroundTransparency = 1
    content.BorderSizePixel = 0
    content.AutomaticSize = Enum.AutomaticSize.Y
    content.Parent = root

    local list = Instance.new("UIListLayout")
    list.Padding = UDim.new(0, 8)
    list.HorizontalAlignment = Enum.HorizontalAlignment.Left
    list.VerticalAlignment = Enum.VerticalAlignment.Top
    list.SortOrder = Enum.SortOrder.LayoutOrder
    list.Parent = content

    local padding = Instance.new("UIPadding")
    padding.PaddingLeft = UDim.new(0, 12)
    padding.PaddingRight = UDim.new(0, 12)
    padding.PaddingTop = UDim.new(0, 12)
    padding.Parent = content

    local function updateCanvas()
        root.CanvasSize = UDim2.new(0, 0, 0, content.AbsoluteSize.Y + 12)
    end

    updateCanvas()
    table.insert(self._connections, content:GetPropertyChangedSignal("AbsoluteSize"):Connect(updateCanvas))

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
        label.Parent = content
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
        input.Parent = content
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
        button.Parent = content
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
        button.Parent = content
        return button
    end

    local persistAndNotify
    local refreshControls

    makeLabel("Theme")
    local themeButton = makeChoiceButton("Theme", self.values.theme)

    makeLabel("Font Size")
    local fontSizeInput = makeTextInput(self.values.fontSize)

    makeLabel("Tab Width")
    local tabWidthInput = makeTextInput(self.values.tabWidth)

    makeLabel("IntelliSense")
    local intellisenseEnabledButton = makeToggleButton("Enabled", self.values.intellisenseEnabled)
    local intellisenseAutoTriggerButton = makeToggleButton("Auto-trigger", self.values.intellisenseAutoTrigger)
    local intellisenseDotTriggerButton = makeToggleButton("Trigger on '.'", self.values.intellisenseAutoTriggerOnDot)

    makeLabel("IntelliSense Debounce (ms)")
    local intellisenseDebounceInput = makeTextInput(self.values.intellisenseDebounceMs)

    makeLabel("GetService Visible Services")
    local serviceSearchInput = makeTextInput("")
    serviceSearchInput.PlaceholderText = "Search services"

    local servicesContainer = Instance.new("Frame")
    servicesContainer.Size = UDim2.new(1, 0, 0, 0)
    servicesContainer.BackgroundTransparency = 1
    servicesContainer.BorderSizePixel = 0
    servicesContainer.AutomaticSize = Enum.AutomaticSize.Y
    servicesContainer.LayoutOrder = nextOrder()
    servicesContainer.Parent = content

    local servicesListLayout = Instance.new("UIListLayout")
    servicesListLayout.Padding = UDim.new(0, 4)
    servicesListLayout.HorizontalAlignment = Enum.HorizontalAlignment.Left
    servicesListLayout.VerticalAlignment = Enum.VerticalAlignment.Top
    servicesListLayout.SortOrder = Enum.SortOrder.LayoutOrder
    servicesListLayout.Parent = servicesContainer

    local serviceButtons = {}

    local function getVisibleServiceSet()
        local out = {}
        for _, serviceName in ipairs(self.values.intellisenseVisibleServices or {}) do
            if type(serviceName) == "string" and serviceName ~= "" then
                out[string.lower(serviceName)] = true
            end
        end
        return out
    end

    local function setVisibleServicesFromSet(visibleSet)
        local out = {}
        for _, serviceName in ipairs(ALL_SERVICE_NAMES) do
            if visibleSet[string.lower(serviceName)] then
                table.insert(out, serviceName)
            end
        end

        if #out == 0 then
            out = copyTable(DEFAULT_VISIBLE_SERVICES)
        end

        self.values.intellisenseVisibleServices = out
    end

    local function toggleVisibleService(serviceName)
        local visibleSet = getVisibleServiceSet()
        local key = string.lower(serviceName)
        if visibleSet[key] then
            visibleSet[key] = nil
        else
            visibleSet[key] = true
        end

        setVisibleServicesFromSet(visibleSet)
    end

    local function refreshServiceButtons()
        local query = string.lower(serviceSearchInput.Text or "")
        local visibleSet = getVisibleServiceSet()

        for _, serviceName in ipairs(ALL_SERVICE_NAMES) do
            local button = serviceButtons[serviceName]
            if button then
                local matches = query == "" or string.find(string.lower(serviceName), query, 1, true) ~= nil
                button.Visible = matches
                local checked = visibleSet[string.lower(serviceName)] == true
                button.Text = string.format("[%s] %s", checked and "x" or " ", serviceName)
            end
        end
    end

    for index, serviceName in ipairs(ALL_SERVICE_NAMES) do
        local button = Instance.new("TextButton")
        button.Size = UDim2.new(1, 0, 0, 24)
        button.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
        button.TextColor3 = Color3.fromRGB(235, 235, 235)
        button.BorderSizePixel = 0
        button.Font = Enum.Font.SourceSans
        button.TextSize = 15
        button.TextXAlignment = Enum.TextXAlignment.Left
        button.LayoutOrder = index
        button.Parent = servicesContainer
        serviceButtons[serviceName] = button

        table.insert(self._connections, button.Activated:Connect(function()
            toggleVisibleService(serviceName)
            persistAndNotify()
            refreshControls()
        end))
    end

    local autoIndentButton = makeToggleButton("Auto-indent", self.values.autoIndent)
    local lineNumbersButton = makeToggleButton("Line numbers", self.values.lineNumbers)

    self.controls = {
        themeButton = themeButton,
        fontSizeInput = fontSizeInput,
        tabWidthInput = tabWidthInput,
        intellisenseEnabledButton = intellisenseEnabledButton,
        intellisenseAutoTriggerButton = intellisenseAutoTriggerButton,
        intellisenseDotTriggerButton = intellisenseDotTriggerButton,
        intellisenseDebounceInput = intellisenseDebounceInput,
        serviceSearchInput = serviceSearchInput,
        refreshServiceButtons = refreshServiceButtons,
        autoIndentButton = autoIndentButton,
        lineNumbersButton = lineNumbersButton,
    }

    persistAndNotify = function()
        self.values = sanitize(self.values)
        self.plugin:SetSetting(SETTINGS_KEY, self.values)
        if self._onChanged then
            self._onChanged(copyTable(self.values))
        end
    end

    refreshControls = function()
        themeButton.Text = string.format("Theme: %s", self.values.theme)
        fontSizeInput.Text = tostring(self.values.fontSize)
        tabWidthInput.Text = tostring(self.values.tabWidth)

        intellisenseEnabledButton.Text = string.format("Enabled: %s", self.values.intellisenseEnabled and "On" or "Off")
        intellisenseAutoTriggerButton.Text = string.format("Auto-trigger: %s", self.values.intellisenseAutoTrigger and "On" or "Off")
        intellisenseDotTriggerButton.Text = string.format("Trigger on '.': %s", self.values.intellisenseAutoTriggerOnDot and "On" or "Off")
        intellisenseDebounceInput.Text = tostring(self.values.intellisenseDebounceMs)

        if self.controls and self.controls.refreshServiceButtons then
            self.controls.refreshServiceButtons()
        end

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

    table.insert(self._connections, intellisenseEnabledButton.Activated:Connect(function()
        self.values.intellisenseEnabled = not self.values.intellisenseEnabled
        persistAndNotify()
        refreshControls()
    end))

    table.insert(self._connections, intellisenseAutoTriggerButton.Activated:Connect(function()
        self.values.intellisenseAutoTrigger = not self.values.intellisenseAutoTrigger
        persistAndNotify()
        refreshControls()
    end))

    table.insert(self._connections, intellisenseDotTriggerButton.Activated:Connect(function()
        self.values.intellisenseAutoTriggerOnDot = not self.values.intellisenseAutoTriggerOnDot
        persistAndNotify()
        refreshControls()
    end))

    table.insert(self._connections, intellisenseDebounceInput.FocusLost:Connect(function(enterPressed)
        if not enterPressed then
            refreshControls()
            return
        end

        local value = tonumber(intellisenseDebounceInput.Text)
        if value then
            self.values.intellisenseDebounceMs = value
            persistAndNotify()
            refreshControls()
        else
            refreshControls()
        end
    end))

    table.insert(self._connections, serviceSearchInput:GetPropertyChangedSignal("Text"):Connect(function()
        refreshServiceButtons()
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

function Settings:getCachedRobloxValidityProfile()
    return self.plugin:GetSetting(ROBLOX_VALIDITY_PROFILE_CACHE_KEY)
end

function Settings:setCachedRobloxValidityProfile(profile)
    self.plugin:SetSetting(ROBLOX_VALIDITY_PROFILE_CACHE_KEY, profile)
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

        self.controls.intellisenseEnabledButton.Text = string.format("Enabled: %s", self.values.intellisenseEnabled and "On" or "Off")
        self.controls.intellisenseAutoTriggerButton.Text = string.format("Auto-trigger: %s", self.values.intellisenseAutoTrigger and "On" or "Off")
        self.controls.intellisenseDotTriggerButton.Text = string.format("Trigger on '.': %s", self.values.intellisenseAutoTriggerOnDot and "On" or "Off")
        self.controls.intellisenseDebounceInput.Text = tostring(self.values.intellisenseDebounceMs)

        if self.controls.refreshServiceButtons then
            self.controls.refreshServiceButtons()
        end

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
