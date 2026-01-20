--!nonstrict

local CoreGui = game:GetService("CoreGui")
local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local CorePackages = game:GetService("CorePackages")

local isInExperienceUIVREnabled =
	require(CorePackages.Workspace.Packages.SharedExperimentDefinition).isInExperienceUIVREnabled
if isInExperienceUIVREnabled then
	return true
end

local FFlagEnableSpatialRobloxGui = require(RobloxGui.Modules.Flags.FFlagEnableSpatialRobloxGui)

if not FFlagEnableSpatialRobloxGui then
	return false
end

local IXPServiceWrapper = require(CorePackages.Workspace.Packages.IxpServiceWrapper).IXPServiceWrapper
local FStringSpatialRobloxUIIXPLayerName = require(RobloxGui.Modules.Flags.FStringSpatialRobloxUIIXPLayerName)
local FStringSpatialRobloxUIIXPUITypeVariableName =
	require(RobloxGui.Modules.Flags.FStringSpatialRobloxUIIXPUITypeVariableName)
local FStringSpatialRobloxUIIXPSpatialUIVariantValue =
	require(RobloxGui.Modules.Flags.FStringSpatialRobloxUIIXPSpatialUIVariantValue)

local layerData = IXPServiceWrapper:GetLayerData(FStringSpatialRobloxUIIXPLayerName)
local uiType = layerData[FStringSpatialRobloxUIIXPUITypeVariableName]

return uiType == FStringSpatialRobloxUIIXPSpatialUIVariantValue
