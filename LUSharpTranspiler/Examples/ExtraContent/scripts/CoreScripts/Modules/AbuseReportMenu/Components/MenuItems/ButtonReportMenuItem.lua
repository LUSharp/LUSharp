local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)

local ReportMenuItem = require(root.Components.MenuItems.ReportMenuItem)

type Props = {
	label: string,
	button: any,
	layoutOrder: number,
	isSmallPortraitViewport: boolean,
}

local ReportSubmissionButton = function(props: any)
	return React.createElement(ReportMenuItem, {
		label = props.label,
		layoutOrder = props.layoutOrder,
		rightComponent = props.button,
		menuContainerWidth = props.menuContainerWidth,
		isSmallPortraitViewport = props.isSmallPortraitViewport,
	})
end

return ReportSubmissionButton
