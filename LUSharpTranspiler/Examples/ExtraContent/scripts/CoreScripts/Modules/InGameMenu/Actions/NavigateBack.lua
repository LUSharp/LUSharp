local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("NAVIGATE_BACK", function()
	return {}
end)
