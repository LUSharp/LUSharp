-- Action to open the ValidationErrorModal that appears over a prompt if validation Error occurred
local CorePackages = game:GetService("CorePackages")
local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(errorMessage)
	return {
		errorMessage = errorMessage,
	}
end)
