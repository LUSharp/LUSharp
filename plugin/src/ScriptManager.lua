local CollectionService = game:GetService("CollectionService")

local ScriptManager = {}

local TAG = "LUSharp"
local SOURCE_VALUE_NAME = "CSharpSource"
local SOURCE_TEMPLATE = [[public class %s {
    public static void GameEntry() {

    }
}
]]

local function getSourceValue(moduleScript)
    local sourceValue = nil

    for _, child in ipairs(moduleScript:GetChildren()) do
        if child.Name == SOURCE_VALUE_NAME then
            if child:IsA("StringValue") then
                if not sourceValue then
                    sourceValue = child
                else
                    child:Destroy()
                end
            else
                child:Destroy()
            end
        end
    end

    return sourceValue
end

local function ensureSourceValue(moduleScript)
    local value = getSourceValue(moduleScript)
    if value then
        return value
    end

    value = Instance.new("StringValue")
    value.Name = SOURCE_VALUE_NAME
    value.Value = string.format(SOURCE_TEMPLATE, moduleScript.Name)
    value.Parent = moduleScript
    return value
end

local function isManagedScript(instance)
    if not instance or not instance:IsA("LuaSourceContainer") then
        return false
    end

    return CollectionService:HasTag(instance, TAG)
end

function ScriptManager.getAll()
    local tagged = CollectionService:GetTagged(TAG)
    local scripts = {}

    for _, instance in ipairs(tagged) do
        if instance:IsA("LuaSourceContainer") then
            table.insert(scripts, instance)
        end
    end

    table.sort(scripts, function(a, b)
        return a:GetFullName() < b:GetFullName()
    end)

    return scripts
end

function ScriptManager.getSource(moduleScript)
    if not isManagedScript(moduleScript) then
        return nil
    end

    local sourceValue = getSourceValue(moduleScript)
    if sourceValue then
        return sourceValue.Value
    end

    return nil
end

function ScriptManager.setSource(moduleScript, source)
    if not isManagedScript(moduleScript) or type(source) ~= "string" then
        return false
    end

    local sourceValue = ensureSourceValue(moduleScript)
    sourceValue.Value = source
    return true
end

function ScriptManager.createScript(name, parent, context)
    if type(name) ~= "string" or name:match("^%s*$") then
        return nil
    end

    if typeof(parent) ~= "Instance" then
        return nil
    end

    if context ~= nil and type(context) ~= "string" then
        return nil
    end

    local className = "ModuleScript"
    if context == "Server" then
        className = "Script"
    elseif context == "Client" then
        className = "LocalScript"
    end

    local scriptInstance = Instance.new(className)
    scriptInstance.Name = name
    scriptInstance.Source = "-- Compiled by LUSharp (do not edit)\n"
    scriptInstance.Parent = parent

    CollectionService:AddTag(scriptInstance, TAG)

    local sourceValue = ensureSourceValue(scriptInstance)
    sourceValue.Value = string.format(SOURCE_TEMPLATE, name)

    if context then
        scriptInstance:SetAttribute("LUSharpContext", context)
    end

    return scriptInstance
end

function ScriptManager.renameScript(moduleScript, newName)
    if not isManagedScript(moduleScript) or type(newName) ~= "string" or newName:match("^%s*$") then
        return false
    end

    moduleScript.Name = newName

    local sourceValue = ensureSourceValue(moduleScript)
    if sourceValue.Value == "" then
        sourceValue.Value = string.format(SOURCE_TEMPLATE, newName)
    end

    return true
end

function ScriptManager.deleteScript(moduleScript)
    if not isManagedScript(moduleScript) then
        return false
    end

    CollectionService:RemoveTag(moduleScript, TAG)
    moduleScript:Destroy()
    return true
end

return ScriptManager
