local root = script:FindFirstAncestor("AbuseReportMenu")
local Types = require(root.Components.Types)
local Constants = require(root.Components.Constants)

local FreeFormCommentMenuConfig: Types.FreeCommentsMenuItemType = {
	componentType = "freeComments",
	getIsVisible = function(menuUIState: any)
		return true
	end,
	onUpdate = function(
		newValue: string,
		menuUIState: any,
		dispatchUIStates: any,
		utilityProps: Types.MenuUtilityProps
	)
		utilityProps.analyticsDispatch({
			type = Constants.AnalyticsActions.SetCommentAdded,
			commentAdded = #newValue > 0,
		})
		dispatchUIStates({ type = Constants.PlayerMenuActions.UpdateComment, comment = newValue })
	end,
	componentName = "FreeComments",
}

return FreeFormCommentMenuConfig
