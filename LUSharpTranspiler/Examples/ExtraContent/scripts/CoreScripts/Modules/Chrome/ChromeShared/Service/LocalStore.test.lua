local LocalStore = require(script.Parent.LocalStore)
local AppStorageService = game:GetService("AppStorageService")
local CorePackages = game:GetService("CorePackages")
local JestGlobals = require(CorePackages.Packages.Dev.JestGlobals3)
local expect = JestGlobals.expect
local describe = JestGlobals.describe
local it = JestGlobals.it
local beforeEach = JestGlobals.beforeEach
local afterEach = JestGlobals.afterEach

local APP_STORAGE_KEY = "InGameMenuState"

describe("IGM Storage", function()
	if LocalStore.isEnabled() then
		beforeEach(function()
			AppStorageService:SetItem(APP_STORAGE_KEY, "")
			AppStorageService:Flush()
			LocalStore.clearCache()
		end)

		afterEach(function()
			AppStorageService:SetItem(APP_STORAGE_KEY, "")
			AppStorageService:Flush()
			LocalStore.clearCache()
		end)

		it("should store/load without error", function()
			local testVal = 5555

			LocalStore.storeForAnyPlayer("test", testVal)
			local a = LocalStore.loadForAnyPlayer("test")

			expect(a).toBe(testVal)
		end)

		it("should load value with a clean cache", function()
			local testVal = 5555

			LocalStore.storeForAnyPlayer("test", testVal)
			LocalStore.clearCache()
			local a = LocalStore.loadForAnyPlayer("test")

			expect(a).toBe(testVal)
		end)

		it("should have no cross talk between local and any", function()
			local testVal = 5555

			LocalStore.storeForLocalPlayer("test", testVal)
			local a = LocalStore.loadForAnyPlayer("test")
			local b = LocalStore.loadForLocalPlayer("test")

			expect(a).toBeNil()
			expect(b).toBe(testVal)
		end)

		it("should store table types", function()
			local testVal1 = { abc = 5555 }
			local testVal2 = { abc = 6666 }

			LocalStore.storeForLocalPlayer("test", testVal1)
			LocalStore.storeForAnyPlayer("test", testVal2)

			local a = LocalStore.loadForLocalPlayer("test")
			local b = LocalStore.loadForAnyPlayer("test")

			expect(a.abc).toBe(testVal1.abc)
			expect(b.abc).toBe(testVal2.abc)

			LocalStore.clearCache()

			a = LocalStore.loadForLocalPlayer("test")
			b = LocalStore.loadForAnyPlayer("test")

			expect(a.abc).toBe(testVal1.abc)
			expect(b.abc).toBe(testVal2.abc)
		end)
	end
end)

describe("Exposed Universes", function()
	if LocalStore.isEnabled() then
		afterEach(function()
			AppStorageService:SetItem(APP_STORAGE_KEY, "")
			AppStorageService:Flush()
			LocalStore.clearCache()
		end)

		it("should return an empty list if no universes have been shown", function()
			local result = LocalStore.getUniversesExposedTo("test")
			expect(result).toEqual({})
		end)

		it("should add a universe to the list if not already present", function()
			LocalStore.addUniverseToExposureList("test", 1)
			local result = LocalStore.getUniversesExposedTo("test")
			expect(result).toEqual({ 1 })
		end)

		it("should not add a duplicate universe", function()
			LocalStore.addUniverseToExposureList("test", 1)
			LocalStore.addUniverseToExposureList("test", 1) -- Should not be added

			local result = LocalStore.getUniversesExposedTo("test")
			expect(result).toEqual({ 1 })
		end)

		it("should not add a universe if the limit is reached", function()
			LocalStore.addUniverseToExposureList("test", 1)
			LocalStore.addUniverseToExposureList("test", 2)
			LocalStore.addUniverseToExposureList("test", 3)
			LocalStore.addUniverseToExposureList("test", 4)
			LocalStore.addUniverseToExposureList("test", 5) -- Limit is 5
			LocalStore.addUniverseToExposureList("test", 6) -- Should not be added

			local result = LocalStore.getUniversesExposedTo("test")
			expect(result).toEqual({ 1, 2, 3, 4, 5 })
		end)

		it("should return 0 if no universes have been shown", function()
			local result = LocalStore.getNumUniversesExposedTo("test")
			expect(result).toBe(0)
		end)

		it("should return the correct number of shown universes", function()
			LocalStore.addUniverseToExposureList("test", 1)
			LocalStore.addUniverseToExposureList("test", 2)

			local result = LocalStore.getNumUniversesExposedTo("test")
			expect(result).toBe(2)
		end)
	end
end)
