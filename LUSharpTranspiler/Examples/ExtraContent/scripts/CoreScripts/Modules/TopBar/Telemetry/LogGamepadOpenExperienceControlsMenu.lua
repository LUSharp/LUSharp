local TelemetryService = game:GetService("TelemetryService")
local AnalyticsService = game:GetService("RbxAnalyticsService")

local TopBar = script.Parent.Parent

local FFlagLoggingGamepadOpenExpControlsMenu = require(TopBar.Flags.FFlagLoggingGamepadOpenExpControlsMenu)
local FIntGamepadOpenExperienceControlsMenuThrottleHundrethsPercent = game:DefineFastInt("GamepadOpenExperienceControlsMenuThrottleHundrethsPercent", 0)
local EngineFeatureTelemetryServicePlaySessionInfoEnabled = game:GetEngineFeature("TelemetryServicePlaySessionInfoEnabled")

local EVENT_NAME = "coreui_gamepad_toggle_exp_controls"
local EVENT_DESCRIPTION = "Event fired from the client every time Experience Controls Menu is toggled open/closed"

local config = {
	eventName = EVENT_NAME,
	backends = {
        "EventIngest",
	},
	throttlingPercentage = FIntGamepadOpenExperienceControlsMenuThrottleHundrethsPercent,
	lastUpdated = { 25, 4, 25 },
	description = EVENT_DESCRIPTION,
	links = "https://github.rbx.com/Roblox/proto-schemas/pull/5750",
}

return function(isToggleOpen: boolean)
	if not FFlagLoggingGamepadOpenExpControlsMenu then
		return
	end

	local standardizedFields
	local customFields
	if EngineFeatureTelemetryServicePlaySessionInfoEnabled then
		standardizedFields = { "addPlaceId", "addUniverseId", "addPlaySessionId", "addSessionInfo"}
		customFields = { is_toggle_open = tostring(isToggleOpen), }
	else
		standardizedFields = { "addPlaceId", "addUniverseId", "addSessionInfo"}
		customFields = {
			is_toggle_open = tostring(isToggleOpen),
			playsessionid = AnalyticsService:GetPlaySessionId(),
		}
	end

	TelemetryService:LogEvent(config, {standardizedFields = standardizedFields, customFields = customFields})
end
