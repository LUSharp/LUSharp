local ChromeService = require(script.Parent.ChromeService)
local CorePackages = game:GetService("CorePackages")

local JestGlobals = require(CorePackages.Packages.Dev.JestGlobals3)
local expect = JestGlobals.expect
local describe = JestGlobals.describe
local it = JestGlobals.it

describe("Unibar Layout Signal", function()
	it("non-null initial values", function()
		local chromeService: ChromeService.ChromeService = ChromeService.new()
		local layoutSignal = chromeService:layout()
		local layout = layoutSignal:get()

		expect(layoutSignal)
		expect(layout)
	end)

	it("sends update signals", function()
		local chromeService: ChromeService.ChromeService = ChromeService.new()
		local layoutSignal = chromeService:layout()

		local layoutValue
		layoutSignal:connect(function(layout)
			layoutValue = layout
		end)

		chromeService:setMenuAbsolutePosition(Vector2.new(100, 200))

		expect(layoutValue)
		expect(layoutValue.Min.X).toBe(100)
		expect(layoutValue.Min.Y).toBe(200)
	end)

	it("sends minimal signal updates", function()
		local chromeService: ChromeService.ChromeService = ChromeService.new()
		local layoutSignal = chromeService:layout()

		local count = 0
		layoutSignal:connect(function(layout)
			count = count + 1
		end)

		chromeService:setMenuAbsolutePosition(Vector2.new(100, 200))
		chromeService:setMenuAbsolutePosition(Vector2.new(100, 200))
		chromeService:setMenuAbsolutePosition(Vector2.new(100, 200))
		expect(count).toBe(1)

		chromeService:setMenuAbsolutePosition(Vector2.new(200, 200))
		chromeService:setMenuAbsolutePosition(Vector2.new(200, 200))
		expect(count).toBe(2)

		chromeService:setMenuAbsoluteSize(Vector2.new(1000, 1000))
		chromeService:setMenuAbsoluteSize(Vector2.new(1000, 1000))
		expect(count).toBe(3)
	end)

	it("correctly calculated Rect", function()
		local chromeService: ChromeService.ChromeService = ChromeService.new()
		local layoutSignal = chromeService:layout()

		chromeService:setMenuAbsolutePosition(Vector2.new(100, 200))
		chromeService:setMenuAbsoluteSize(Vector2.new(1000, 50))

		local layout = layoutSignal:get()
		expect(layout.Width).toBe(1000)
		expect(layout.Min.X).toBe(100)
		expect(layout.Min.Y).toBe(200)
	end)
end)
