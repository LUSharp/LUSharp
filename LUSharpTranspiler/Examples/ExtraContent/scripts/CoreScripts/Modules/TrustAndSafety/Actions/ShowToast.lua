local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(toastTitle, toastSubtitle)
	return {
		toastTitle = toastTitle,
		toastSubtitle = toastSubtitle,
	}
end)
