--!nonstrict
local CorePackages = game:GetService("CorePackages")

local TrustAndSafetyIXPManager =
	require(CorePackages.Workspace.Packages.TrustAndSafetyIxp).TrustAndSafetyIXPManager.default

-- Wrapper to contain the external dependency on RobloxGui
return {
	getReportAnythingAvatarEnabled = function()
		return TrustAndSafetyIXPManager:getReportAnythingAvatarEnabled()
	end,
	getReportAnythingExperienceEnabled = function()
		return TrustAndSafetyIXPManager:getReportAnythingExperienceEnabled()
	end,
}
