local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local Constants = require(root.Components.Constants)
local Types = require(root.Components.Types)
local Cryo = require(CorePackages.Packages.Cryo)

local AbuseReasonMenuConfig: Types.DropdownMenuItemType = {
	componentType = "dropdown",
	getIsVisible = function(menuUIState: any)
		return true
	end,
	getMenuItems = function(menuUIState: any)
		local menuItemSource = if menuUIState.methodOfAbuse == "Voice Chat"
			then Constants.PlayerVoiceAbuseTypes
			else Constants.PlayerAbuseTypes
		return Cryo.Dictionary.values(menuItemSource)
	end,
	onUpdateSelectedOption = function(
		newValue: any,
		menuUIState: any,
		dispatchUIStates: any,
		utilityProps: Types.MenuUtilityProps
	)
		utilityProps.analyticsDispatch({ type = Constants.AnalyticsActions.IncrementReasonChanged })
		utilityProps.analyticsDispatch({
			type = Constants.AnalyticsActions.SetReasonSelection,
			selection = newValue,
		})
		dispatchUIStates({ type = Constants.PlayerMenuActions.UpdateAbuseReason, abuseReason = newValue })
	end,
	fieldLabel = "ReasonForAbuse",
	componentName = "AbuseReason",
}

return AbuseReasonMenuConfig
