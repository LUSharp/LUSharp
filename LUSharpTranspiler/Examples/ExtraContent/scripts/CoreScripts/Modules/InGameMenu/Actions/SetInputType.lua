local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action("SET_INPUT_TYPE", function(inputType)
	return {
		inputType = inputType,
	}
end)
