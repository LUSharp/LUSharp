--[[
Registers all integrations and returns table of configurations from each integration
]]

local Settings = script:FindFirstAncestor("Settings")

-- Modules
local Integrations = Settings.Integrations

-- Core Module
local Settings = require(Integrations.Settings)
local Constants = require(Integrations.Constants)
local Utils = require(Integrations.Utils)

return {
	Settings = Settings,
	Constants = Constants,
	Utils = Utils,
}
