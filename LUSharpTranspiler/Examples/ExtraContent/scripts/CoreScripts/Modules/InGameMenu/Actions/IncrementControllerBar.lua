local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("INCREMENT_CONTROLLER_BAR_COUNT", function()
	return {}
end)
