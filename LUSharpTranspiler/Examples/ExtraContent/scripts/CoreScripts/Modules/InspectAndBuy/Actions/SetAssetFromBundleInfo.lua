local CorePackages = game:GetService("CorePackages")
local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(assetId, bundleInfo)
	return {
		assetId = assetId,
		bundleInfo = bundleInfo,
	}
end)
