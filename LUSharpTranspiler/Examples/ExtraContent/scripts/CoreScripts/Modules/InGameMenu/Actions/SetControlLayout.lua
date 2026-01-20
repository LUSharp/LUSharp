local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_CONTROL_LAYOUT", function(controlLayout)
	return {
		controlLayout = controlLayout,
	}
end)
