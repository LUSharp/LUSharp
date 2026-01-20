local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(reportType, targetPlayer)
	return {
		reportType = reportType,
		targetPlayer = targetPlayer,
	}
end)
