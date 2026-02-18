local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("CANCEL_LEAVING_GAME", function()
	return {}
end)
