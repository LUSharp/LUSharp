local root = script:FindFirstAncestor("AbuseReportMenu")
local Constants = require(root.Components.Constants)
local Types = require(root.Components.Types)
local PlayerMenuActions = Constants.PlayerMenuActions

local function reportExperienceUIStateReducer(state: Types.ReportExperienceState, action: any)
	local newState = table.clone(state)
	local type = action.type
	if type == PlayerMenuActions.UpdateComment then
		newState.comment = action.comment
	end
	return newState
end

return reportExperienceUIStateReducer
