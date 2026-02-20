local CollectionService = game:GetService("CollectionService")

local ScriptManager = {}

local TAG = "LUSharp"
local SOURCE_VALUE_NAME = "CSharpSource"
local SOURCE_TEMPLATE = [[public class %s {

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

local function isManagedModule(instance)
    if not instance or not instance:IsA("ModuleScript") then
        return false
    end

    return CollectionService:HasTag(instance, TAG)
end

function ScriptManager.getAll()
    local tagged = CollectionService:GetTagged(TAG)
    local scripts = {}

    for _, instance in ipairs(tagged) do
        if instance:IsA("ModuleScript") then
            table.insert(scripts, instance)
        end
    end

    table.sort(scripts, function(a, b)
        return a:GetFullName() < b:GetFullName()
    end)

    return scripts
end

function ScriptManager.getSource(moduleScript)
    if not isManagedModule(moduleScript) then
        return nil
    end

    local sourceValue = getSourceValue(moduleScript)
    if sourceValue then
        return sourceValue.Value
    end

    return nil
end

function ScriptManager.setSource(moduleScript, source)
    if not isManagedModule(moduleScript) or type(source) ~= "string" then
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

    local moduleScript = Instance.new("ModuleScript")
    moduleScript.Name = name
    moduleScript.Source = "-- Compiled by LUSharp (do not edit)\nreturn {}"
    moduleScript.Parent = parent

    CollectionService:AddTag(moduleScript, TAG)

    local sourceValue = ensureSourceValue(moduleScript)
    sourceValue.Value = string.format(SOURCE_TEMPLATE, name)

    if context then
        moduleScript:SetAttribute("LUSharpContext", context)
    end

    return moduleScript
end

function ScriptManager.renameScript(moduleScript, newName)
    if not isManagedModule(moduleScript) or type(newName) ~= "string" or newName:match("^%s*$") then
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
    if not isManagedModule(moduleScript) then
        return false
    end

    CollectionService:RemoveTag(moduleScript, TAG)
    moduleScript:Destroy()
    return true
end

return ScriptManager
