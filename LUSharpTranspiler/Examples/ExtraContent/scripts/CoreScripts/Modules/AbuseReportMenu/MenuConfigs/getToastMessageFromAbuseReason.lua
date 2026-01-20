local root = script:FindFirstAncestor("AbuseReportMenu")
local Constants = require(root.Components.Constants)
local ReportSuccessToast = Constants.ReportSuccessToast
local PlayerAbuseTypes = Constants.PlayerAbuseTypes

local function getToastMessageFromAbuseReason(abuseReason: string, isReportAnything: boolean?)
	-- default roast message
	local reportSuccessMessage = ReportSuccessToast.ChatGeneric

	-- abuse reason specific roast messages
	if abuseReason == PlayerAbuseTypes.Cheating then
		reportSuccessMessage = ReportSuccessToast.Cheating
	elseif abuseReason == PlayerAbuseTypes.Username then
		reportSuccessMessage = ReportSuccessToast.BadUserName
	elseif abuseReason == PlayerAbuseTypes.Links or abuseReason == Constants.InappropriateContentAbuseReason then
		reportSuccessMessage = ReportSuccessToast.Place
	end
	-- TODO add Report Anything logic here when integrated
	if isReportAnything then
		reportSuccessMessage = ReportSuccessToast.ReportAnythingGeneric
	end

	return reportSuccessMessage
end

return getToastMessageFromAbuseReason
