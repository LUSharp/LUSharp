local LiveUpdater = require("../src/RobloxValidityLiveUpdater")

local function makeValidProfile(timestamp, hash)
    return {
        metadata = {
            schemaVersion = 1,
            retrievedAtUtc = timestamp,
            sourceContentHash = hash,
        },
        namespaces = {
            Roblox = true,
            ["Roblox.Services"] = true,
        },
        classes = {
            Players = true,
            Workspace = true,
        },
    }
end

local function run(describe, it, expect)
    describe("RobloxValidityLiveUpdater", function()
        it("prefers newer validated live profile over snapshot", function()
            local cachedProfile = nil
            local writes = 0
            local changedSource = nil

            local updater = LiveUpdater.new({
                getCachedProfile = function()
                    return cachedProfile
                end,
                setCachedProfile = function(profile)
                    cachedProfile = profile
                    writes += 1
                end,
                fetchLiveProfile = function()
                    return makeValidProfile("9999-12-31T23:59:59Z", "live-hash")
                end,
                onProfileChanged = function(_, source)
                    changedSource = source
                end,
            })

            local initialProfile, initialSource = updater:getActiveProfile()
            expect(type(initialProfile)):toBe("table")
            expect(initialSource):toBe("snapshot")

            local profile, source, updated = updater:refreshNow()
            expect(updated):toBe(true)
            expect(source):toBe("live")
            expect(type(profile)):toBe("table")
            expect(profile.metadata.retrievedAtUtc):toBe("9999-12-31T23:59:59Z")
            expect(changedSource):toBe("live")
            expect(writes):toBe(1)
            expect(type(cachedProfile)):toBe("table")
            expect(cachedProfile.metadata.retrievedAtUtc):toBe("9999-12-31T23:59:59Z")
        end)

        it("falls back cleanly when live fetch fails", function()
            local cachedProfile = nil
            local writes = 0

            local updater = LiveUpdater.new({
                getCachedProfile = function()
                    return cachedProfile
                end,
                setCachedProfile = function(profile)
                    cachedProfile = profile
                    writes += 1
                end,
                fetchLiveProfile = function()
                    error("network down")
                end,
            })

            local beforeProfile, beforeSource = updater:getActiveProfile()
            local profile, source, updated = updater:refreshNow()

            expect(updated):toBe(false)
            expect(source):toBe(beforeSource)
            expect(type(profile)):toBe("table")
            expect(type(beforeProfile)):toBe("table")
            expect(profile.metadata.retrievedAtUtc):toBe(beforeProfile.metadata.retrievedAtUtc)
            expect(writes):toBe(0)
        end)
    end)
end

return run
