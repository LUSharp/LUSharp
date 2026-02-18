--!nolint DeprecatedApi
local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")
local ContextActionService = game:GetService("ContextActionService")
local GuiService = game:GetService("GuiService")

local InGameMenuDependencies = require(CorePackages.Packages.InGameMenuDependencies)
local Roact = InGameMenuDependencies.Roact
local UIBlox = InGameMenuDependencies.UIBlox
local t = InGameMenuDependencies.t

local BaseMenu = UIBlox.App.Menu.BaseMenu

local InGameMenu = script.Parent.Parent

local FocusHandler = require(script.Parent.Connection.FocusHandler)
local RootedConnection = require(script.Parent.Connection.RootedConnection)

local Flags = InGameMenu.Flags
local GetFFlagIGMGamepadSelectionHistory = require(Flags.GetFFlagIGMGamepadSelectionHistory)

local PLAYER_CONTEXTUAL_MENU_CLOSE_ACTION = "player_contextual_menu_close_action"
local SELECTION_PARENT_NAME = "player_contextual_menu_selection_parent"

local PlayerContextualMenu = Roact.PureComponent:extend("PlayerContextualMenu")

PlayerContextualMenu.validateProps = t.strictInterface({
	moreActions = t.table,
	actionWidth = t.number,
	xOffset = t.number,
	yOffset = t.number,
	onClose = t.callback,
	canCaptureFocus = t.boolean,
})

function PlayerContextualMenu:init()
	self.firstOptionRef = Roact.createRef()
	self.containerRef = Roact.createRef()
end

function PlayerContextualMenu:renderContextualMenuFocusHandler(isRooted, children)
	local isFocused = self.props.canCaptureFocus and isRooted

	return Roact.createElement(FocusHandler, {
		isFocused = isFocused,

		didFocus = function(previousSelection)
			ContextActionService:BindCoreAction(PLAYER_CONTEXTUAL_MENU_CLOSE_ACTION, function(actionName, inputState)
				if inputState == Enum.UserInputState.End and self.props.onClose then
					self.props.onClose()
					return Enum.ContextActionResult.Sink
				end
				return Enum.ContextActionResult.Pass
			end, false, Enum.KeyCode.ButtonB)

			-- RemoveSelectionGroup is deprecated
			GuiService:RemoveSelectionGroup(SELECTION_PARENT_NAME)
			-- AddSelectionParent is deprecated
			GuiService:AddSelectionParent(SELECTION_PARENT_NAME, self.containerRef:getValue())

			if GetFFlagIGMGamepadSelectionHistory() then
				GuiService.SelectedCoreObject = previousSelection or self.firstOptionRef:getValue()
			else
				GuiService.SelectedCoreObject = self.firstOptionRef:getValue()
			end
		end,

		didBlur = function()
			ContextActionService:UnbindCoreAction(PLAYER_CONTEXTUAL_MENU_CLOSE_ACTION)
		end,
	}, children)
end

function PlayerContextualMenu:render()
	return Roact.createElement(Roact.Portal, {
		-- LUAU FIXME: Need read-write syntax for props to obviate the need for this cast
		target = CoreGui :: Instance,
	}, {
		InGameMenuContextGui = Roact.createElement("ScreenGui", {
			DisplayOrder = 2,
			ZIndexBehavior = Enum.ZIndexBehavior.Sibling,
		}, {
			RootedConnection = Roact.createElement(RootedConnection, {
				render = function(isRooted)
					return self:renderContextualMenuFocusHandler(isRooted, {
						MoreActionsMenu = Roact.createElement("Frame", {
							Size = UDim2.fromScale(1, 1),
							BackgroundTransparency = 1,
							[Roact.Ref] = self.containerRef,
						}, {
							BaseMenu = Roact.createElement(BaseMenu, {
								buttonProps = self.props.moreActions,
								setFirstItemRef = self.firstOptionRef,
								width = UDim.new(0, self.props.actionWidth),
								position = UDim2.fromOffset(self.props.xOffset, self.props.yOffset),
							}),
						}),
					})
				end,
			})
		}),
	})
end

return PlayerContextualMenu
