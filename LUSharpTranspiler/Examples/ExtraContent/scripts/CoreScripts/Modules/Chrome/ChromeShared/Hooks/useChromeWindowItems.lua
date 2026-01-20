local Root = script:FindFirstAncestor("ChromeShared")

local ChromeService = require(Root.Service)
local ChromeTypes = require(Root.Service.Types)
local useObservableValue = require(Root.Hooks.useObservableValue)

return function()
	return (useObservableValue(ChromeService:windowList()) or {}) :: ChromeTypes.WindowList
end
