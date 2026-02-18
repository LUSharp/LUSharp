local root = script:FindFirstAncestor("AbuseReportMenu")

local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)

local Constants = require(root.Components.Constants)
local useHideForScreenshot = require(root.Hooks.useHideForScreenshot)
local useCaptureScreenshot = require(root.Hooks.useCaptureScreenshot)
local reportAnythingReducer = require(root.Reducers.reportAnythingReducer)
local TnSIXPWrapper = require(root.IXP.TnSIXPWrapper)

return function(isReportTabVisible, hideReportTab, showReportTab, onUserInitiatedHide)
	local raAvatarEnabled = TnSIXPWrapper.getReportAnythingAvatarEnabled()
	local raExperienceEnabled = TnSIXPWrapper.getReportAnythingExperienceEnabled()

	local reportAnythingState, reportAnythingDispatch =
		React.useReducer(reportAnythingReducer, Constants.ReportAnythingInitialState)

	local shouldCaptureScreenshot = (raAvatarEnabled or raExperienceEnabled)
		-- don't capture the screenshot again if we already have one from this menu open
		and reportAnythingState.screenshotContentId == ""

	local onScreenshotHide = useCaptureScreenshot(
		-- will be called with the named arguments when the respective results are ready
		React.useCallback(function(screenshotContentId)
			reportAnythingDispatch({
				type = Constants.ReportAnythingActions.SetScreenshotContentId,
				screenshotContentId = screenshotContentId,
			})
		end),
		React.useCallback(function(screenshotId)
			reportAnythingDispatch({
				type = Constants.ReportAnythingActions.SetScreenshotId,
				screenshotId = screenshotId,
			})
		end),
		React.useCallback(function(identificationResults)
			reportAnythingDispatch({
				type = Constants.ReportAnythingActions.SetIdentificationResults,
				identificationResults = identificationResults,
			})
		end)
	)
	local onUserInitiatedHideCurried = function()
		onUserInitiatedHide(reportAnythingState, reportAnythingDispatch)
	end
	useHideForScreenshot(
		shouldCaptureScreenshot,
		isReportTabVisible,
		hideReportTab,
		showReportTab,
		onScreenshotHide,
		onUserInitiatedHideCurried
	)

	return reportAnythingState, reportAnythingDispatch
end
