local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")

local Chrome = script:FindFirstAncestor("Chrome")
local ChromeUtils = require(Chrome.ChromeShared.Service.ChromeUtils)
local MappedSignal = ChromeUtils.MappedSignal

local SignalLib = require(CorePackages.Workspace.Packages.AppCommonLib)
local Signal = SignalLib.Signal

local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local FFlagRespawnChromeShortcutTelemetry = require(Chrome.Flags.FFlagRespawnChromeShortcutTelemetry)

local respawnPageOpen = false
local respawnPageOpenSignal = Signal.new()
local mappedRespawnPageOpenSignal = MappedSignal.new(respawnPageOpenSignal, function()
	return respawnPageOpen
end)

task.defer(function()
	local SettingsHub = require(RobloxGui.Modules.Settings.SettingsHub)
	SettingsHub.CurrentPageSignal:connect(function(pageName)
		respawnPageOpen = pageName == SettingsHub.Instance.ResetCharacterPage.Page.Name
		respawnPageOpenSignal:fire()
	end)
end)

export type RespawnProps = {
	usedShortcut: boolean?,
}

local function respawnPage(props: RespawnProps?)
	local SettingsHub = require(RobloxGui.Modules.Settings.SettingsHub)
	local RespawnPage = SettingsHub.Instance.ResetCharacterPage
	local function switchToRespawnPage()
		local respawnOpenPayload = {
			used_shortcut = if props then props.usedShortcut else nil,
		}
		SettingsHub.Instance:SwitchToPage(RespawnPage, true, nil, nil, nil, respawnOpenPayload)
	end

	if SettingsHub:GetVisibility() then
		if respawnPageOpen then
			SettingsHub:SetVisibility(false)
		else
			if FFlagRespawnChromeShortcutTelemetry then
				switchToRespawnPage()
			else
				SettingsHub.Instance:SwitchToPage(RespawnPage, true)
			end
		end
	else
		if FFlagRespawnChromeShortcutTelemetry then
			SettingsHub:SetVisibility(true)
			switchToRespawnPage()
		else
			SettingsHub:SetVisibility(true, false, RespawnPage)
		end
	end
end

return {
	respawnPageOpenSignal = mappedRespawnPageOpenSignal,
	respawnPage = respawnPage,
}
