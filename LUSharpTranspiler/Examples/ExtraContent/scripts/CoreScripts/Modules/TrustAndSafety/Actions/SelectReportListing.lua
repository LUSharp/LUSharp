local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(reportType, targetPlayer, sortedUserIds)
	return {
		reportType = reportType,
		targetPlayer = targetPlayer,
		sortedUserIds = sortedUserIds,
	}
end)
