local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_MORE_MENU_OPEN", function(isMainPageMoreMenuOpen)
	return {
		isMainPageMoreMenuOpen = isMainPageMoreMenuOpen,
	}
end)
