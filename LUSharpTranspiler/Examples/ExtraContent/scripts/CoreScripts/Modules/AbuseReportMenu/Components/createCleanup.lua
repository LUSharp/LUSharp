local root = script:FindFirstAncestor("AbuseReportMenu")
local Types = require(root.Components.Types)
local CorePackages = game:GetService("CorePackages")
local Constants = require(root.Components.Constants)
local ReportTypes = Constants.ReportTypes

local EventIngestService = game:GetService("EventIngestService")
local AnalyticsService = game:GetService("RbxAnalyticsService")

local ReportAnythingAnalytics = require(root.ReportAnything.Utility.ReportAnythingAnalytics)
local EventIngest = require(CorePackages.Workspace.Packages.Analytics).AnalyticsReporters.EventIngest
local Analytics = require(CorePackages.Workspace.Packages.Analytics).Analytics.new(AnalyticsService)
local ReportAbuseAnalytics = require(root.Analytics.ReportAbuseAnalytics)
local collectAnalyticsParameters = require(root.Analytics.collectAnalyticsParameters)

local reportAbuseAnalytics = ReportAbuseAnalytics.new(
	EventIngest.new(EventIngestService),
	Analytics.Diag,
	ReportAbuseAnalytics.MenuContexts.NewMenu
)

local createCleanup = function(
	analyticsState: Types.AnalyticsState,
	analyticsDispatch: (action: any) -> (),
	setPreselectedPlayer: (nil) -> (),
	reportType: string
)
	return function(raState, raDispatch)
		if not analyticsState.menuEverShown then
			return
		end
		local analyticsParameters = nil
		if reportType == ReportTypes.Person then
			analyticsParameters = collectAnalyticsParameters.forPersonReport(
				analyticsState,
				raState,
				ReportAnythingAnalytics.getAccumulatedParameters()
			)
		else
			analyticsParameters = collectAnalyticsParameters.forExperienceReport(
				analyticsState,
				raState,
				ReportAnythingAnalytics.getAccumulatedParameters()
			)
		end

		reportAbuseAnalytics:reportInGameAbuseReportInteraction(analyticsParameters)

		raDispatch({
			type = Constants.ReportAnythingActions.ClearAnnotationFlowProperties,
		})
		analyticsDispatch({ type = Constants.AnalyticsActions.Reset })
		ReportAnythingAnalytics.clear()
		setPreselectedPlayer(nil)
		reportAbuseAnalytics:endAbuseReportSession()
	end
end

return createCleanup
