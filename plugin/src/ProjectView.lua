local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local ScriptManager = requireModule("ScriptManager")

local ProjectView = {}
ProjectView.__index = ProjectView

local GROUP_ORDER = { "Server", "Client", "Shared", "Other" }

local function getContextLabel(moduleScript)
    local attributeContext = moduleScript:GetAttribute("LUSharpContext")
    if type(attributeContext) == "string" and attributeContext ~= "" then
        return attributeContext
    end

    local fullName = moduleScript:GetFullName()
    if fullName:find("ServerScriptService", 1, true) then
        return "Server"
    end
    if fullName:find("StarterPlayerScripts", 1, true)
        or fullName:find("StarterGui", 1, true)
        or fullName:find("StarterCharacterScripts", 1, true) then
        return "Client"
    end
    if fullName:find("ReplicatedStorage", 1, true) then
        return "Shared"
    end

    return "Other"
end

local function sortScripts(scripts)
    table.sort(scripts, function(a, b)
        return a:GetFullName() < b:GetFullName()
    end)
end

function ProjectView.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, ProjectView)
    self.plugin = pluginObject
    self._connections = {}
    self._entryButtons = {}
    self._onScriptSelected = nil

    local widgetInfo = DockWidgetPluginGuiInfo.new(
        Enum.InitialDockState.Right,
        true,
        false,
        options.width or 360,
        options.height or 520,
        240,
        220
    )

    local widget = pluginObject:CreateDockWidgetPluginGui("LUSharpProjectView", widgetInfo)
    widget.Title = "LUSharp Project"
    self.widget = widget

    local root = Instance.new("Frame")
    root.Name = "Root"
    root.Size = UDim2.fromScale(1, 1)
    root.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
    root.BorderSizePixel = 0
    root.Parent = widget
    self.root = root

    local title = Instance.new("TextLabel")
    title.Name = "Title"
    title.Size = UDim2.new(1, -12, 0, 28)
    title.Position = UDim2.new(0, 6, 0, 4)
    title.BackgroundTransparency = 1
    title.Font = Enum.Font.SourceSansBold
    title.TextSize = 18
    title.TextXAlignment = Enum.TextXAlignment.Left
    title.TextColor3 = Color3.fromRGB(235, 235, 235)
    title.Text = "C# Scripts"
    title.Parent = root

    local scroller = Instance.new("ScrollingFrame")
    scroller.Name = "Scroller"
    scroller.Size = UDim2.new(1, -8, 1, -42)
    scroller.Position = UDim2.new(0, 4, 0, 34)
    scroller.BackgroundTransparency = 1
    scroller.BorderSizePixel = 0
    scroller.ScrollBarThickness = 8
    scroller.CanvasSize = UDim2.new(0, 0, 0, 0)
    scroller.AutomaticCanvasSize = Enum.AutomaticSize.Y
    scroller.Parent = root
    self.scroller = scroller

    local content = Instance.new("Frame")
    content.Name = "Content"
    content.Size = UDim2.new(1, -4, 0, 0)
    content.BackgroundTransparency = 1
    content.BorderSizePixel = 0
    content.Parent = scroller
    self.content = content

    local listLayout = Instance.new("UIListLayout")
    listLayout.Padding = UDim.new(0, 4)
    listLayout.FillDirection = Enum.FillDirection.Vertical
    listLayout.HorizontalAlignment = Enum.HorizontalAlignment.Left
    listLayout.SortOrder = Enum.SortOrder.LayoutOrder
    listLayout.Parent = content
    self.listLayout = listLayout

    table.insert(self._connections, listLayout:GetPropertyChangedSignal("AbsoluteContentSize"):Connect(function()
        content.Size = UDim2.new(1, -4, 0, listLayout.AbsoluteContentSize.Y)
        scroller.CanvasSize = UDim2.new(0, 0, 0, listLayout.AbsoluteContentSize.Y + 8)
    end))

    return self
end

function ProjectView:setOnScriptSelected(callback)
    self._onScriptSelected = callback
end

function ProjectView:_clearEntries()
    for _, connection in ipairs(self._entryButtons) do
        connection:Disconnect()
    end
    self._entryButtons = {}

    for _, child in ipairs(self.content:GetChildren()) do
        if not child:IsA("UIListLayout") then
            child:Destroy()
        end
    end
end

function ProjectView:_appendGroupHeader(name)
    local header = Instance.new("TextLabel")
    header.Name = name .. "Header"
    header.Size = UDim2.new(1, -8, 0, 22)
    header.BackgroundColor3 = Color3.fromRGB(40, 40, 40)
    header.BorderSizePixel = 0
    header.Font = Enum.Font.SourceSansBold
    header.TextSize = 15
    header.TextColor3 = Color3.fromRGB(200, 200, 200)
    header.TextXAlignment = Enum.TextXAlignment.Left
    header.Text = "  " .. name
    header.Parent = self.content
end

function ProjectView:_appendScriptButton(moduleScript)
    local button = Instance.new("TextButton")
    button.Name = moduleScript.Name .. "Button"
    button.Size = UDim2.new(1, -8, 0, 26)
    button.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
    button.BorderSizePixel = 0
    button.AutoButtonColor = true
    button.Font = Enum.Font.Code
    button.TextSize = 14
    button.TextColor3 = Color3.fromRGB(230, 230, 230)
    button.TextXAlignment = Enum.TextXAlignment.Left
    button.Text = string.format("  %s", moduleScript.Name)
    button.Parent = self.content

    local tooltip = string.format("%s\n%s", moduleScript:GetFullName(), getContextLabel(moduleScript))
    button:SetAttribute("Tooltip", tooltip)

    local connection = button.Activated:Connect(function()
        if self._onScriptSelected then
            self._onScriptSelected(moduleScript)
        end
    end)
    table.insert(self._entryButtons, connection)
end

function ProjectView:refresh(scripts)
    scripts = scripts or ScriptManager.getAll()
    sortScripts(scripts)

    local grouped = {
        Server = {},
        Client = {},
        Shared = {},
        Other = {},
    }

    for _, moduleScript in ipairs(scripts) do
        local group = getContextLabel(moduleScript)
        if grouped[group] == nil then
            group = "Other"
        end
        table.insert(grouped[group], moduleScript)
    end

    self:_clearEntries()

    for _, groupName in ipairs(GROUP_ORDER) do
        local groupScripts = grouped[groupName]
        if #groupScripts > 0 then
            self:_appendGroupHeader(groupName)
            for _, moduleScript in ipairs(groupScripts) do
                self:_appendScriptButton(moduleScript)
            end
        end
    end
end

function ProjectView:show()
    self.widget.Enabled = true
end

function ProjectView:hide()
    self.widget.Enabled = false
end

function ProjectView:toggle()
    self.widget.Enabled = not self.widget.Enabled
end

function ProjectView:destroy()
    self:_clearEntries()

    for _, connection in ipairs(self._connections) do
        connection:Disconnect()
    end
    self._connections = {}

    if self.widget then
        self.widget:Destroy()
        self.widget = nil
    end
end

return ProjectView
