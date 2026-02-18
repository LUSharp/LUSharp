local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")

local Roact = require(CorePackages.Packages.Roact)
local UIBlox = require(CorePackages.Packages.UIBlox)

local BaseMenu = UIBlox.App.Menu.BaseMenu
local Images = UIBlox.App.ImageSet.Images
local useStyle = UIBlox.Core.Style.useStyle

local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local ContactList = RobloxGui.Modules.ContactList
local dependencies = require(ContactList.dependencies)
local useSelector = dependencies.Hooks.useSelector

local useAnalytics = require(ContactList.Analytics.useAnalytics)
local EventNamesEnum = require(ContactList.Analytics.EventNamesEnum)
local FriendAction = require(ContactList.Enums.FriendAction)

local useLocalization = dependencies.Hooks.useLocalization

local function ConfigureFriendMenu(props)
	local analytics = useAnalytics()
	local style = useStyle()
	local theme = style.Theme

	local combinedName = useSelector(function(state)
		if state.PlayerMenu.friend then
			return state.PlayerMenu.friend.combinedName
		else
			return ""
		end
	end)

	local friendUserId = useSelector(function(state)
		if state.PlayerMenu.friend then
			return state.PlayerMenu.friend.userId
		else
			return nil
		end
	end)

	local localized = useLocalization({
		blockLabel = {
			"Feature.Call.Label.Block",
			combinedName = combinedName,
		},
		unfriendLabel = {
			"Feature.Call.Label.Unfriend",
			combinedName = combinedName,
		},
	})

	return Roact.createElement(BaseMenu, {
		buttonProps = {
			{
				icon = Images["icons/actions/block"],
				text = localized.blockLabel,
				onActivated = function()
					analytics.fireEvent(EventNamesEnum.PhoneBookPlayerMenuBlockClicked, {
						eventTimestampMs = os.time() * 1000,
						friendUserId = friendUserId,
					})
					props.initiateConfirmation(FriendAction.Block)
				end,
			},
			{
				icon = Images["icons/actions/friends/friendRemove"],
				text = localized.unfriendLabel,
				onActivated = function()
					analytics.fireEvent(EventNamesEnum.PhoneBookPlayerMenuUnfriendClicked, {
						eventTimestampMs = os.time() * 1000,
						friendUserId = friendUserId,
					})
					props.initiateConfirmation(FriendAction.Unfriend)
				end,
			},
		},
		background = theme.BackgroundUIDefault,
		width = UDim.new(0, 352),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchorPoint = Vector2.new(0.5, 0.5),
	})
end

return ConfigureFriendMenu
