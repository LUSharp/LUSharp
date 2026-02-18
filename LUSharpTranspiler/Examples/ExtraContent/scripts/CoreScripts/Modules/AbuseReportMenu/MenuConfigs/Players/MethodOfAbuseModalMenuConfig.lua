--!nonstrict
local root = script:FindFirstAncestor("AbuseReportMenu")
local CoreGui = game:GetService("CoreGui")
local Types = require(root.Components.Types)
local CorePackages = game:GetService("CorePackages")
local Constants = require(root.Components.Constants)
local getAvailableMethodsOfAbuse = require(root.Utility.getAvailableMethodsOfAbuse)
local TnSIXPWrapper = require(root.IXP.TnSIXPWrapper)
local RobloxGui = CoreGui.RobloxGui
local VoiceChatServiceManager = require(RobloxGui.Modules.VoiceChat.VoiceChatServiceManager).default
local Cryo = require(CorePackages.Packages.Cryo)

local MethodOfAbuseMenuConfig: Types.ModalSelectorMenuItemType = {
	componentType = "modalSelector",
	getIsVisible = function(menuUIState)
		return menuUIState.isVoiceEnabled or TnSIXPWrapper.getReportAnythingAvatarEnabled()
	end,
	getMenuItems = function(menuUIState: Types.ReportPersonState)
		local shouldIncludeVoice = menuUIState.isVoiceEnabled
			and not Cryo.isEmpty(VoiceChatServiceManager:getRecentUsersInteractionData())

		return getAvailableMethodsOfAbuse(shouldIncludeVoice, TnSIXPWrapper.getReportAnythingAvatarEnabled())
	end,
	getSelectedValue = function(menuUIState: any)
		return menuUIState.methodOfAbuse
	end,
	onUpdateSelectedOption = function(
		selectedItem: Types.ModalSelectorMenuItemCellDataType,
		menuUIState: Types.ReportPersonState,
		dispatchUIStates: any,
		utilityProps: Types.MenuUtilityProps
	)
		dispatchUIStates({
			type = Constants.PlayerMenuActions.UpdateMethodOfAbuse,
			methodOfAbuse = selectedItem.identifier,
		})
		if selectedItem.identifier ~= menuUIState.methodOfAbuse then
			utilityProps.reportAnythingDispatch({
				type = Constants.ReportAnythingActions.ClearAnnotationFlowProperties,
			})
			utilityProps.analyticsDispatch({ type = Constants.AnalyticsActions.IncrementTypeofabuseChanged })
			utilityProps.analyticsDispatch({
				type = Constants.AnalyticsActions.SetTypeOfAbuseSelection,
				selection = selectedItem.identifier,
			})
		end
	end,
	fieldLabel = "MethodOfAbuse",
	componentName = "MethodOfAbuse",
}

return MethodOfAbuseMenuConfig
