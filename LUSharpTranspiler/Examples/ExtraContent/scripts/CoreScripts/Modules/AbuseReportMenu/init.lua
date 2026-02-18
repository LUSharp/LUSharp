local AbuseReportMenu = require(script.Components.AbuseReportMenuNew)
local ReportAbuseAnalytics = require(script.Analytics.ReportAbuseAnalytics)

return {
	AbuseReportMenu = AbuseReportMenu,
	ReportAbuseAnalytics = ReportAbuseAnalytics,
	["jest.config"] = script["jest.config"],
}
