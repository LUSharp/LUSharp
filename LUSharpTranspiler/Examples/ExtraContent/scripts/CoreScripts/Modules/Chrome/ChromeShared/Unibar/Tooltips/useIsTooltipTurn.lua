local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)

local TooltipContext = require(script.Parent.TooltipContext)
local TooltipProvider = require(script.Parent.TooltipProvider)

type TooltipContextType = TooltipProvider.ContextType

function useIsTooltipTurn(id: string)
	local tooltipQueue = React.useContext(TooltipContext) :: TooltipContextType?

	return tooltipQueue and tooltipQueue.isCurrentTooltip(id)
end

return useIsTooltipTurn
