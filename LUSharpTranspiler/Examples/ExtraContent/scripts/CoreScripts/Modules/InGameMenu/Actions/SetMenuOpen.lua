local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_MENU_OPEN", function(isMenuOpen)
	return {
		isMenuOpen = isMenuOpen,
	}
end)
