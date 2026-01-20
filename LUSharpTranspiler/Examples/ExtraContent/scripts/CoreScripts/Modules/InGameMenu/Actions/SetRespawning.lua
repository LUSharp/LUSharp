local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_RESPAWNING", function(isRespawning)
	return {
		respawning = isRespawning,
	}
end)
