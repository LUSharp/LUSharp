local root = script:FindFirstAncestor("AbuseReportMenu")
local Types = require(root.Components.Types)
local MenuConfigs = root.MenuConfigs

local OptionalAttachScreenshotMenuConfig = require(MenuConfigs.Experience.OptionalAttachScreenshotMenuConfig)
local AbuseReasonMenuConfig = require(MenuConfigs.Experience.AbuseReasonMenuConfig)
local SubmitReportButtonMenuConfig = require(MenuConfigs.Experience.SubmitReportButtonMenuConfig)
local FreeFormCommentMenuConfig = require(MenuConfigs.Experience.FreeFormCommentMenuConfig)

-- Layout order of components will be determined by the order in this table
-- TODO this should be typed specifically
local ReportExperienceMenuItemConfigList: { Types.MenuItemConfigType } = {
	AbuseReasonMenuConfig,
	OptionalAttachScreenshotMenuConfig,
	FreeFormCommentMenuConfig,
	SubmitReportButtonMenuConfig,
}

return ReportExperienceMenuItemConfigList
