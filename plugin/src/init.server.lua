-- LUSharp Studio Plugin
-- Entry point

local Selection = game:GetService("Selection")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local RunService = game:GetService("RunService")

local ENABLE_DEV_MODULE_ROOT = false

local function getDevModuleRoot()
    if not ENABLE_DEV_MODULE_ROOT then
        return nil
    end

    if not RunService:IsStudio() then
        return nil
    end

    local src = ReplicatedStorage:FindFirstChild("src")
    local test = src and src:FindFirstChild("test")
    if test and test:IsA("Folder") then
        return test
    end

    return nil
end

local DEV_MODULE_ROOT = getDevModuleRoot()
warn("[LUSharp SelDbg] init-server-loaded[v4] devRoot=" .. tostring(DEV_MODULE_ROOT ~= nil))

local function requireModule(name)
    if DEV_MODULE_ROOT then
        local devModule = DEV_MODULE_ROOT:FindFirstChild(name)
        if devModule and devModule:IsA("ModuleScript") then
            return require(devModule)
        end
    end

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
    "rbxassetid://101453582644426",
    "Build"
)

local buildAllButton = toolbar:CreateButton(
    "BuildAll",
    "Compile all tagged C# scripts",
    "rbxassetid://101453582644426",
    "Build All"
)

local newScriptButton = toolbar:CreateButton(
    "NewScript",
    "Create a new LUSharp C# script",
    "rbxassetid://140243957745633",
    "New C# Script"
)

local editorButton = toolbar:CreateButton(
    "Editor",
    "Toggle LUSharp editor",
    "rbxassetid://135264217576717",
    "Editor"
)

local projectButton = toolbar:CreateButton(
    "Project",
    "Toggle LUSharp project view",
    "rbxassetid://129582648595696",
    "Project"
)

local settingsButton = toolbar:CreateButton(
    "Settings",
    "Toggle LUSharp settings",
    "rbxassetid://103086022120574",
    "Settings"
)

for _, btn in ipairs({ buildButton, buildAllButton, newScriptButton, editorButton, projectButton, settingsButton }) do
    btn.ClickableWhenViewportHidden = true
end

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
local settingsValues = settings:get()

local editor = Editor.new(plugin, {
    fileName = "<untitled.cs>",
})
warn("[LUSharp SelDbg] editor-created")
local projectView = ProjectView.new(plugin)

editor:applySettings(settingsValues)
settings:setOnChanged(function(values)
    settingsValues = values
    editor:applySettings(values)
end)

local currentScript = nil
local suppressDirtyTracking = false

local diagnosticsRequestToken = 0
local diagnosticsWorkerRunning = false
local pendingDiagnosticsRequest = nil
local diagnosticsCacheSource = nil
local diagnosticsCacheResult = nil
local MAX_DIAGNOSTICS_SOURCE_LENGTH = 120000
local MAX_DIAGNOSTICS_TOKEN_COUNT = 10000
local PARSER_YIELD_EVERY = 1200
local DIAGNOSTICS_MAX_PARSE_OPERATIONS = 180000
local LARGE_DIAGNOSTICS_MAX_PARSE_OPERATIONS = 60000
local BUILD_MAX_PARSE_OPERATIONS = 500000

local function computeDiagnosticsDebounceSeconds(source)
    local length = #(source or "")
    if length >= 40000 then
        return 0.9
    end
    if length >= 20000 then
        return 0.6
    end
    if length >= 10000 then
        return 0.35
    end
    return 0.18
end

local function parseDiagnostics(source)
    source = source or ""

    if diagnosticsCacheSource == source and diagnosticsCacheResult ~= nil then
        return diagnosticsCacheResult
    end

    local tokens = Lexer.tokenize(source)
    task.wait()

    local maxOperations = DIAGNOSTICS_MAX_PARSE_OPERATIONS
    if #tokens >= MAX_DIAGNOSTICS_TOKEN_COUNT then
        maxOperations = LARGE_DIAGNOSTICS_MAX_PARSE_OPERATIONS
    end

    local parseResult = Parser.parse(tokens, {
        yieldEvery = PARSER_YIELD_EVERY,
        maxOperations = maxOperations,
    })
    local diagnostics = IntelliSense.getDiagnostics(parseResult)

    diagnosticsCacheSource = source
    diagnosticsCacheResult = diagnostics

    return diagnostics
end

local function runDiagnosticsRequest(request)
    if diagnosticsWorkerRunning then
        pendingDiagnosticsRequest = request
        return
    end

    diagnosticsWorkerRunning = true
    task.spawn(function()
        if request.token ~= diagnosticsRequestToken or currentScript ~= request.script then
            diagnosticsWorkerRunning = false
            if pendingDiagnosticsRequest ~= nil then
                local nextRequest = pendingDiagnosticsRequest
                pendingDiagnosticsRequest = nil
                task.defer(function()
                    runDiagnosticsRequest(nextRequest)
                end)
            end
            return
        end

        local diagnostics = parseDiagnostics(request.source)
        task.wait()

        if request.token == diagnosticsRequestToken
            and currentScript == request.script
            and ScriptManager.getSource(request.script) == request.source then
            editor:setDiagnostics(diagnostics)
        end

        diagnosticsWorkerRunning = false

        if pendingDiagnosticsRequest ~= nil then
            local nextRequest = pendingDiagnosticsRequest
            pendingDiagnosticsRequest = nil
            task.defer(function()
                runDiagnosticsRequest(nextRequest)
            end)
        end
    end)
end

local function queueEditorDiagnostics(scriptInstance, source, immediate)
    if not scriptInstance then
        return
    end

    source = source or ""

    diagnosticsRequestToken += 1
    local token = diagnosticsRequestToken
    local request = {
        token = token,
        script = scriptInstance,
        source = source,
    }

    local delaySeconds = immediate and 0 or computeDiagnosticsDebounceSeconds(source)
    task.delay(delaySeconds, function()
        if token ~= diagnosticsRequestToken then
            return
        end
        if currentScript ~= scriptInstance then
            return
        end
        runDiagnosticsRequest(request)
    end)
end

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

    suppressDirtyTracking = true
    editor:setSource(source, { deferRefresh = true })
    suppressDirtyTracking = false

    queueEditorDiagnostics(moduleScript, source, true)

    editor:show()
    editor:focus()
end

editor:setOnSourceChanged(function(source)
    if not currentScript or suppressDirtyTracking then
        return
    end

    local existing = ScriptManager.getSource(currentScript)
    if existing ~= source then
        ScriptManager.setSource(currentScript, source)
        projectView:setScriptDirty(currentScript, true)
    end

    queueEditorDiagnostics(currentScript, source, false)
end)

editor:setOnRequestCompletions(function()
    local cursorPos = editor.textBox.CursorPosition
    local context = currentScript and currentScript:GetAttribute("LUSharpContext")
    local completions = IntelliSense.getCompletions(editor:getSource(), cursorPos, {
        context = context,
        visibleServices = settingsValues and settingsValues.intellisenseVisibleServices,
    })
    editor:showCompletions(completions)
    return completions
end)

editor:setOnRequestHoverInfo(function(cursorPos)
    if not currentScript then
        return nil
    end

    local source = editor:getSource() or ""
    local sourceLength = #source
    if sourceLength == 0 then
        return nil
    end

    local context = currentScript:GetAttribute("LUSharpContext")

    if cursorPos < 1 or cursorPos > sourceLength + 1 then
        return nil
    end

    return IntelliSense.getHoverInfo(source, cursorPos, {
        context = context,
        searchNearby = true,
        nearbyRadius = 20,
    })
end)

projectView:setOnScriptSelected(function(moduleScript)
    if currentScript ~= moduleScript then
        openScript(moduleScript)
    else
        editor:show()
        editor:focus()
    end
end)

local function countErrorDiagnostics(parseResult)
    if not parseResult or not parseResult.diagnostics then
        return 0
    end

    local errorCount = 0
    for _, diagnostic in ipairs(parseResult.diagnostics) do
        local severity = string.lower(tostring(diagnostic.severity or ""))
        if severity == "error" then
            errorCount += 1
        end
    end

    return errorCount
end

local function compileOne(scriptInstance)
    local source = ScriptManager.getSource(scriptInstance)
    if source == nil then
        return false, "missing_source"
    end

    local sourceSnapshot = source

    local tokens = Lexer.tokenize(sourceSnapshot)
    task.wait()

    local parseResult = Parser.parse(tokens, {
        yieldEvery = PARSER_YIELD_EVERY,
        maxOperations = BUILD_MAX_PARSE_OPERATIONS,
    })
    local errorCount = countErrorDiagnostics(parseResult)
    task.wait()

    if parseResult.aborted then
        warn("[LUSharp] Parse aborted due to parser operation budget in " .. scriptInstance:GetFullName())
        projectView:setScriptBuildStatus(scriptInstance, {
            ok = false,
            errors = math.max(1, errorCount),
            dirty = true,
        })
        return false, "parse_budget"
    end

    if parseResult.diagnostics and #parseResult.diagnostics > 0 then
        warn("[LUSharp] Parse diagnostics in " .. scriptInstance:GetFullName())
        for _, diagnostic in ipairs(parseResult.diagnostics) do
            warn(string.format("  %s (%d:%d): %s", diagnostic.severity, diagnostic.line, diagnostic.column, diagnostic.message))
        end
    end

    local ir = Lowerer.lower(parseResult)
    task.wait()

    local desiredType = "ModuleScript"
    if scriptInstance:IsA("Script") then
        desiredType = "Script"
    elseif scriptInstance:IsA("LocalScript") then
        desiredType = "LocalScript"
    end

    if ir.modules and ir.modules[1] then
        ir.modules[1].scriptType = desiredType
    end

    local out = Emitter.emit(ir.modules[1])
    task.wait()

    local latestSource = ScriptManager.getSource(scriptInstance)
    if latestSource ~= sourceSnapshot then
        projectView:setScriptDirty(scriptInstance, true)
        warn("[LUSharp] Build skipped for " .. scriptInstance:GetFullName() .. " because source changed during build.")
        return false, "stale_source"
    end

    scriptInstance.Source = "-- Compiled by LUSharp (do not edit)\n" .. out
    projectView:setScriptBuildStatus(scriptInstance, {
        ok = errorCount == 0,
        errors = errorCount,
        dirty = false,
    })
    return true, nil
end

local buildQueue = {}
local isBuildQueueRunning = false

local function enqueueBuild(label, run)
    table.insert(buildQueue, {
        label = label,
        run = run,
    })

    if isBuildQueueRunning then
        print(string.format("[LUSharp] Queued build request: %s", label))
        return
    end

    isBuildQueueRunning = true
    task.spawn(function()
        while #buildQueue > 0 do
            local job = table.remove(buildQueue, 1)
            print(string.format("[LUSharp] Build started: %s", job.label))

            local ok, err = xpcall(job.run, debug.traceback)
            if not ok then
                warn("[LUSharp] Build job failed (" .. tostring(job.label) .. "): " .. tostring(err))
            end

            task.wait()
        end

        isBuildQueueRunning = false
    end)
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

    enqueueBuild("active/selected", function()
        local ok, err = pcall(function()
            local compiled, reason = compileOne(target)
            if not compiled and reason == "missing_source" then
                warn("[LUSharp] Selected instance is not a tagged LUSharp script.")
            end
        end)

        if not ok then
            if target:IsA("LuaSourceContainer") then
                projectView:setScriptBuildStatus(target, { ok = false, errors = 1, dirty = true })
            end
            warn("[LUSharp] Build failed: " .. tostring(err))
        end
    end)
end

local function compileAll()
    enqueueBuild("all scripts", function()
        local scripts = ScriptManager.getAll()
        local okCount = 0
        local total = #scripts

        for index, moduleScript in ipairs(scripts) do
            local ok, err = pcall(function()
                if compileOne(moduleScript) then
                    okCount += 1
                end
            end)

            if not ok then
                projectView:setScriptBuildStatus(moduleScript, { ok = false, errors = 1, dirty = true })
                warn("[LUSharp] Build failed for " .. moduleScript:GetFullName() .. ": " .. tostring(err))
            end

            if total > 1 then
                print(string.format("[LUSharp] Build All progress (%d/%d)", index, total))
            end
            task.wait()
        end

        print(string.format("[LUSharp] Build All complete (%d/%d).", okCount, total))
    end)
end

local function gatherBuildTargets(target, includeSubtree)
    local targets = {}
    local seen = setmetatable({}, { __mode = "k" })

    local function addIfManaged(scriptInstance)
        if not scriptInstance or not scriptInstance:IsA("LuaSourceContainer") then
            return
        end
        if ScriptManager.getSource(scriptInstance) == nil then
            return
        end
        if seen[scriptInstance] then
            return
        end
        seen[scriptInstance] = true
        table.insert(targets, scriptInstance)
    end

    if not target then
        return targets
    end

    if target:IsA("LuaSourceContainer") and not includeSubtree then
        addIfManaged(target)
        return targets
    end

    for _, scriptInstance in ipairs(ScriptManager.getAll()) do
        if includeSubtree then
            if scriptInstance == target or scriptInstance:IsDescendantOf(target) then
                addIfManaged(scriptInstance)
            end
        else
            if scriptInstance.Parent == target then
                addIfManaged(scriptInstance)
            end
        end
    end

    if #targets == 0 then
        addIfManaged(target)
    end

    return targets
end

local function buildTargets(targets)
    local okCount = 0
    local total = #targets

    for index, scriptInstance in ipairs(targets) do
        local ok, err = pcall(function()
            if compileOne(scriptInstance) then
                okCount += 1
            end
        end)

        if not ok then
            projectView:setScriptBuildStatus(scriptInstance, { ok = false, errors = 1, dirty = true })
            warn("[LUSharp] Build failed for " .. scriptInstance:GetFullName() .. ": " .. tostring(err))
        end

        if total > 1 then
            print(string.format("[LUSharp] Build progress (%d/%d)", index, total))
        end
        task.wait()
    end

    return okCount
end

local function buildFromProjectNode(target, includeSubtree)
    enqueueBuild("project node", function()
        local targets = gatherBuildTargets(target, includeSubtree)
        if #targets == 0 then
            warn("[LUSharp] No LUSharp scripts found for this build target.")
            return
        end

        local okCount = buildTargets(targets)
        print(string.format("[LUSharp] Build complete (%d/%d).", okCount, #targets))
    end)
end

local function nextAvailableScriptName(parent, baseName)
    local name = baseName
    local i = 1
    while parent:FindFirstChild(name) do
        i += 1
        name = baseName .. tostring(i)
    end
    return name
end

local function createNewScriptInParent(parent, requestedName)
    if typeof(parent) ~= "Instance" then
        return nil
    end

    local trimmed = trim(tostring(requestedName or ""))
    local baseName = trimmed ~= "" and trimmed or "NewScript"
    local name = nextAvailableScriptName(parent, baseName)
    local context = inferContextFromParent(parent)
    local moduleScript = ScriptManager.createScript(name, parent, context)

    if moduleScript then
        projectView:refresh()
        openScript(moduleScript)
        Selection:Set({ moduleScript })
        projectView:setScriptDirty(moduleScript, true)
    end

    return moduleScript
end

local function createNewScript()
    local selection = Selection:Get()
    local parent = selection[1]

    if typeof(parent) ~= "Instance" then
        parent = game:GetService("ReplicatedStorage")
    elseif parent:IsA("LuaSourceContainer") then
        parent = parent.Parent
    end

    createNewScriptInParent(parent)
end

projectView:setOnRequestNewScript(function(parent)
    local targetParent = parent
    if typeof(targetParent) ~= "Instance" then
        targetParent = game:GetService("ReplicatedStorage")
    elseif targetParent == game then
        targetParent = game:GetService("ReplicatedStorage")
    elseif targetParent:IsA("LuaSourceContainer") then
        targetParent = targetParent.Parent
    end

    local suggestedName = nextAvailableScriptName(targetParent, "NewScript")
    showNewScriptPrompt(suggestedName, function(inputName)
        local finalName = trim(inputName)
        if finalName == "" then
            finalName = suggestedName
        end
        createNewScriptInParent(targetParent, finalName)
    end)
end)

projectView:setOnRequestRename(function(scriptInstance)
    showNewScriptPrompt(scriptInstance.Name, function(inputName)
        local newName = trim(inputName)
        if newName == "" then
            return
        end

        if ScriptManager.renameScript(scriptInstance, newName) then
            if currentScript == scriptInstance then
                editor:setFilename(scriptInstance.Name .. ".cs")
            end
            projectView:refresh()
        end
    end)
end)

projectView:setOnRequestDelete(function(scriptInstance)
    if currentScript == scriptInstance then
        diagnosticsRequestToken += 1
        pendingDiagnosticsRequest = nil

        currentScript = nil
        editor:setFilename("<untitled.cs>")
        editor:setSource("")
        editor:setDiagnostics({})
    end

    if ScriptManager.deleteScript(scriptInstance) then
        projectView:refresh()
    end
end)

projectView:setOnRequestMove(function(scriptInstance, destination)
    if typeof(destination) ~= "Instance" then
        return
    end

    if scriptInstance == destination or destination:IsDescendantOf(scriptInstance) then
        warn("[LUSharp] Invalid move destination.")
        return
    end

    scriptInstance.Parent = destination
    scriptInstance:SetAttribute("LUSharpContext", inferContextFromParent(destination))
    projectView:refresh()
    Selection:Set({ scriptInstance })
end)

projectView:setOnRequestBuild(function(instance, includeSubtree)
    buildFromProjectNode(instance, includeSubtree and true or false)
end)

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

    projectView.selectedInstance = (typeof(target) == "Instance") and target or nil

    if target and target:IsA("LuaSourceContainer") and ScriptManager.getSource(target) ~= nil then
        if currentScript ~= target then
            openScript(target)
        end
    end

    projectView:refresh()
end)

projectView:refresh()

editor:show()
editor:focus()

print("[LUSharp] Plugin loaded")
