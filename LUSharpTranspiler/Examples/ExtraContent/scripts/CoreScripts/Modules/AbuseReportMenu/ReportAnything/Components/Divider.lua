local CorePackages = game:GetService("CorePackages")

local UIBlox = require(CorePackages.Packages.UIBlox)
local React = require(CorePackages.Packages.React)
local Cryo = require(CorePackages.Packages.Cryo)

local withStyle = UIBlox.Core.Style.withStyle

export type Props = {
	Size: UDim2?,
	Position: UDim2?,
	AnchorPoint: Vector2?,
	LayoutOrder: number?,
	Visible: boolean?,
}

local function Divider(props: Props)
	return withStyle(function(style)
		-- Divider relies on being right-aligned in order to be left-indented.
		return React.createElement(
			"Frame",
			Cryo.Dictionary.join(
				{
					Size = UDim2.new(1, 0, 0, 1),
				},
				props,
				{
					BorderSizePixel = 0,
					BackgroundColor3 = style.Theme.Divider.Color,
					BackgroundTransparency = style.Theme.Divider.Transparency,
				}
			)
		)
	end)
end

return Divider
