-- LUSharp Studio Plugin
-- Entry point

local Selection = game:GetService("Selection")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local RunService = game:GetService("RunService")
local HttpService = game:GetService("HttpService")

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
local RobloxValidityLiveUpdater = requireModule("RobloxValidityLiveUpdater")

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

for _, btn in { buildButton, buildAllButton, newScriptButton, editorButton, projectButton, settingsButton } do
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
local openEditors = {} -- { [scriptInstance] = { editor = Editor, script = scriptInstance } }
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
local diagnosticsCacheProfileStamp = nil
local diagnosticsCacheResult = nil
local MAX_DIAGNOSTICS_SOURCE_LENGTH = 120000
local MAX_DIAGNOSTICS_TOKEN_COUNT = 10000
local PARSER_YIELD_EVERY = 1200
local DIAGNOSTICS_MAX_PARSE_OPERATIONS = 180000
local LARGE_DIAGNOSTICS_MAX_PARSE_OPERATIONS = 60000
local BUILD_MAX_PARSE_OPERATIONS = 500000

local ROBLOX_VALIDITY_PROFILE_URL_KEY = "LUSharp.RobloxValidityProfileUrl"
local ROBLOX_VALIDITY_REFRESH_SECONDS_KEY = "LUSharp.RobloxValidityRefreshSeconds"
local DEFAULT_ROBLOX_VALIDITY_REFRESH_SECONDS = 900
local validityUpdater = nil

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

local function getActiveValidityProfile()
    if not validityUpdater then
        return nil
    end

    local profile = validityUpdater:getActiveProfile()
    if type(profile) ~= "table" then
        return nil
    end

    return profile
end

local function getValidityProfileStamp(profile)
    local metadata = type(profile) == "table" and profile.metadata or nil
    local stamp = type(metadata) == "table" and metadata.retrievedAtUtc or nil
    if type(stamp) ~= "string" then
        return ""
    end

    return stamp
end

local function parseDiagnostics(source)
    source = source or ""

    local validityProfile = getActiveValidityProfile()
    local profileStamp = getValidityProfileStamp(validityProfile)

    if diagnosticsCacheSource == source and diagnosticsCacheProfileStamp == profileStamp and diagnosticsCacheResult ~= nil then
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
    local diagnostics = IntelliSense.getDiagnostics(parseResult, source, {
        validityProfile = validityProfile,
        currentScript = currentScript,
    })

    diagnosticsCacheSource = source
    diagnosticsCacheProfileStamp = profileStamp
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

local function createEditorForScript(moduleScript)
    local scriptName = moduleScript.Name
    local newEditor = Editor.new(plugin, {
        width = 650,
        height = 520,
        widgetId = "LUSharpEditor_" .. scriptName .. "_" .. tostring(moduleScript:GetDebugId()),
        fileName = scriptName .. ".cs",
        dockState = Enum.InitialDockState.Right,
    })

    -- Apply current settings
    if settingsValues then
        newEditor:applySettings(settingsValues)
    end

    -- Set up source
    local source = ScriptManager.getSource(moduleScript)
    if source == nil then
        ScriptManager.setSource(moduleScript, "")
        source = ""
    end
    newEditor:setSource(source, { deferRefresh = true })

    -- Set up callbacks
    newEditor:setOnSourceChanged(function(newSource)
        local existing = ScriptManager.getSource(moduleScript)
        if existing ~= newSource then
            ScriptManager.setSource(moduleScript, newSource)
            projectView:setScriptDirty(moduleScript, true)
        end
        queueEditorDiagnostics(moduleScript, newSource, false)
    end)

    newEditor:setOnRequestCompletions(function()
        local cursorPos = newEditor.textBox.CursorPosition
        local context = moduleScript:GetAttribute("LUSharpContext")
        local completions = IntelliSense.getCompletions(newEditor:getSource(), cursorPos, {
            context = context,
            currentScript = moduleScript,
            visibleServices = settingsValues and settingsValues.intellisenseVisibleServices,
            validityProfile = getActiveValidityProfile(),
        })
        newEditor:showCompletions(completions)
        return completions
    end)

    newEditor:setOnRequestHoverInfo(function(cursorPos)
        local hoverSource = newEditor:getSource() or ""
        local context = moduleScript:GetAttribute("LUSharpContext")
        return IntelliSense.getHoverInfo(hoverSource, cursorPos, {
            context = context,
            currentScript = moduleScript,
            searchNearby = true,
            nearbyRadius = 20,
            validityProfile = getActiveValidityProfile(),
        })
    end)

    -- Run initial diagnostics
    queueEditorDiagnostics(moduleScript, source, true)

    newEditor:show()
    newEditor:focus()

    openEditors[moduleScript] = { editor = newEditor, script = moduleScript }
    return newEditor
end

local function makeRobloxValidityFetchFunction()
    local profileUrl = plugin:GetSetting(ROBLOX_VALIDITY_PROFILE_URL_KEY)
    if type(profileUrl) ~= "string" or profileUrl == "" then
        return nil
    end

    return function()
        local body = HttpService:GetAsync(profileUrl, false)
        local decoded = HttpService:JSONDecode(body)
        if type(decoded) ~= "table" then
            error("Roblox validity endpoint returned non-table payload")
        end
        return decoded
    end
end

local function invalidateDiagnosticsCache()
    diagnosticsCacheSource = nil
    diagnosticsCacheProfileStamp = nil
    diagnosticsCacheResult = nil
end

local function setupRobloxValidityUpdater()
    local refreshSeconds = tonumber(plugin:GetSetting(ROBLOX_VALIDITY_REFRESH_SECONDS_KEY))
        or DEFAULT_ROBLOX_VALIDITY_REFRESH_SECONDS

    local fetchLiveProfile = makeRobloxValidityFetchFunction()

    validityUpdater = RobloxValidityLiveUpdater.new({
        getCachedProfile = function()
            return settings:getCachedRobloxValidityProfile()
        end,
        setCachedProfile = function(profile)
            settings:setCachedRobloxValidityProfile(profile)
        end,
        fetchLiveProfile = fetchLiveProfile,
        updateIntervalSeconds = refreshSeconds,
        onProfileChanged = function()
            invalidateDiagnosticsCache()
            if currentScript then
                queueEditorDiagnostics(currentScript, editor:getSource() or "", true)
            end
        end,
    })

    validityUpdater:refreshNow()
    if fetchLiveProfile then
        validityUpdater:start()
    end
end

setupRobloxValidityUpdater()

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
    -- If script has a secondary editor window, focus it
    local existing = openEditors[moduleScript]
    if existing and existing.editor then
        existing.editor:show()
        existing.editor:focus()
        local source = ScriptManager.getSource(moduleScript)
        if source then
            existing.editor:setSource(source, { deferRefresh = true })
        end
        return
    end

    -- Switch the main editor to this script
    currentScript = moduleScript
    editor:setFilename(moduleScript.Name .. ".cs")

    suppressDirtyTracking = true
    local source = ScriptManager.getSource(moduleScript)
    if source == nil then
        ScriptManager.setSource(moduleScript, "")
        source = ""
    end
    editor:setSource(source, { deferRefresh = true })
    suppressDirtyTracking = false

    editor:show()
    editor:focus()

    queueEditorDiagnostics(moduleScript, source, true)
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
        currentScript = currentScript,
        visibleServices = settingsValues and settingsValues.intellisenseVisibleServices,
        validityProfile = getActiveValidityProfile(),
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

    local diagnostics = nil
    if diagnosticsCacheSource == source and diagnosticsCacheResult ~= nil then
        diagnostics = diagnosticsCacheResult
    else
        diagnostics = parseDiagnostics(source)
    end

    return IntelliSense.getHoverInfo(source, cursorPos, {
        context = context,
        currentScript = currentScript,
        searchNearby = true,
        nearbyRadius = 20,
        diagnostics = diagnostics,
        validityProfile = getActiveValidityProfile(),
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
    for _, diagnostic in parseResult.diagnostics do
        local severity = string.lower(tostring(diagnostic.severity or ""))
        if severity == "error" then
            errorCount += 1
        end
    end

    return errorCount
end

local LUSHARP_RUNTIME_SOURCE = [=[
-- LUSharp Runtime Loader (do not edit)
local Shared = {}

for _, child in script.Parent:GetChildren() do
    if child:IsA("ModuleScript") then
        local ok, result = pcall(require, child)
        if ok then
            Shared[child.Name] = result
        else
            warn("[LUSharp] Failed to load " .. child.Name .. ": " .. tostring(result))
        end
    end
end

-- Late-bind cross-script references
for name, mod in Shared do
    if type(mod) == "table" and type(mod.__init) == "function" then
        mod.__init(Shared)
    end
end

-- Call Main() on entry point modules
for name, mod in Shared do
    if type(mod) == "table" and type(mod.Main) == "function" then
        mod.Main()
    end
end
]=]

local function ensureRuntimeModule(parent)
    if not parent or typeof(parent) ~= "Instance" then return end
    local existing = parent:FindFirstChild("_LUSharpRuntime")
    if existing then
        -- Replace old ModuleScript with Script if needed
        if existing:IsA("ModuleScript") then
            existing:Destroy()
            existing = nil
        elseif existing:IsA("Script") then
            if existing.Source ~= LUSHARP_RUNTIME_SOURCE then
                existing.Source = LUSHARP_RUNTIME_SOURCE
            end
            return
        end
    end
    local loader = Instance.new("Script")
    loader.Name = "_LUSharpRuntime"
    loader.Source = LUSHARP_RUNTIME_SOURCE
    loader.Parent = parent
end

local function compileOne(scriptInstance)
    local source = ScriptManager.getSource(scriptInstance)
    if source == nil then
        return false, "missing_source"
    end

    ensureRuntimeModule(scriptInstance.Parent)

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
        for _, diagnostic in parseResult.diagnostics do
            warn(string.format("  %s (%d:%d): %s", diagnostic.severity, diagnostic.line, diagnostic.column, diagnostic.message))
        end
    end

    local ir = Lowerer.lower(parseResult)
    task.wait()

    -- Check for lowerer diagnostics (CS0176, CS0120, etc.)
    if ir.diagnostics and #ir.diagnostics > 0 then
        warn("[LUSharp] Lowerer diagnostics in " .. scriptInstance:GetFullName())
        for _, diagnostic in ir.diagnostics do
            warn(string.format("  %s: %s", diagnostic.code or "error", diagnostic.message))
        end
        projectView:setScriptBuildStatus(scriptInstance, {
            ok = false,
            errors = #ir.diagnostics + errorCount,
            dirty = true,
        })
        return false, "lowerer_errors"
    end

    -- All compiled scripts become ModuleScripts (runtime loader calls Main())
    if not scriptInstance:IsA("ModuleScript") then
        local newInstance = Instance.new("ModuleScript")
        newInstance.Name = scriptInstance.Name
        for _, tag in scriptInstance:GetTags() do
            newInstance:AddTag(tag)
        end
        for attrName, attrValue in scriptInstance:GetAttributes() do
            newInstance:SetAttribute(attrName, attrValue)
        end
        for _, child in scriptInstance:GetChildren() do
            child.Parent = newInstance
        end
        newInstance.Parent = scriptInstance.Parent
        scriptInstance:Destroy()
        scriptInstance = newInstance
    end

    if ir.modules and ir.modules[1] then
        ir.modules[1].scriptType = "ModuleScript"
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

        for index, moduleScript in scripts do
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

    for _, scriptInstance in ScriptManager.getAll() do
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

    for index, scriptInstance in targets do
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

projectView:setOnRequestOpenNewWindow(function(scriptInstance)
    if scriptInstance and scriptInstance:IsA("LuaSourceContainer") then
        createEditorForScript(scriptInstance)
    end
end)

projectView:setOnRequestOpenLuauView(function(scriptInstance)
    if scriptInstance and scriptInstance:IsA("LuaSourceContainer") then
        -- Open the compiled Luau source view
        local ScriptEditorService = game:GetService("ScriptEditorService")
        pcall(function()
            ScriptEditorService:OpenScriptDocument(scriptInstance)
        end)
    end
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

-- Auto-load the first available script on startup
do
    local scripts = ScriptManager.getAll()
    if #scripts > 0 then
        openScript(scripts[1])
    end
end

print("[LUSharp] Plugin loaded")
