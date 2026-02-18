--!nonstrict
local CoreGui = game:GetService("CoreGui")
local CorePackages = game:GetService("CorePackages")
local LocalizationService = game:GetService("LocalizationService")

local Roact = require(CorePackages.Packages.Roact)

local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local renderWithCoreScriptsStyleProvider = require(RobloxGui.Modules.Common.renderWithCoreScriptsStyleProvider)

local Localization = require(CorePackages.Workspace.Packages.InExperienceLocales).Localization
local LocalizationProvider = require(CorePackages.Workspace.Packages.Localization).LocalizationProvider

local FTUXPanel = require(script.Components.FTUXPanel)
local GetFeatures = require(script.Features.GetFeatures)

local PlatformEnum = require(script.Enums.PlatformEnum)
type Platform = PlatformEnum.Platform

return {
	mountFtuxMenu = function(platform: Platform)
		game.StarterGui:SetCoreGuiEnabled(Enum.CoreGuiType.PlayerList, false)
		game:GetService("StarterGui"):SetCoreGuiEnabled(Enum.CoreGuiType.Chat, false)
		local _ftuxTree = Roact.createElement(LocalizationProvider, {
			localization = Localization.new(LocalizationService.RobloxLocaleId),
		}, {
			FTUXMenu = Roact.createElement("ScreenGui", {}, {
				ThemeProvider = renderWithCoreScriptsStyleProvider({
					FtuxPanel = Roact.createElement(FTUXPanel, {
						platform = platform,
					}, {}),
				}),
			}),
		})

		GetFeatures(platform)
		Roact.mount(_ftuxTree, CoreGui, "FTUXMenu")
	end,
}
