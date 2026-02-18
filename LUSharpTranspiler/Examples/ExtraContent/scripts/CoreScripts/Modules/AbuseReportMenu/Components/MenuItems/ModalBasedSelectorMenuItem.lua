local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)
local ModalBasedSelector = require(root.Components.ModalBasedSelector)
local ReportMenuItem = require(root.Components.MenuItems.ReportMenuItem)

type Props = {
	label: string,
	layoutOrder: number,
	selectorHeight: number,
	isSmallPortraitViewport: boolean,
	placeholderText: string,
}

local ModalBasedSelectorMenuItem = function(props: any)
	return React.createElement(ReportMenuItem, {
		label = props.label,
		layoutOrder = props.layoutOrder,
		rightComponent = React.createElement(ModalBasedSelector, props),
		menuContainerWidth = props.menuContainerWidth,
		isSmallPortraitViewport = props.isSmallPortraitViewport,
		placeholderText = props.placeholderText,
	})
end

return ModalBasedSelectorMenuItem
