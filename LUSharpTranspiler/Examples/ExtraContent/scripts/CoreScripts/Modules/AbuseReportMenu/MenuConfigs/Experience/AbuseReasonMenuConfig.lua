local root = script:FindFirstAncestor("AbuseReportMenu")
local Types = require(root.Components.Types)
local Constants = require(root.Components.Constants)

local AbuseReasonMenuConfig: Types.DropdownMenuItemType = {
	componentType = "dropdown",
	getIsVisible = function(menuUIState: any)
		return true
	end,
	getMenuItems = function(menuUIState: any)
		return {}
	end,
	onUpdateSelectedOption = function(menuUIState: any, dispatchUIStates: any) end,
	readonlyValue = Constants.InappropriateContentAbuseReason,
	fieldLabel = "ReasonForAbuse",
	componentName = "AbuseReason",
}

return AbuseReasonMenuConfig
