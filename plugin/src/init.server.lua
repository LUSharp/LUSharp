-- LUSharp Studio Plugin
-- Entry point

local Selection = game:GetService("Selection")

local function requireModule(name)
    local moduleScript = script:FindFirstChild(name)
    if not moduleScript and script.Parent then
        moduleScript = script.Parent:FindFirstChild(name)
    end

    if not moduleScript then
        error("[LUSharp] Missing module: " .. name, 2)
    end

    return require(moduleScript)
end

local Editor = requireModule("Editor")
local ProjectView = requireModule("ProjectView")
local ScriptManager = requireModule("ScriptManager")
local Settings = requireModule("Settings")
local IntelliSense = requireModule("IntelliSense")

local Lexer = requireModule("Lexer")
local Parser = requireModule("Parser")
local Lowerer = requireModule("Lowerer")
local Emitter = requireModule("Emitter")

local toolbar = plugin:CreateToolbar("LUSharp")

local buildButton = toolbar:CreateButton(
    "Build",
    "Compile selected/active C# script",
    "rbxassetid://0",
    "Build"
)

local buildAllButton = toolbar:CreateButton(
    "BuildAll",
    "Compile all tagged C# scripts",
    "rbxassetid://0",
    "Build All"
)

local newScriptButton = toolbar:CreateButton(
    "NewScript",
    "Create a new LUSharp C# script",
    "rbxassetid://0",
    "New C# Script"
)

local editorButton = toolbar:CreateButton(
    "Editor",
    "Toggle LUSharp editor",
    "rbxassetid://0",
    "Editor"
)

local projectButton = toolbar:CreateButton(
    "Project",
    "Toggle LUSharp project view",
    "rbxassetid://0",
    "Project"
)

local settingsButton = toolbar:CreateButton(
    "Settings",
    "Toggle LUSharp settings",
    "rbxassetid://0",
    "Settings"
)

local promptInfo = DockWidgetPluginGuiInfo.new(
    Enum.InitialDockState.Float,
    false,
    true,
    320,
    140,
    260,
    120
)

local promptWidget = plugin:CreateDockWidgetPluginGui("LUSharpNewScriptPrompt", promptInfo)
promptWidget.Title = "New C# Script"
promptWidget.Enabled = false

local promptRoot = Instance.new("Frame")
promptRoot.Size = UDim2.fromScale(1, 1)
promptRoot.BackgroundColor3 = Color3.fromRGB(30, 30, 30)
promptRoot.BorderSizePixel = 0
promptRoot.Parent = promptWidget

local promptLabel = Instance.new("TextLabel")
promptLabel.Size = UDim2.new(1, -12, 0, 22)
promptLabel.Position = UDim2.new(0, 6, 0, 6)
promptLabel.BackgroundTransparency = 1
promptLabel.Font = Enum.Font.SourceSans
promptLabel.TextSize = 16
promptLabel.TextColor3 = Color3.fromRGB(230, 230, 230)
promptLabel.TextXAlignment = Enum.TextXAlignment.Left
promptLabel.Text = "Script name"
promptLabel.Parent = promptRoot

local promptInput = Instance.new("TextBox")
promptInput.Size = UDim2.new(1, -12, 0, 28)
promptInput.Position = UDim2.new(0, 6, 0, 32)
promptInput.BackgroundColor3 = Color3.fromRGB(45, 45, 45)
promptInput.BorderSizePixel = 0
promptInput.ClearTextOnFocus = false
promptInput.Font = Enum.Font.Code
promptInput.TextSize = 15
promptInput.TextColor3 = Color3.fromRGB(235, 235, 235)
promptInput.TextXAlignment = Enum.TextXAlignment.Left
promptInput.Text = ""
promptInput.Parent = promptRoot

local promptOk = Instance.new("TextButton")
promptOk.Size = UDim2.new(0, 120, 0, 28)
promptOk.Position = UDim2.new(0, 6, 0, 70)
promptOk.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
promptOk.BorderSizePixel = 0
promptOk.Font = Enum.Font.SourceSansBold
promptOk.TextSize = 15
promptOk.TextColor3 = Color3.fromRGB(235, 235, 235)
promptOk.Text = "Create"
promptOk.Parent = promptRoot

local promptCancel = Instance.new("TextButton")
promptCancel.Size = UDim2.new(0, 120, 0, 28)
promptCancel.Position = UDim2.new(0, 132, 0, 70)
promptCancel.BackgroundColor3 = Color3.fromRGB(52, 52, 52)
promptCancel.BorderSizePixel = 0
promptCancel.Font = Enum.Font.SourceSans
promptCancel.TextSize = 15
promptCancel.TextColor3 = Color3.fromRGB(235, 235, 235)
promptCancel.Text = "Cancel"
promptCancel.Parent = promptRoot

local pendingPromptCallback = nil

local function trim(text)
    return (text:gsub("^%s+", ""):gsub("%s+$", ""))
end

local function showNewScriptPrompt(defaultName, onConfirm)
    pendingPromptCallback = onConfirm
    promptInput.Text = defaultName or "NewScript"
    promptWidget.Enabled = true
    promptInput:CaptureFocus()
end

local function closePrompt()
    promptWidget.Enabled = false
    pendingPromptCallback = nil
end

promptOk.Activated:Connect(function()
    local cb = pendingPromptCallback
    local value = trim(promptInput.Text)
    closePrompt()
    if cb then
        cb(value)
    end
end)

promptCancel.Activated:Connect(function()
    closePrompt()
end)

local settings = Settings.new(plugin)
local editor = Editor.new(plugin, {
    fileName = "<untitled.cs>",
})
local projectView = ProjectView.new(plugin)

editor:applySettings(settings:get())
settings:setOnChanged(function(values)
    editor:applySettings(values)
end)

local currentScript = nil

local function inferContextFromParent(parent)
    local fullName = parent:GetFullName()
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

local function openScript(moduleScript)
    currentScript = moduleScript

    editor:setFilename(moduleScript.Name .. ".cs")

    local source = ScriptManager.getSource(moduleScript)
    if source == nil then
        ScriptManager.setSource(moduleScript, "")
        source = ScriptManager.getSource(moduleScript) or ""
    end

    editor:setSource(source)
    editor:show()
    editor:focus()
end

editor:setOnSourceChanged(function(source)
    if currentScript then
        ScriptManager.setSource(currentScript, source)
    end
end)

editor:setOnRequestCompletions(function()
    local cursorPos = editor.textBox.CursorPosition
    local completions = IntelliSense.getCompletions(editor:getSource(), cursorPos)
    editor:showCompletions(completions)
end)

projectView:setOnScriptSelected(function(moduleScript)
    Selection:Set({ moduleScript })
    openScript(moduleScript)
end)

local function compileOne(moduleScript)
    local source = ScriptManager.getSource(moduleScript)
    if source == nil then
        return false
    end

    local tokens = Lexer.tokenize(source)
    local parseResult = Parser.parse(tokens)

    if parseResult.diagnostics and #parseResult.diagnostics > 0 then
        warn("[LUSharp] Parse diagnostics in " .. moduleScript:GetFullName())
        for _, diagnostic in ipairs(parseResult.diagnostics) do
            warn(string.format("  %s (%d:%d): %s", diagnostic.severity, diagnostic.line, diagnostic.column, diagnostic.message))
        end
    end

    local ir = Lowerer.lower(parseResult)
    local out = Emitter.emit(ir.modules[1])

    moduleScript.Source = "-- Compiled by LUSharp (do not edit)\n" .. out
    return true
end

local function compileActiveOrSelected()
    local target = currentScript
    if not target then
        local selection = Selection:Get()
        target = selection[1]
    end

    if not target then
        warn("[LUSharp] No script selected.")
        return
    end

    local ok, err = pcall(function()
        local compiled = compileOne(target)
        if not compiled then
            warn("[LUSharp] Selected instance is not a tagged LUSharp ModuleScript.")
        end
    end)

    if not ok then
        warn("[LUSharp] Build failed: " .. tostring(err))
    end
end

local function compileAll()
    local scripts = ScriptManager.getAll()
    local okCount = 0

    for _, moduleScript in ipairs(scripts) do
        local ok, err = pcall(function()
            if compileOne(moduleScript) then
                okCount += 1
            end
        end)

        if not ok then
            warn("[LUSharp] Build failed for " .. moduleScript:GetFullName() .. ": " .. tostring(err))
        end
    end

    print(string.format("[LUSharp] Build All complete (%d/%d).", okCount, #scripts))
end

local function createNewScript()
    local selection = Selection:Get()
    local parent = selection[1]

    if typeof(parent) ~= "Instance" then
        parent = game:GetService("ReplicatedStorage")
    end

    local baseName = "NewScript"
    local name = baseName
    local i = 1
    while parent:FindFirstChild(name) do
        i += 1
        name = baseName .. tostring(i)
    end

    local context = inferContextFromParent(parent)
    local moduleScript = ScriptManager.createScript(name, parent, context)

    if moduleScript then
        projectView:refresh()
        Selection:Set({ moduleScript })
        openScript(moduleScript)
    end
end

buildButton.Click:Connect(compileActiveOrSelected)
buildAllButton.Click:Connect(compileAll)
newScriptButton.Click:Connect(createNewScript)

editorButton.Click:Connect(function()
    editor:toggle()
end)

projectButton.Click:Connect(function()
    projectView:toggle()
end)

settingsButton.Click:Connect(function()
    settings:toggle()
end)

Selection.SelectionChanged:Connect(function()
    local selection = Selection:Get()
    local target = selection[1]

    if target and target:IsA("ModuleScript") then
        if ScriptManager.getSource(target) ~= nil then
            openScript(target)
        end
    end
end)

projectView:refresh()

print("[LUSharp] Plugin loaded")
