local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_RESPAWN_BEHAVIOR", function(respawnEnabled, respawnCallback)
	return {
		respawnEnabled = respawnEnabled,
		customCallback = respawnCallback,
	}
end)
