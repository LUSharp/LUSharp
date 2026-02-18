local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("OPEN_NATIVE_CLOSE_PROMPT", function()
	return {}
end)
