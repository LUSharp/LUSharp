local CorePackages = game:GetService("CorePackages")
local Rodux = require(CorePackages.Packages.Rodux)

export type Action = {
	type: string,
}

return Rodux.makeActionCreator(script.Name, function()
	return {}
end)
