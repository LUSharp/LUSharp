local CorePackages = game:GetService("CorePackages")
local GuiService = game:GetService("GuiService")

local React = require(CorePackages.Packages.React)

return function()
	local topbarInsetHeight, setTopbarInsetHeight = React.useState(GuiService.TopbarInset.Height)

	React.useEffect(function()
		local topbarInsetPropertyChangedSignal = GuiService:GetPropertyChangedSignal("TopbarInset"):Connect(function()
			setTopbarInsetHeight(GuiService.TopbarInset.Height)
		end)

		return function()
			topbarInsetPropertyChangedSignal:Disconnect()
		end
	end, {})

	return topbarInsetHeight
end
