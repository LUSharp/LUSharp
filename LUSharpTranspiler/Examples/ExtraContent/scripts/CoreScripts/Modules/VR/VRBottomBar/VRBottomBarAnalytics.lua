local AnalyticsService = game:GetService("RbxAnalyticsService")
local TelemetryService = game:GetService("TelemetryService")

local EngineFeatureTelemetryServicePlaySessionInfoEnabled = game:GetEngineFeature("TelemetryServicePlaySessionInfoEnabled")

type Props = {
	integrationId: string,
	isToggleOn: boolean?,
	source: string?,
}

local function sendEventToTelemetryV2(props: Props)
	local VRBottomBarActionConfig = {
		eventName = "coreui_vr_bottom_bar_action",
		backends = { "EventIngest" },
		throttlingPercentage = game:DefineFastInt("VRBottomBarActionEventThrottleHunderedthsPercent", 0),
		lastUpdated = { 2025, 5, 12 },
		description = "Event fired from the client every time VRBottomBar buttons are clicked",
		links = "https://github.rbx.com/Roblox/proto-schemas/pull/5803",
	}

	local standardizedFields = { "addPlaceId", "addUniverseId", "addSessionInfo"}
	local customFields = {
		integration_id = props.integrationId,
		is_toggle_on = tostring(props.isToggleOn),
		source = props.source,
	}

	if EngineFeatureTelemetryServicePlaySessionInfoEnabled then
		table.insert(standardizedFields, "addPlaySessionId")
	else
		customFields["playsessionid"] = AnalyticsService:GetPlaySessionId()
	end
	TelemetryService:LogEvent(VRBottomBarActionConfig, {standardizedFields = standardizedFields, customFields = customFields})
end

return {
	sendEventToTelemetryV2 = sendEventToTelemetryV2,
}
