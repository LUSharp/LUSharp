local CoreGui = game:GetService("CoreGui")
local UserInputService = game:GetService("UserInputService")
local CorePackages = game:GetService("CorePackages")

local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local CachedPolicyService = require(CorePackages.Workspace.Packages.CachedPolicyService)
local GetFFlagScreenshotHudApi = require(RobloxGui.Modules.Flags.GetFFlagScreenshotHudApi)

return function()
	local platform = UserInputService:GetPlatform()
	local isMobile = platform == Enum.Platform.IOS or platform == Enum.Platform.Android
	local shouldSaveScreenshotToAlbum = false

	if GetFFlagScreenshotHudApi() and isMobile and
		not CachedPolicyService:IsSubjectToChinaPolicies() then
		shouldSaveScreenshotToAlbum = true
	end

	return shouldSaveScreenshotToAlbum
end
