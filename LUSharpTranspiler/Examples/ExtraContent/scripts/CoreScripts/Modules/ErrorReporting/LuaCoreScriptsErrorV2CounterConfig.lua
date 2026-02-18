local CorePackages = game:GetService("CorePackages")
local LoggingProtocol = require(CorePackages.Workspace.Packages.LoggingProtocol)
local FIntLuaCoreScriptsErrorV2ThrottleHundredthPercentage = game:DefineFastInt("LuaCoreScriptsErrorV2ThrottleHundredthPercentage", 0)

type TelemetryEventConfig = LoggingProtocol.TelemetryEventConfig
return {
	eventName = "LuaCoreScriptsError",
	backends = {
		LoggingProtocol.TelemetryBackends.Counter,
	},
	throttlingPercentage = FIntLuaCoreScriptsErrorV2ThrottleHundredthPercentage,
	lastUpdated = { 25, 1, 29 },
	description = [[V2 Counter to for Lua CoreScripts Error.]],
} :: TelemetryEventConfig
