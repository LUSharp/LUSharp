--[[
	Action that sets the help modal status, whether it is visible or not
]]
--
local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(newShowHelpModalStatus)
	return {
		helpModalVisible = newShowHelpModalStatus,
	}
end)
