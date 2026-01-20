local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)
local Types = require(root.Components.Types)
local AvatarIdentificationPkg = require(CorePackages.Workspace.Packages.TnSAvatarIdentification)
local AdIdentificationPkg = require(CorePackages.Workspace.Packages.TnSAdIdentification)

local ScreenshotManager = require(CorePackages.Workspace.Packages.TnSScreenshot).ScreenshotManager

local AvatarIdentification = AvatarIdentificationPkg.AvatarIdentification
local AdIdentification = AdIdentificationPkg.AdIdentification

-- Returns a function that, when called, captures a screenshot
-- and runs identification of avatars and ads.
-- The results of the capture and computation are
-- given to the callbacks provided as arguments to this hook.

local useCaptureScreenshot = function(
	onScreenshotContentIdReady: (string) -> (),
	onScreenshotIdReady: (string) -> (),
	onIdentificationCompleted: (Types.IdentificationResults) -> ()
)
	return React.useCallback(function()
		ScreenshotManager:TakeScreenshotWithCallback(onScreenshotIdReady, onScreenshotContentIdReady)
		local identifiedAvatars, avatarIDStats = AvatarIdentification.getVisibleAvatars()
		local identifiedAds, adIDStats = AdIdentification.getVisibleAds()

		onIdentificationCompleted({
			identifiedAvatars = identifiedAvatars,
			identifiedAds = identifiedAds,
			avatarIDStats = avatarIDStats,
			adIDStats = adIDStats,
		})
	end, { onScreenshotIdReady, onScreenshotContentIdReady, onIdentificationCompleted } :: { any })
end

return useCaptureScreenshot
