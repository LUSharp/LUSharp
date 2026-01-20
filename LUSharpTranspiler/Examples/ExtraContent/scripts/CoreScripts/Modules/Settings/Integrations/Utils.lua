--[[
	Utilites used by Integrations in In-Experience Menu
]]

local CorePackages = game:GetService("CorePackages")
local RobloxAppEnums = require(CorePackages.Workspace.Packages.RobloxAppEnums)
local AppCommonLib = require(CorePackages.Workspace.Packages.AppCommonLib)

export type DeviceOrientationMode = RobloxAppEnums.DeviceOrientationMode
local DeviceOrientationMode = RobloxAppEnums.DeviceOrientationMode

export type DeviceType = RobloxAppEnums.DeviceType

local function getViewportSize()
	local viewportSize = (workspace.CurrentCamera :: Camera).ViewportSize
	return viewportSize
end

local function getDeviceOrientation(): DeviceOrientationMode
	local viewportSize = getViewportSize()
	return if viewportSize.X > viewportSize.Y
		then DeviceOrientationMode.Landscape
		else DeviceOrientationMode.Portrait
end

local function getDeviceType() : DeviceType
	return AppCommonLib.getDeviceType(getDeviceOrientation())
end

return {
	getDeviceType = getDeviceType,
	DeviceTypes = RobloxAppEnums.DeviceType,
	getDeviceOrientation = getDeviceOrientation,
	getViewportSize = getViewportSize,
}
