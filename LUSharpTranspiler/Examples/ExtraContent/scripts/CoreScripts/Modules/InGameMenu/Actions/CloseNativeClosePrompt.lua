local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("CLOSE_NATIVE_PROMPT", function()
	return {}
end)
