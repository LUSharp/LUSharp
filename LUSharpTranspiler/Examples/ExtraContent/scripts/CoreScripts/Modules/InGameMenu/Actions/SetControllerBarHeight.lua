local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_CONTROLLER_BAR_HEIGHT", function(controllerBarHeight)
	return {
		controllerBarHeight = controllerBarHeight,
	}
end)
