local CorePackages = game:GetService("CorePackages")
local React = require(CorePackages.Packages.React)

local TooltipContext = React.createContext(nil)

return TooltipContext
