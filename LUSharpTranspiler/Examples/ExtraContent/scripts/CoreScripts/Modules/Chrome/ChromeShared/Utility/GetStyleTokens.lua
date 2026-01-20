local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")
local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local UIBlox = require(CorePackages.Packages.UIBlox)
local UIBloxStyleTokens = UIBlox.App.Style.Tokens
local UIBloxStyleConstants = UIBlox.App.Style.Constants
local DeviceType = UIBloxStyleConstants.DeviceType
local TenFootInterface = require(RobloxGui.Modules.TenFootInterface)

return function(defaultDeviceType, theme)
	local deviceType = if TenFootInterface:IsEnabled()
		then DeviceType.Console
		else defaultDeviceType or DeviceType.DefaultDeviceType
	return UIBloxStyleTokens.getFoundationTokens(deviceType, theme or UIBloxStyleConstants.DefaultThemeName)
end
