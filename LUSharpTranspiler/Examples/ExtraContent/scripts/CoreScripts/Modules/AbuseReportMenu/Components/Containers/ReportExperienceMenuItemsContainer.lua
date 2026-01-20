local root = script:FindFirstAncestor("AbuseReportMenu")
local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)
local getMenuItemsFromConfigs = require(root.MenuConfigs.getMenuItemsFromConfigs)
local getMenuItemSizings = require(root.Utility.getMenuItemSizings)
local ReportExperienceMenuItemConfigList = require(root.MenuConfigs.Experience.ReportExperienceMenuItemConfigList)
local Types = require(root.Components.Types)
local Constants = require(root.Components.Constants)
local Cryo = require(CorePackages.Packages.Cryo)
local reportExperienceUIStateReducer = require(root.Reducers.reportExperienceUIStateReducer)

type Props = {
	utilityProps: Types.MenuUtilityProps,
	isSmallPortraitViewport: boolean,
}

local function ReportExperienceMenuItemsContainer(props: Props)
	local sizings = getMenuItemSizings()
	local menuUIStates, dispatchUIStates =
		React.useReducer(reportExperienceUIStateReducer, Constants.InitExperienceUIState)
	React.useEffect(function()
		if props.utilityProps.isReportTabVisible then
			props.utilityProps.analyticsDispatch({
				type = Constants.AnalyticsActions.SwitchToExperienceInitialSelections,
			})
		end
	end, { props.utilityProps.isReportTabVisible })

	local configItems = getMenuItemsFromConfigs(
		menuUIStates,
		dispatchUIStates,
		props.utilityProps,
		ReportExperienceMenuItemConfigList,
		props.isSmallPortraitViewport
	)

	local menuItems = Cryo.Dictionary.join({
		Layout = React.createElement("UIListLayout", {
			FillDirection = Enum.FillDirection.Vertical,
			SortOrder = Enum.SortOrder.LayoutOrder,
			Padding = UDim.new(0, sizings.ItemPadding),
		}),
	}, configItems)

	return React.createElement("Frame", {
		Size = UDim2.new(1, 0, 1, 0),
		BackgroundTransparency = 1,
	}, {
		MenuItems = React.createElement("Frame", {
			Size = UDim2.new(1, 0, 1, 0),
			BackgroundTransparency = 1,
		}, menuItems),
	})
end

return ReportExperienceMenuItemsContainer
