--[[
	Action that sets the onboarding modal status, whether it is visible or not
]]
--
local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(newShowOnboardingModalStatus)
	return {
		onboardingModalVisible = newShowOnboardingModalStatus,
	}
end)
