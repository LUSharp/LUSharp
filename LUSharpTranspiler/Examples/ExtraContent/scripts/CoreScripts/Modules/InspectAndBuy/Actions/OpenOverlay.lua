local CorePackages = game:GetService("CorePackages")
local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(overlay, overlayProps)
	return {
		overlay = overlay,
		overlayProps = overlayProps,
	}
end)
