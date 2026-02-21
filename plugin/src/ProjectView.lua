local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local CollectionService = game:GetService("CollectionService")
local Selection = game:GetService("Selection")
local StudioService = game:GetService("StudioService")
local UserInputService = game:GetService("UserInputService")

local ScriptManager = requireModule("ScriptManager")

local ProjectView = {}
ProjectView.__index = ProjectView

local TAG = "LUSharp"
local ROW_HEIGHT = 24
local INDENT_WIDTH = 16
local MARKER_WIDTH = 16
local MARKER_SIZE = 12

local function isLuSharpScript(instance)
    return instance
        and instance:IsA("LuaSourceContainer")
        and CollectionService:HasTag(instance, TAG)
end

local function shouldShowInstance(instance)
    if instance == game then
        return true
    end

    if instance:IsA("LuaSourceContainer") then
        return isLuSharpScript(instance)
    end

    return true
end

local function compareInstances(a, b)
    local aScript = a:IsA("LuaSourceContainer")
    local bScript = b:IsA("LuaSourceContainer")

    if aScript ~= bScript then
        return not aScript
    end

    local an = string.lower(a.Name)
    local bn = string.lower(b.Name)
    if an == bn then
        return a.ClassName < b.ClassName
    end

    return an < bn
end

local function buildRowLabelText(instance)
    return instance == game and "game" or instance.Name
end

local function getFallbackMarkerBadge(instance, isScript)
    if instance == game then
        return "G", Color3.fromRGB(94, 136, 235), Color3.fromRGB(48, 84, 156)
    end

    if isScript then
        if instance:IsA("Script") then
            return "S", Color3.fromRGB(142, 206, 255), Color3.fromRGB(72, 136, 185)
        elseif instance:IsA("LocalScript") then
            return "L", Color3.fromRGB(134, 223, 168), Color3.fromRGB(72, 148, 104)
        end

        return "M", Color3.fromRGB(216, 189, 255), Color3.fromRGB(130, 95, 176)
    end

    local className = tostring(instance.ClassName or "")
    local text = string.sub(className, 1, 1)
    if text == "" then
        text = "?"
    end

    return string.upper(text), Color3.fromRGB(120, 120, 120), Color3.fromRGB(78, 78, 78)
end

local function contextLabel(instance)
    if not instance or not instance:IsA("LuaSourceContainer") then
        return nil
    end

    local explicit = instance:GetAttribute("LUSharpContext")
    if type(explicit) == "string" and explicit ~= "" then
        return explicit
    end

    if instance:IsDescendantOf(game:GetService("ServerScriptService")) then
        return "Server"
    end

    local starterPlayer = game:GetService("StarterPlayer")
    if instance:IsDescendantOf(starterPlayer:WaitForChild("StarterPlayerScripts"))
        or instance:IsDescendantOf(starterPlayer:WaitForChild("StarterCharacterScripts"))
        or instance:IsDescendantOf(game:GetService("StarterGui")) then
        return "Client"
    end

    if instance:IsDescendantOf(game:GetService("ReplicatedStorage")) then
        return "Shared"
    end

    return "Other"
end

local function makeButton(parent, text)
    local btn = Instance.new("TextButton")
    btn.Size = UDim2.new(0, 92, 1, 0)
    btn.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
    btn.BorderSizePixel = 0
    btn.Font = Enum.Font.SourceSans
    btn.TextSize = 14
    btn.TextColor3 = Color3.fromRGB(235, 235, 235)
    btn.Text = text
    btn.Parent = parent
    return btn
end

function ProjectView.new(pluginObject, options)
    options = options or {}

    local self = setmetatable({}, ProjectView)
    self.plugin = pluginObject
    self._connections = {}
    self._rowConnections = {}

    self._onScriptSelected = nil
    self._onNodeSelected = nil
    self._onRequestNewScript = nil
    self._onRequestRename = nil
    self._onRequestDelete = nil
    self._onRequestMove = nil
    self._onRequestBuild = nil

    self.selectedInstance = nil
    self.searchText = ""
    self.expanded = setmetatable({}, { __mode = "k" })
    self.expanded[game] = true

    self.statusByScript = setmetatable({}, { __mode = "k" })
    self._searchMatchCache = setmetatable({}, { __mode = "k" })

    self._dragSource = nil
    self._dragTarget = nil
    self._moveSourceScript = nil

    local widgetInfo = DockWidgetPluginGuiInfo.new(
        Enum.InitialDockState.Right,
        true,
        false,
        options.width or 380,
        options.height or 560,
        260,
        240
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

    local topBar = Instance.new("Frame")
    topBar.Name = "TopBar"
    topBar.Size = UDim2.new(1, -8, 0, 28)
    topBar.Position = UDim2.new(0, 4, 0, 4)
    topBar.BackgroundTransparency = 1
    topBar.BorderSizePixel = 0
    topBar.Parent = root

    local search = Instance.new("TextBox")
    search.Name = "Search"
    search.Size = UDim2.new(1, -196, 1, 0)
    search.BackgroundColor3 = Color3.fromRGB(45, 45, 45)
    search.BorderSizePixel = 0
    search.ClearTextOnFocus = false
    search.Font = Enum.Font.Code
    search.TextSize = 14
    search.TextColor3 = Color3.fromRGB(235, 235, 235)
    search.TextXAlignment = Enum.TextXAlignment.Left
    search.PlaceholderText = "Search instances / scripts"
    search.PlaceholderColor3 = Color3.fromRGB(150, 150, 150)
    search.Text = ""
    search.Parent = topBar
    self.searchBox = search

    local newScriptButton = makeButton(topBar, "New C#")
    newScriptButton.Size = UDim2.new(0, 92, 1, 0)
    newScriptButton.Position = UDim2.new(1, -188, 0, 0)

    local refreshButton = makeButton(topBar, "Refresh")
    refreshButton.Size = UDim2.new(0, 92, 1, 0)
    refreshButton.Position = UDim2.new(1, -92, 0, 0)

    local scroller = Instance.new("ScrollingFrame")
    scroller.Name = "Scroller"
    scroller.Size = UDim2.new(1, -8, 1, -40)
    scroller.Position = UDim2.new(0, 4, 0, 36)
    scroller.BackgroundTransparency = 1
    scroller.BorderSizePixel = 0
    scroller.ScrollBarThickness = 8
    scroller.CanvasSize = UDim2.new(0, 0, 0, 0)
    scroller.AutomaticCanvasSize = Enum.AutomaticSize.None
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
    listLayout.Padding = UDim.new(0, 2)
    listLayout.FillDirection = Enum.FillDirection.Vertical
    listLayout.HorizontalAlignment = Enum.HorizontalAlignment.Left
    listLayout.SortOrder = Enum.SortOrder.LayoutOrder
    listLayout.Parent = content
    self.listLayout = listLayout

    table.insert(self._connections, listLayout:GetPropertyChangedSignal("AbsoluteContentSize"):Connect(function()
        content.Size = UDim2.new(1, -4, 0, listLayout.AbsoluteContentSize.Y)
        scroller.CanvasSize = UDim2.new(0, 0, 0, listLayout.AbsoluteContentSize.Y + 8)
    end))

    table.insert(self._connections, search:GetPropertyChangedSignal("Text"):Connect(function()
        self.searchText = string.lower(search.Text or "")
        self:refresh()
    end))

    table.insert(self._connections, refreshButton.Activated:Connect(function()
        self:refresh()
    end))

    table.insert(self._connections, newScriptButton.Activated:Connect(function()
        self:_requestNewScript(self.selectedInstance)
    end))

    self._contextActionIds = {
        newScript = "lusharp_projectview_newScript",
        rename = "lusharp_projectview_rename",
        delete = "lusharp_projectview_delete",
        move = "lusharp_projectview_move",
        build = "lusharp_projectview_build",
        buildSubtree = "lusharp_projectview_buildSubtree",
    }

    local menu = self.plugin:CreatePluginMenu("LUSharpProjectViewContextMenu", "Project Actions")
    self._contextActionByRef = setmetatable({}, { __mode = "k" })

    local function addContextAction(actionId, text)
        local actionRef = menu:AddNewAction(actionId, text)
        if actionRef ~= nil then
            self._contextActionByRef[actionRef] = actionId
        end
    end

    addContextAction(self._contextActionIds.newScript, "New C# Script")
    addContextAction(self._contextActionIds.rename, "Rename")
    addContextAction(self._contextActionIds.delete, "Delete")
    addContextAction(self._contextActionIds.move, "Move To...")
    addContextAction(self._contextActionIds.build, "Build")
    addContextAction(self._contextActionIds.buildSubtree, "Build with Descendants")
    self._contextMenu = menu

    table.insert(self._connections, UserInputService.InputEnded:Connect(function(input)
        if input.UserInputType ~= Enum.UserInputType.MouseButton1 then
            return
        end

        local source = self._dragSource
        local target = self._dragTarget
        self._dragSource = nil
        self._dragTarget = nil

        if not source or not target or not self._onRequestMove then
            return
        end

        local destination = target
        if destination:IsA("LuaSourceContainer") then
            destination = destination.Parent
        end

        if not destination or source == destination or destination:IsDescendantOf(source) then
            return
        end

        self._onRequestMove(source, destination)
    end))

    return self
end

function ProjectView:setOnScriptSelected(callback)
    self._onScriptSelected = callback
end

function ProjectView:setOnNodeSelected(callback)
    self._onNodeSelected = callback
end

function ProjectView:setOnRequestNewScript(callback)
    self._onRequestNewScript = callback
end

function ProjectView:setOnRequestRename(callback)
    self._onRequestRename = callback
end

function ProjectView:setOnRequestDelete(callback)
    self._onRequestDelete = callback
end

function ProjectView:setOnRequestMove(callback)
    self._onRequestMove = callback
end

function ProjectView:setOnRequestBuild(callback)
    self._onRequestBuild = callback
end

function ProjectView:_getClassIconData(instance)
    self._classIconCache = self._classIconCache or {}

    local className = instance == game and "DataModel" or instance.ClassName
    local cached = self._classIconCache[className]
    if cached ~= nil then
        return cached or nil
    end

    local ok, iconData = pcall(function()
        return StudioService:GetClassIcon(className)
    end)

    if ok and type(iconData) == "table" and type(iconData.Image) == "string" and iconData.Image ~= "" then
        self._classIconCache[className] = iconData
        return iconData
    end

    self._classIconCache[className] = false
    return nil
end

function ProjectView:_requestNewScript(target)
    if not self._onRequestNewScript then
        return
    end

    local parent = target or self.selectedInstance or game:GetService("ReplicatedStorage")
    if typeof(parent) ~= "Instance" then
        parent = game:GetService("ReplicatedStorage")
    elseif parent == game then
        parent = game:GetService("ReplicatedStorage")
    elseif parent:IsA("LuaSourceContainer") then
        parent = parent.Parent
    end

    self._onRequestNewScript(parent)
end

function ProjectView:_requestRename(scriptInstance)
    if self._onRequestRename and isLuSharpScript(scriptInstance) then
        self._onRequestRename(scriptInstance)
    end
end

function ProjectView:_requestDelete(scriptInstance)
    if self._onRequestDelete and isLuSharpScript(scriptInstance) then
        self._onRequestDelete(scriptInstance)
    end
end

function ProjectView:_requestMove(scriptInstance)
    if not self._onRequestMove then
        return
    end

    if not isLuSharpScript(scriptInstance) then
        warn("[LUSharp] Select a LUSharp script row first, then pick a destination in Explorer.")
        return
    end

    local destination = Selection:Get()[1]
    if destination and destination:IsA("LuaSourceContainer") then
        destination = destination.Parent
    end

    if typeof(destination) ~= "Instance" then
        warn("[LUSharp] Select a destination Instance in Explorer to move this script.")
        return
    end

    if scriptInstance == destination or destination:IsDescendantOf(scriptInstance) then
        warn("[LUSharp] Invalid move destination.")
        return
    end

    self._onRequestMove(scriptInstance, destination)
end

function ProjectView:_requestBuild(instance, includeSubtree)
    if self._onRequestBuild and instance then
        self._onRequestBuild(instance, includeSubtree and true or false)
    end
end

function ProjectView:_runContextAction(actionId, target)
    if actionId == self._contextActionIds.newScript then
        self:_requestNewScript(target)
    elseif actionId == self._contextActionIds.rename then
        self:_requestRename(target)
    elseif actionId == self._contextActionIds.delete then
        self:_requestDelete(target)
    elseif actionId == self._contextActionIds.move then
        self:_requestMove(target)
    elseif actionId == self._contextActionIds.build then
        self:_requestBuild(target, false)
    elseif actionId == self._contextActionIds.buildSubtree then
        self:_requestBuild(target, true)
    end
end

function ProjectView:_normalizeContextActionId(action)
    if action == nil then
        return nil
    end

    if self._contextActionByRef then
        local mapped = self._contextActionByRef[action]
        if type(mapped) == "string" then
            return mapped
        end
    end

    local candidates = {}

    local function pushCandidate(raw)
        if type(raw) == "string" and raw ~= "" then
            table.insert(candidates, raw)
        end
    end

    if type(action) == "string" then
        pushCandidate(action)
    elseif type(action) == "table" then
        pushCandidate(action.ActionId)
        pushCandidate(action.Id)
        pushCandidate(action.Name)
        pushCandidate(action.Text)
    else
        for _, field in ipairs({ "ActionId", "Id", "Name", "Text" }) do
            local ok, value = pcall(function()
                return action[field]
            end)
            if ok then
                pushCandidate(value)
            end
        end

        local ok, text = pcall(function()
            return tostring(action)
        end)
        if ok then
            pushCandidate(text)
        end
    end

    local ids = self._contextActionIds
    for _, raw in ipairs(candidates) do
        if raw == ids.newScript then
            return ids.newScript
        elseif raw == ids.rename then
            return ids.rename
        elseif raw == ids.delete then
            return ids.delete
        elseif raw == ids.move then
            return ids.move
        elseif raw == ids.build then
            return ids.build
        elseif raw == ids.buildSubtree then
            return ids.buildSubtree
        end

        local lowered = string.lower(raw)
        if string.find(lowered, "new", 1, true) and string.find(lowered, "script", 1, true) then
            return ids.newScript
        elseif string.find(lowered, "rename", 1, true) then
            return ids.rename
        elseif string.find(lowered, "delete", 1, true) then
            return ids.delete
        elseif string.find(lowered, "move", 1, true) then
            return ids.move
        elseif string.find(lowered, "descendant", 1, true) then
            return ids.buildSubtree
        elseif string.find(lowered, "build", 1, true) then
            return ids.build
        end
    end

    return nil
end

function ProjectView:_showContextMenu(target)
    if not self._contextMenu then
        return
    end

    local ok, action = pcall(function()
        return self._contextMenu:ShowAsync()
    end)

    if not ok then
        warn("[LUSharp] Context menu failed: " .. tostring(action))
        return
    end

    if not action then
        return
    end

    local actionId = self:_normalizeContextActionId(action)
    if type(actionId) == "string" then
        self:_runContextAction(actionId, target)
        return
    end

    local actionType = "unknown"
    local okType, resolvedType = pcall(function()
        return typeof(action)
    end)
    if okType and type(resolvedType) == "string" then
        actionType = resolvedType
    end

    warn("[LUSharp] Unrecognized context action result (type=" .. tostring(actionType) .. "): " .. tostring(action))
end

function ProjectView:setScriptBuildStatus(scriptInstance, status)
    if not isLuSharpScript(scriptInstance) then
        return
    end

    self.statusByScript[scriptInstance] = status
    self:refresh()
end

function ProjectView:setScriptDirty(scriptInstance, dirty)
    if not isLuSharpScript(scriptInstance) then
        return
    end

    local status = self.statusByScript[scriptInstance] or {}
    status.dirty = dirty and true or false
    self.statusByScript[scriptInstance] = status
    self:refresh()
end

function ProjectView:_clearEntries()
    for _, connection in ipairs(self._rowConnections) do
        connection:Disconnect()
    end
    self._rowConnections = {}

    for _, child in ipairs(self.content:GetChildren()) do
        if not child:IsA("UIListLayout") then
            child:Destroy()
        end
    end
end

function ProjectView:_getVisibleChildren(instance)
    local children = {}

    for _, child in ipairs(instance:GetChildren()) do
        if shouldShowInstance(child) then
            if self.searchText == "" or self:_subtreeMatchesSearch(child) then
                table.insert(children, child)
            end
        end
    end

    table.sort(children, compareInstances)
    return children
end

function ProjectView:_nameMatchesSearch(instance)
    if self.searchText == "" then
        return true
    end

    return string.find(string.lower(instance.Name), self.searchText, 1, true) ~= nil
end

function ProjectView:_subtreeMatchesSearch(instance)
    local cached = self._searchMatchCache[instance]
    if cached ~= nil then
        return cached
    end

    local matched = self:_nameMatchesSearch(instance)
    if not matched then
        for _, child in ipairs(instance:GetChildren()) do
            if shouldShowInstance(child) and self:_subtreeMatchesSearch(child) then
                matched = true
                break
            end
        end
    end

    self._searchMatchCache[instance] = matched
    return matched
end

function ProjectView:_isExpanded(instance)
    if self.searchText ~= "" then
        return true
    end

    return self.expanded[instance] == true
end

function ProjectView:_statusText(scriptInstance)
    local status = self.statusByScript[scriptInstance]
    if not status then
        return ""
    end

    local badges = {}
    if status.dirty then
        table.insert(badges, "dirty")
    end

    if type(status.errors) == "number" and status.errors > 0 then
        table.insert(badges, "errors:" .. tostring(status.errors))
    elseif status.ok then
        table.insert(badges, "ok")
    end

    return table.concat(badges, " | ")
end

function ProjectView:_appendNode(instance, depth)
    local children = self:_getVisibleChildren(instance)
    local hasChildren = #children > 0
    local expanded = self:_isExpanded(instance)

    local row = Instance.new("Frame")
    row.Name = instance.Name .. "Row"
    row.Size = UDim2.new(1, -8, 0, ROW_HEIGHT)
    row.BackgroundColor3 = (instance == self.selectedInstance) and Color3.fromRGB(58, 58, 58) or Color3.fromRGB(42, 42, 42)
    row.BorderSizePixel = 0
    row.Active = true
    row.Parent = self.content

    local arrow = Instance.new("TextButton")
    arrow.Size = UDim2.new(0, 16, 1, 0)
    arrow.Position = UDim2.new(0, depth * INDENT_WIDTH, 0, 0)
    arrow.BackgroundTransparency = 1
    arrow.BorderSizePixel = 0
    arrow.Font = Enum.Font.SourceSans
    arrow.TextSize = 14
    arrow.TextColor3 = Color3.fromRGB(200, 200, 200)
    arrow.Text = hasChildren and (expanded and "v" or ">") or ""
    arrow.Parent = row

    local isScript = isLuSharpScript(instance)

    local markerWidth = MARKER_WIDTH
    local markerSpacing = 2
    local markerX = depth * INDENT_WIDTH + 18

    local iconData = self:_getClassIconData(instance)

    local marker = Instance.new("Frame")
    marker.Size = UDim2.new(0, MARKER_SIZE, 0, MARKER_SIZE)
    marker.Position = UDim2.new(0, markerX + math.floor((markerWidth - MARKER_SIZE) / 2), 0.5, -math.floor(MARKER_SIZE / 2))
    marker.BackgroundTransparency = 1
    marker.BorderSizePixel = 0
    marker.Active = true
    marker.Parent = row

    if iconData then
        local icon = Instance.new("ImageLabel")
        icon.Size = UDim2.fromScale(1, 1)
        icon.BackgroundTransparency = 1
        icon.BorderSizePixel = 0
        icon.Image = iconData.Image

        if typeof(iconData.ImageRectOffset) == "Vector2" then
            icon.ImageRectOffset = iconData.ImageRectOffset
        end
        if typeof(iconData.ImageRectSize) == "Vector2" then
            icon.ImageRectSize = iconData.ImageRectSize
        end

        icon.Active = true
        icon.Parent = marker
    else
        local markerText, markerFill, markerBorder = getFallbackMarkerBadge(instance, isScript)

        marker.BackgroundTransparency = 0
        marker.BackgroundColor3 = markerFill
        marker.BorderSizePixel = 1
        marker.BorderColor3 = markerBorder

        local markerCorner = Instance.new("UICorner")
        markerCorner.CornerRadius = UDim.new(1, 0)
        markerCorner.Parent = marker

        local markerLabel = Instance.new("TextLabel")
        markerLabel.Size = UDim2.fromScale(1, 1)
        markerLabel.BackgroundTransparency = 1
        markerLabel.BorderSizePixel = 0
        markerLabel.Font = Enum.Font.SourceSansBold
        markerLabel.TextSize = 9
        markerLabel.TextColor3 = Color3.fromRGB(240, 240, 240)
        markerLabel.Text = markerText
        markerLabel.Parent = marker
    end

    local labelLeft = markerX + markerWidth + markerSpacing

    local labelButton = Instance.new("TextButton")
    labelButton.Size = UDim2.new(1, -(labelLeft + 2), 1, 0)
    labelButton.Position = UDim2.new(0, labelLeft, 0, 0)
    labelButton.BackgroundTransparency = 1
    labelButton.BorderSizePixel = 0
    labelButton.TextXAlignment = Enum.TextXAlignment.Left
    labelButton.Font = Enum.Font.SourceSans
    labelButton.TextSize = 15
    labelButton.Text = buildRowLabelText(instance)
    labelButton.TextColor3 = isScript and Color3.fromRGB(142, 206, 255) or Color3.fromRGB(225, 225, 225)
    labelButton.Parent = row

    local statusLabel = Instance.new("TextLabel")
    statusLabel.Size = UDim2.new(0, 140, 1, 0)
    statusLabel.Position = UDim2.new(1, -140, 0, 0)
    statusLabel.BackgroundTransparency = 1
    statusLabel.BorderSizePixel = 0
    statusLabel.Font = Enum.Font.SourceSans
    statusLabel.TextSize = 13
    statusLabel.TextXAlignment = Enum.TextXAlignment.Right
    statusLabel.TextColor3 = Color3.fromRGB(180, 180, 180)
    statusLabel.Text = isScript and self:_statusText(instance) or ""
    statusLabel.Parent = row

    local function openRowContextMenu()
        self.selectedInstance = instance
        if isScript then
            self._moveSourceScript = instance
        end
        self:refresh()
        self:_showContextMenu(instance)
    end

    local function hookRightClick(guiObject)
        if not guiObject then
            return
        end

        if guiObject:IsA("GuiButton") then
            table.insert(self._rowConnections, guiObject.MouseButton2Click:Connect(function()
                openRowContextMenu()
            end))
        end

        table.insert(self._rowConnections, guiObject.InputBegan:Connect(function(input)
            if input.UserInputType == Enum.UserInputType.MouseButton2 then
                openRowContextMenu()
            end
        end))
    end

    table.insert(self._rowConnections, arrow.Activated:Connect(function()
        if hasChildren then
            self.expanded[instance] = not expanded
            self:refresh()
        end
    end))

    table.insert(self._rowConnections, labelButton.Activated:Connect(function()
        self.selectedInstance = instance

        if isScript then
            self._moveSourceScript = instance
        end

        Selection:Set({ instance })

        if isScript and self._onScriptSelected then
            self._onScriptSelected(instance)
        end

        if self._onNodeSelected then
            self._onNodeSelected(instance)
        end

        self:refresh()
    end))

    table.insert(self._rowConnections, labelButton.InputBegan:Connect(function(input)
        if input.UserInputType == Enum.UserInputType.MouseButton1 and isScript then
            self._dragSource = instance
            self._dragTarget = instance
        end
    end))

    hookRightClick(row)
    hookRightClick(labelButton)
    hookRightClick(arrow)
    hookRightClick(statusLabel)
    hookRightClick(marker)

    table.insert(self._rowConnections, labelButton.MouseEnter:Connect(function()
        if self._dragSource then
            self._dragTarget = instance
        end
    end))

    if expanded then
        for _, child in ipairs(children) do
            self:_appendNode(child, depth + 1)
        end
    end
end

function ProjectView:refresh(_scripts)
    self._searchMatchCache = setmetatable({}, { __mode = "k" })

    self:_clearEntries()
    self:_appendNode(game, 0)
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
