local root = script:FindFirstAncestor("AbuseReportMenu")
local Types = require(root.Components.Types)
local Constants = require(root.Components.Constants)

local Actions = Constants.ReportAnythingActions

local function reportAnythingReducer(state: Types.ReportAnythingState, action: any)
	local newState: Types.ReportAnythingState = table.clone(state)
	local actionType = action.type

	if actionType == Actions.ClearAll then
		newState = Constants.ReportAnythingInitialState
	elseif actionType == Actions.ClearAnnotationFlowProperties then
		newState.annotationPoints = {}
		newState.attachCompleted = false
		newState.annotationPageSeen = false
		newState.annotationOptionSeen = false
	elseif actionType == Actions.SetScreenshotId then
		newState.screenshotId = action.screenshotId
	elseif actionType == Actions.SetScreenshotContentId then
		newState.screenshotContentId = action.screenshotContentId
	elseif actionType == Actions.SetAnnotationPoints then
		newState.annotationPoints = action.annotationPoints
		newState.annotationPageSeen = true
		newState.annotationOptionSeen = true
	elseif actionType == Actions.SetIdentificationResults then
		newState.identificationResults = action.identificationResults
	elseif actionType == Actions.CompleteAttachment then
		newState.attachCompleted = true
	elseif actionType == Actions.SetAnnotationCircleRadius then
		newState.annotationCircleRadius = action.radius
	elseif actionType == Actions.SetAnnotationAreaDimensions then
		newState.annotationAreaWidth = action.width
		newState.annotationAreaHeight = action.height
	elseif actionType == Actions.SetAnnotationOptionSeen then
		newState.annotationOptionSeen = true
	end

	return newState
end

return reportAnythingReducer
