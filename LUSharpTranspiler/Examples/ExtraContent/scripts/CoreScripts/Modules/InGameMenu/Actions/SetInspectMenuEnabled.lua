local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_INSPECT_MENU_ENABLED", function(enabled)
	return {
		enabled = enabled,
	}
end)
