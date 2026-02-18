local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local Constants = require(root.Components.Constants)
local Types = require(root.Components.Types)
local Cryo = require(CorePackages.Packages.Cryo)

local AbuseReasonModalSelectorMenuConfig: Types.ModalSelectorMenuItemType = {
	componentType = "modalSelector",
	getIsVisible = function(menuUIState: any)
		return true
	end,
	getMenuItems = function(menuUIState: any, localizedText: any)
		local menuItemSource = if menuUIState.methodOfAbuse == Constants.AbuseMethods.VoiceChat
			then Constants.PlayerVoiceAbuseTypes
			else Constants.PlayerAbuseTypes
		local menuItemLabels = Cryo.Dictionary.values(menuItemSource)
		return Cryo.List.map(menuItemLabels, function(label)
			return {
				label = localizedText[label],
				identifier = label,
			}
		end)
	end,
	getSelectedValue = function(menuUIState: any)
		return menuUIState.abuseReason
	end,
	onUpdateSelectedOption = function(
		selectedItem: Types.ModalSelectorMenuItemCellDataType,
		menuUIState: Types.ReportPersonState,
		dispatchUIStates: any,
		utilityProps: Types.MenuUtilityProps
	)
		utilityProps.analyticsDispatch({ type = Constants.AnalyticsActions.IncrementReasonChanged })
		utilityProps.analyticsDispatch({
			type = Constants.AnalyticsActions.SetReasonSelection,
			selection = selectedItem.identifier,
		})
		dispatchUIStates({ type = Constants.PlayerMenuActions.UpdateAbuseReason, abuseReason = selectedItem.identifier })
	end,
	fieldLabel = "ReasonForAbuse",
	componentName = "AbuseReason",
}

return AbuseReasonModalSelectorMenuConfig
