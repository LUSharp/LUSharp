local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_LOCALE_ID", function(localeId)
	return {
		newLocaleId = localeId,
	}
end)
