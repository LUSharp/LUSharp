local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(reportType, targetPlayer, reportCategory)
	return {
		reportType = reportType,
		targetPlayer = targetPlayer,
		reportCategory = reportCategory,
	}
end)
