local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)

local ReportTextEntry = require(root.Components.ReportTextEntry)
local ReportMenuItem = require(root.Components.MenuItems.ReportMenuItem)
local Constants = require(root.Components.Constants)

type Props = {
	layoutOrder: number,
	comment: string,
	placeholderText: string,
	menuContainerWidth: number,
	onUpdate: (newValue: string) -> (),
	isSmallPortraitViewport: boolean,
}

local function FreeCommentsMenuItem(props: Props)
	return React.createElement(ReportMenuItem, {
		label = "Abuse Description",
		isRightComponentFullWidth = true,
		layoutOrder = props.layoutOrder,
		rightComponent = React.createElement(ReportTextEntry, {
			layoutOrder = props.layoutOrder,
			text = props.comment,
			placeholderText = props.placeholderText,
			onUpdate = props.onUpdate,
		}),
		heightOverride = Constants.FreeCommentsMenuItemHeight,
		menuContainerWidth = props.menuContainerWidth,
		isSmallPortraitViewport = props.isSmallPortraitViewport,
	})
end

return FreeCommentsMenuItem
