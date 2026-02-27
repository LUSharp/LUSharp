local Provider = require("../src/RobloxValidityProvider")

local function run(describe, it, expect)
    describe("RobloxValidityProvider", function()
        it("uses bundled snapshot when cached profile is missing", function()
            local resolved, source = Provider.resolveActiveProfile(nil)
            expect(type(resolved)):toBe("table")
            expect(type(resolved.namespaces)):toBe("table")
            expect(type(resolved.classes)):toBe("table")
            expect(source):toBe("snapshot")
        end)

        it("ignores invalid cached profile and falls back to snapshot", function()
            local badCache = {
                metadata = {
                    schemaVersion = 1,
                    retrievedAtUtc = "2026-02-26T10:00:00Z",
                },
            }

            local resolved, source = Provider.resolveActiveProfile(badCache)
            expect(source):toBe("snapshot")
            expect(type(resolved.namespaces)):toBe("table")
        end)

        it("prefers valid cached profile when it is newer than snapshot", function()
            local cached = {
                metadata = {
                    schemaVersion = 1,
                    retrievedAtUtc = "9999-12-31T23:59:59Z",
                    sourceContentHash = "abc123",
                },
                namespaces = {
                    Roblox = true,
                    RobloxCustom = true,
                },
                classes = {
                    Part = true,
                    Players = true,
                },
            }

            local resolved, source = Provider.resolveActiveProfile(cached)
            expect(source):toBe("cache")
            expect(resolved.namespaces.RobloxCustom):toBe(true)
        end)
    end)
end

return run
