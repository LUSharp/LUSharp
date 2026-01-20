local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("OPEN_REPORT_DIALOG", function(userId, userName)
	return {
		userId = userId,
		userName = userName,
	}
end)
