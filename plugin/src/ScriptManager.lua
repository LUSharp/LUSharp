local ScriptManager = {}

local function getCollectionService()
    return game:GetService("CollectionService")
end

local TAG = "LUSharp"
local SOURCE_VALUE_NAME = "CSharpSource"
local NAMESPACE_ATTRIBUTE_NAME = "LUSharpNamespace"
local DEFAULT_NAMESPACE_ROOT = "Game"

local SOURCE_TEMPLATE = [[public class %s {
    public static void GameEntry() {

    }
}
]]

local SOURCE_TEMPLATE_WITH_NAMESPACE = [[namespace %s {
public class %s {
    public static void GameEntry() {

    }
}
}
]]

local function trim(text)
    if type(text) ~= "string" then
        return ""
    end

    return (text:gsub("^%s+", ""):gsub("%s+$", ""))
end

local function sanitizeNamespaceSegment(segment)
    local cleaned = trim(segment)
    if cleaned == "" then
        return nil
    end

    cleaned = cleaned:gsub("[^%w_]", "_")
    if cleaned == "" then
        return nil
    end

    if string.match(string.sub(cleaned, 1, 1), "%d") then
        cleaned = "_" .. cleaned
    end

    return cleaned
end

local function normalizeNamespace(namespaceValue)
    if type(namespaceValue) ~= "string" then
        return nil
    end

    local trimmed = trim(namespaceValue)
    if trimmed == "" then
        return nil
    end

    local parts = {}
    for part in string.gmatch(trimmed, "[^%.]+") do
        local sanitized = sanitizeNamespaceSegment(part)
        if sanitized then
            table.insert(parts, sanitized)
        end
    end

    if #parts == 0 then
        return nil
    end

    return table.concat(parts, ".")
end

local function defaultNamespaceForContext(context)
    local contextSegment = sanitizeNamespaceSegment(context)
    if not contextSegment then
        return nil
    end

    if contextSegment == "Other" then
        return DEFAULT_NAMESPACE_ROOT
    end

    return DEFAULT_NAMESPACE_ROOT .. "." .. contextSegment
end

function ScriptManager.resolveNamespace(context, explicitNamespace)
    local normalized = normalizeNamespace(explicitNamespace)
    if normalized then
        return normalized
    end

    return defaultNamespaceForContext(context)
end

function ScriptManager.buildSourceTemplate(className, context, explicitNamespace)
    local namespaceName = ScriptManager.resolveNamespace(context, explicitNamespace)
    if namespaceName then
        return string.format(SOURCE_TEMPLATE_WITH_NAMESPACE, namespaceName, className)
    end

    return string.format(SOURCE_TEMPLATE, className)
end

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
    value.Value = ScriptManager.buildSourceTemplate(
        moduleScript.Name,
        moduleScript:GetAttribute("LUSharpContext"),
        moduleScript:GetAttribute(NAMESPACE_ATTRIBUTE_NAME)
    )
    value.Parent = moduleScript
    return value
end

local function isManagedScript(instance)
    if not instance or not instance:IsA("LuaSourceContainer") then
        return false
    end

    return getCollectionService():HasTag(instance, TAG)
end

function ScriptManager.getAll()
    local tagged = getCollectionService():GetTagged(TAG)
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

function ScriptManager.createScript(name, parent, context, namespace)
    if type(name) ~= "string" or name:match("^%s*$") then
        return nil
    end

    if typeof(parent) ~= "Instance" then
        return nil
    end

    if context ~= nil and type(context) ~= "string" then
        return nil
    end

    if namespace ~= nil and type(namespace) ~= "string" then
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

    getCollectionService():AddTag(scriptInstance, TAG)

    if context then
        scriptInstance:SetAttribute("LUSharpContext", context)
    end

    local trimmedNamespace = trim(namespace)
    if trimmedNamespace ~= "" then
        scriptInstance:SetAttribute(NAMESPACE_ATTRIBUTE_NAME, trimmedNamespace)
    end

    local sourceValue = ensureSourceValue(scriptInstance)
    sourceValue.Value = ScriptManager.buildSourceTemplate(name, context, namespace)

    return scriptInstance
end

function ScriptManager.renameScript(moduleScript, newName)
    if not isManagedScript(moduleScript) or type(newName) ~= "string" or newName:match("^%s*$") then
        return false
    end

    moduleScript.Name = newName

    local sourceValue = ensureSourceValue(moduleScript)
    if sourceValue.Value == "" then
        sourceValue.Value = ScriptManager.buildSourceTemplate(
            newName,
            moduleScript:GetAttribute("LUSharpContext"),
            moduleScript:GetAttribute(NAMESPACE_ATTRIBUTE_NAME)
        )
    end

    return true
end

function ScriptManager.deleteScript(moduleScript)
    if not isManagedScript(moduleScript) then
        return false
    end

    getCollectionService():RemoveTag(moduleScript, TAG)
    moduleScript:Destroy()
    return true
end

return ScriptManager
