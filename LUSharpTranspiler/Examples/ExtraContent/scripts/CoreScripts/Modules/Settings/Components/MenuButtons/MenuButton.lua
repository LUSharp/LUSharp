local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")

local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local Utility = require(RobloxGui.Modules.Settings.Utility)
local React = require(CorePackages.Packages.React)
local Foundation = require(CorePackages.Packages.Foundation)
local Responsive = require(CorePackages.Workspace.Packages.Responsive)

local useTokens = Foundation.Hooks.useTokens
local View = Foundation.View
local Text = Foundation.Text
local Image = Foundation.Image

local FIntRelocateMobileMenuButtonsVariant = require(RobloxGui.Modules.Settings.Flags.FIntRelocateMobileMenuButtonsVariant)

export type ButtonData = {
	name: string,
	text: string,
	hint: {
		keyboard: string,
		gamepadButton: Enum.KeyCode,
		gamepadButtonImage: React.Binding<string>,
		setGamepadButtonImage: (string) -> (),
	},
	isEmphasized: boolean,
	getIsDisabled: () -> boolean,
	onActivated: () -> (),
	hotkeys: { Enum.KeyCode },
	hotkeyFunc: ((...any) -> ...any),
}

local getDisabledTransparency = function(transparency: number)
	return transparency + (1 - transparency) * 0.5
end

local function KeyLabelIcon(key: string, isDisabled: boolean)
	local tokens = useTokens()

	return React.createElement(View, {
		LayoutOrder = 1,
		backgroundStyle = {
			Color3 = tokens.Color.ActionStandard.Background.Color3,
			Transparency = if isDisabled then getDisabledTransparency(tokens.Color.ActionStandard.Background.Transparency) else tokens.Color.ActionStandard.Background.Transparency,
		},
		tag = "size-1000-700 row align-x-center align-y-center radius-medium",
	}, {
		HotkeyText = React.createElement(Text, {
			Text = key,
			textStyle = {
				Color3 = tokens.Color.ActionStandard.Foreground.Color3,
				Transparency = if isDisabled then getDisabledTransparency(tokens.Color.ActionStandard.Foreground.Transparency) else tokens.Color.ActionStandard.Foreground.Transparency,
			},
			tag = "text-title-small",
		}),
	})
end

type Props = {
	text: string,
	lastInput: string,
	keyboardHint: string,
	gamepadButtonImageHint: React.Binding<string>,
	onActivated: () -> (),
	layoutOrder: number,
	isEmphasized: boolean,
	isSmall: boolean,
	isDisabled: boolean,
}

local function MenuButton(props: Props)
	local tokens = useTokens()

	local backgroundStyle = if props.isEmphasized then tokens.Color.ActionSoftEmphasis.Background else tokens.Color.ActionStandard.Background
	local foregroundStyle = if props.isEmphasized then tokens.Color.ActionSoftEmphasis.Foreground else tokens.Color.ActionStandard.Foreground

	return React.createElement(View, {
		onActivated = props.onActivated,
		isDisabled = props.isDisabled,
		LayoutOrder = props.layoutOrder,
		backgroundStyle = {
			Color3 = backgroundStyle.Color3,
			Transparency = if props.isDisabled then getDisabledTransparency(backgroundStyle.Transparency) else backgroundStyle.Transparency,
		},
		tag = {
			["fill auto-x row align-y-center align-x-center gap-small radius-medium"] = true,
			["size-0-1200"] = not props.isSmall,
			["size-0-1000"] = props.isSmall,
			["bg-action-standard"] = not props.isEmphasized,
			["bg-action-soft-emphasis"] = props.isEmphasized,
		},
	}, {
		Hint = if FIntRelocateMobileMenuButtonsVariant == 2 and Utility:IsSmallTouchScreen() then nil
			elseif props.lastInput == Responsive.Input.Pointer then KeyLabelIcon(props.keyboardHint, props.isDisabled)
			elseif props.lastInput == Responsive.Input.Directional then React.createElement(Image, {
				Image = props.gamepadButtonImageHint,
				imageStyle = {
					Color3 = foregroundStyle.Color3,
					Transparency = if props.isDisabled then 0.5 else foregroundStyle.Transparency,
				},
				tag = {
					["size-600"] = not props.isSmall,
					["size-500"] = props.isSmall,
				},
			})
			else nil,
		ButtonText = React.createElement(Text, {
			Text = props.text,
			LayoutOrder = 2,
			textStyle = {
				Color3 = foregroundStyle.Color3,
				Transparency = if props.isDisabled then 0.5 else foregroundStyle.Transparency,
			},
			tag = {
				["auto-x"] = true,
				["text-title-medium"] = not props.isSmall,
				["text-title-small"] = props.isSmall,
				["content-action-standard"] = not props.isEmphasized,
				["content-action-soft-emphasis"] = props.isEmphasized,
			},
		}),
	})
end

return React.memo(MenuButton)
