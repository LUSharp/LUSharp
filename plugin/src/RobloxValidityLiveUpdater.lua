local function requireModule(name)
    if typeof(script) == "Instance" and script.Parent then
        local moduleScript = script.Parent:FindFirstChild(name)
        if moduleScript then
            return require(moduleScript)
        end
    end

    return require("./" .. name)
end

local Provider = requireModule("RobloxValidityProvider")

local RobloxValidityLiveUpdater = {}
RobloxValidityLiveUpdater.__index = RobloxValidityLiveUpdater

local function getTimestamp(profile)
    local metadata = type(profile) == "table" and profile.metadata or nil
    local ts = type(metadata) == "table" and metadata.retrievedAtUtc or nil
    if type(ts) ~= "string" then
        return ""
    end

    return ts
end

function RobloxValidityLiveUpdater.new(options)
    options = options or {}

    local self = setmetatable({}, RobloxValidityLiveUpdater)
    self.provider = options.provider or Provider
    self.getCachedProfile = options.getCachedProfile
    self.setCachedProfile = options.setCachedProfile
    self.fetchLiveProfile = options.fetchLiveProfile
    self.onProfileChanged = options.onProfileChanged
    self.updateIntervalSeconds = tonumber(options.updateIntervalSeconds) or 900
    self._running = false

    local cachedProfile = nil
    if type(self.getCachedProfile) == "function" then
        local ok, result = pcall(self.getCachedProfile)
        if ok then
            cachedProfile = result
        end
    end

    self.activeProfile, self.activeSource = self.provider.resolveActiveProfile(cachedProfile)
    return self
end

function RobloxValidityLiveUpdater:getActiveProfile()
    return self.activeProfile, self.activeSource
end

function RobloxValidityLiveUpdater:refreshNow()
    if type(self.fetchLiveProfile) ~= "function" then
        return self.activeProfile, self.activeSource, false
    end

    local ok, fetchedOrErr = pcall(self.fetchLiveProfile)
    if not ok then
        return self.activeProfile, self.activeSource, false
    end

    local fetchedProfile = fetchedOrErr
    if not self.provider.isValidProfile(fetchedProfile) then
        return self.activeProfile, self.activeSource, false
    end

    local activeTs = getTimestamp(self.activeProfile)
    local fetchedTs = getTimestamp(fetchedProfile)
    if fetchedTs <= activeTs then
        return self.activeProfile, self.activeSource, false
    end

    if type(self.setCachedProfile) == "function" then
        local persisted = pcall(self.setCachedProfile, fetchedProfile)
        if not persisted then
            return self.activeProfile, self.activeSource, false
        end
    end

    self.activeProfile = fetchedProfile
    self.activeSource = "live"

    if type(self.onProfileChanged) == "function" then
        self.onProfileChanged(self.activeProfile, self.activeSource)
    end

    return self.activeProfile, self.activeSource, true
end

function RobloxValidityLiveUpdater:start()
    if self._running then
        return
    end

    if type(self.fetchLiveProfile) ~= "function" then
        return
    end

    self._running = true
    task.spawn(function()
        while self._running do
            task.wait(self.updateIntervalSeconds)
            self:refreshNow()
        end
    end)
end

function RobloxValidityLiveUpdater:stop()
    self._running = false
end

return RobloxValidityLiveUpdater
