local EventIngestService = game:GetService("EventIngestService")

local CorePackages = game:GetService("CorePackages")

local Cryo = require(CorePackages.Packages.Cryo)
local AvatarIdentificationPkg = require(CorePackages.Workspace.Packages.TnSAvatarIdentification)
local EventIngest = require(CorePackages.Workspace.Packages.Analytics).AnalyticsReporters.EventIngest

type AvatarIDStats = AvatarIdentificationPkg.AvatarIDStats

export type ReportAnythingAnalyticsState = {
	previewBackCount: number,
	previewAttachCount: number,
	retakeCount: number,
	retakeAbandon: boolean,
	highlightSceneCount: number,
	annotationBackCount: number,
	annotationPlaceCount: number,
	annotationUndoCount: number,
	annotationRedoCount: number,
	annotationNext: boolean,
}

local Constants = {
	EventType = {
		AvatarsIdentified = "ReportAnythingAvatarsIdentified",
	},
	Context = {
		LegacyMenu = "ReportAbuseLegacyMenu",
	},
}

local createZeroAccumulatedParameters = function(): ReportAnythingAnalyticsState
	return {
		previewBackCount = 0,
		previewAttachCount = 0,
		retakeCount = 0,
		retakeAbandon = false,
		highlightSceneCount = 0,
		annotationBackCount = 0,
		annotationPlaceCount = 0,
		annotationUndoCount = 0,
		annotationRedoCount = 0,
		annotationNext = false,
	}
end

local accumulatedParameters: ReportAnythingAnalyticsState = createZeroAccumulatedParameters()

local _eventIngest = nil
local getEventIngest = function()
	if not _eventIngest then
		_eventIngest = EventIngest.new(EventIngestService)
	end
	return _eventIngest
end

return {
	incrementPreviewBack = function()
		accumulatedParameters.previewBackCount += 1
	end,
	incrementPreviewAttach = function()
		accumulatedParameters.previewAttachCount += 1
	end,
	incrementRetake = function()
		accumulatedParameters.retakeCount += 1
	end,
	setRetakeAbandon = function()
		accumulatedParameters.retakeAbandon = true
	end,
	incrementHighlightScene = function()
		accumulatedParameters.highlightSceneCount += 1
	end,
	incrementAnnotationBack = function()
		accumulatedParameters.annotationBackCount += 1
	end,
	incrementAnnotationPlace = function()
		accumulatedParameters.annotationPlaceCount += 1
	end,
	incrementAnnotationUndo = function()
		accumulatedParameters.annotationUndoCount += 1
	end,
	incrementAnnotationRedo = function()
		accumulatedParameters.annotationRedoCount += 1
	end,
	setAnnotationNext = function()
		accumulatedParameters.annotationNext = true
	end,

	getAccumulatedParameters = function(): ReportAnythingAnalyticsState
		return accumulatedParameters
	end,
	emitAvatarsIdentifiedStats = function(avatarIDStats: AvatarIDStats)
		local statsWithMicroseconds = Cryo.Dictionary.join(avatarIDStats, {
			duration = avatarIDStats.duration * 1e6,
		})
		getEventIngest():sendEventDeferred(
			Constants.Context.LegacyMenu,
			Constants.EventType.AvatarsIdentified,
			statsWithMicroseconds
		)
	end,
	clear = function()
		accumulatedParameters = createZeroAccumulatedParameters()
	end,
}
