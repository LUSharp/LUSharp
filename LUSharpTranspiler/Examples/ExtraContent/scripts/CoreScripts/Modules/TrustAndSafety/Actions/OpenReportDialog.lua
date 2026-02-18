local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(reportType, targetPlayer, onGoBack)
	return {
		reportType = reportType,
		targetPlayer = targetPlayer,
		onGoBack = onGoBack,
	}
end)
