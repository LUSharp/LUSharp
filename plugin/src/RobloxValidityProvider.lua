local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local Snapshot = requireModule("RobloxValiditySnapshot")

local RobloxValidityProvider = {}

local function isIsoTimestamp(value)
    return type(value) == "string" and #value >= 20 and string.find(value, "T", 1, true) ~= nil and string.sub(value, -1) == "Z"
end

function RobloxValidityProvider.isValidProfile(profile)
    if type(profile) ~= "table" then
        return false
    end

    local metadata = profile.metadata
    if type(metadata) ~= "table" then
        return false
    end

    if type(metadata.schemaVersion) ~= "number" then
        return false
    end

    if not isIsoTimestamp(metadata.retrievedAtUtc) then
        return false
    end

    if type(profile.namespaces) ~= "table" then
        return false
    end

    if type(profile.classes) ~= "table" then
        return false
    end

    return true
end

local function getRetrievedAtUtc(profile)
    local metadata = type(profile) == "table" and profile.metadata or nil
    local timestamp = type(metadata) == "table" and metadata.retrievedAtUtc or nil
    if type(timestamp) ~= "string" then
        return ""
    end

    return timestamp
end

function RobloxValidityProvider.resolveActiveProfile(cachedProfile)
    local snapshot = Snapshot
    if not RobloxValidityProvider.isValidProfile(snapshot) then
        error("Invalid bundled Roblox validity snapshot")
    end

    if RobloxValidityProvider.isValidProfile(cachedProfile) then
        local cacheTs = getRetrievedAtUtc(cachedProfile)
        local snapshotTs = getRetrievedAtUtc(snapshot)
        if cacheTs > snapshotTs then
            return cachedProfile, "cache"
        end
    end

    return snapshot, "snapshot"
end

return RobloxValidityProvider
