--[[
Wraps old GameSettings.lua page and its new refactored version that uses the 
Settings Framework. Designed to be portable and attachable to any surface.
Flag flip will determine whether the old or new page is attached.

The GameSettings Page lives inside SettingsHub and exposes settings options that
are configurable by the user.
]]

local CoreGui = game:GetService("CoreGui")
local CorePackages = game:GetService("CorePackages")
local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local Modules = RobloxGui.Modules 

-- Flags
local FFlagRefactorInExpGameSettings = require(Modules.Settings.Flags.FFlagRefactorInExpGameSettings)

-- Modules
local Integrations = nil
local Utils = nil
local Constants = nil
if FFlagRefactorInExpGameSettings() then
	Integrations = require(Modules.Settings.Integrations)
	Utils = Integrations.Utils
	Constants = Integrations.Constants
end
local React = require(CorePackages.Packages.React)
local ReactRoblox = require(CorePackages.Packages.ReactRoblox)
local SettingsService = require(Modules.Settings.Resources.SharedSettingsService)
local SettingReactView = require(CorePackages.Workspace.Packages.SettingReactView).SettingReactView
local SettingsPageFactory = require(Modules.Settings.SettingsPageFactory)
local Theme = require(RobloxGui.Modules.Settings.Theme)
local Foundation = require(CorePackages.Packages.Foundation)
local FoundationProvider = Foundation.FoundationProvider

-- Core Module

-- Returns GameSettings Page with Settings Framework
local function createGameSettings()
	local GameSettings = SettingsPageFactory:CreateNewPage()
	------ TAB CUSTOMIZATION -------
	GameSettings.TabHeader.Name = Constants.GAMESETTINGS.TAB_HEADER.NAME
	local icon = Theme.Images[Constants.GAMESETTINGS.TAB_HEADER.ICON]
	GameSettings.TabHeader.TabLabel.Icon.ImageRectOffset = icon.ImageRectOffset
	GameSettings.TabHeader.TabLabel.Icon.ImageRectSize = icon.ImageRectSize
	GameSettings.TabHeader.TabLabel.Icon.Image = icon.Image
	GameSettings.TabHeader.TabLabel.Title.Text = Constants.GAMESETTINGS.TAB_HEADER.TEXT

	------ PAGE CUSTOMIZATION -------
	-- start with fresh page
	GameSettings.Page:ClearAllChildren()

	GameSettings.PageOpen = false
	GameSettings.OpenSettingsPage = function()
		GameSettings.PageOpen = true
	end
	GameSettings.CloseSettingsPage = function()
		GameSettings.PageOpen = false
	end

	local Settings = React.createElement(FoundationProvider, {
		theme = Foundation.Enums.Theme.Dark,
		device = Utils.getDeviceType(),
	}, {
		Child = React.createElement(SettingReactView, {
			service = SettingsService,
			pageId = Constants.GAMESETTINGS.PAGE_ID,
		})
	})

	local tree = ReactRoblox.createRoot(GameSettings.Page)
	tree:render(Settings)

	return GameSettings
end

if FFlagRefactorInExpGameSettings() then
	return createGameSettings()
end
return require(Modules.Settings.Pages.GameSettings)
