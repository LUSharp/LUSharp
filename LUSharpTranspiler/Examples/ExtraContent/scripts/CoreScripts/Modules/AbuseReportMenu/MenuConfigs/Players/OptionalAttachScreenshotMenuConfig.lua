local root = script:FindFirstAncestor("AbuseReportMenu")

local Types = require(root.Components.Types)
local Constants = require(root.Components.Constants)

local TnSIXPWrapper = require(root.IXP.TnSIXPWrapper)
local AnnotationModal = require(root.ReportAnything.Components.AnnotationModal)

local OptionalAttachScreenshotMenuConfig: Types.ButtonMenuItemType = {
	componentType = "button",
	getIsVisible = function(menuUIState: Types.ReportPersonState)
		return TnSIXPWrapper.getReportAnythingAvatarEnabled()
			and (
				menuUIState.methodOfAbuse == Constants.AbuseMethods.Other
				or menuUIState.methodOfAbuse == Constants.AbuseMethods.Avatar
			)
	end,
	getIsDisabled = function(menuUIState: Types.ReportPersonState, utilityProps: Types.MenuUtilityProps)
		return utilityProps.reportAnythingState.attachCompleted
	end,
	variant = "secondary",
	getIconSrc = function(utilityProps: Types.MenuUtilityProps)
		return if utilityProps.reportAnythingState.attachCompleted
			then "icons/actions/accept"
			else "icons/controls/screenshot"
	end,
	getButtonLabel = function(utilityProps: Types.MenuUtilityProps)
		return if utilityProps.reportAnythingState.attachCompleted then "SceneCaptured" else "CaptureScene"
	end,
	onClick = function(menuUIState: any, dispatchUIStates: any, utilityProps: Types.MenuUtilityProps)
		utilityProps.analyticsDispatch({ type = Constants.AnalyticsActions.IncrementCaptureScene })
		AnnotationModal.mountAnnotationPage(
			Constants.ReportTypes.Person,
			utilityProps.hideReportTab,
			utilityProps.reportAnythingAnalytics,
			utilityProps.reportAnythingState,
			utilityProps.reportAnythingDispatch
		)
	end,
	fieldLabel = "AttachScreenshot",
	componentName = "OptionalScreenshot",
}

return OptionalAttachScreenshotMenuConfig
