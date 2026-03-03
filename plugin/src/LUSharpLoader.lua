-- LUSharp Lazy Loader
-- Discovers, requires, instantiates, and runs all LUSharp ModuleScripts

local CollectionService = game:GetService("CollectionService")

local TAG = "LUSharp"

local _modules = {}
local _registry = {}

-- Phase 1+2: Discover and require all tagged ModuleScripts
local tagged = CollectionService:GetTagged(TAG)
for _, instance in tagged do
    if instance:IsA("ModuleScript") then
        local ok, classTable = pcall(require, instance)
        if ok and type(classTable) == "table" then
            _modules[instance.Name] = classTable
        else
            warn("[LUSharp] Failed to load module: " .. instance:GetFullName())
        end
    end
end

-- Phase 3: Instantiate singletons
for name, classTable in _modules do
    if type(classTable.new) == "function" then
        local ok, instance = pcall(classTable.new)
        if ok then
            _registry[name] = instance
        else
            warn("[LUSharp] Failed to instantiate: " .. name)
        end
    else
        _registry[name] = classTable
    end
end

-- Phase 4: Call Main() on each singleton that has it
for name, instance in _registry do
    if type(instance.Main) == "function" then
        local ok, err = pcall(function()
            instance:Main()
        end)
        if not ok then
            warn("[LUSharp] Error in " .. name .. ":Main() — " .. tostring(err))
        end
    end
end
