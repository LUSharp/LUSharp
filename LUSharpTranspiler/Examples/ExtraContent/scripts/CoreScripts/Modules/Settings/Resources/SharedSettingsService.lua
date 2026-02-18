--[[
Wraps instance of SettingsService to be shared among In-Experience Settings
]]

-- Services
local CorePackages = game:GetService("CorePackages")

-- Modules
local SettingsServiceLib = require(CorePackages.Workspace.Packages.SettingsService)

-- Core Module
local instance: SettingsServiceLib.SettingsService = SettingsServiceLib.SettingsService.new()

return instance
