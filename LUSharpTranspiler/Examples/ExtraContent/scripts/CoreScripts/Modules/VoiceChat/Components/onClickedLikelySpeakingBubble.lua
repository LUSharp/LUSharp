local CoreGui = game:GetService("CoreGui")
local CorePackages = game:GetService("CorePackages")
local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local VoiceChatServiceManager = require(RobloxGui.Modules.VoiceChat.VoiceChatServiceManager).default
local VoiceConstants = require(RobloxGui.Modules.VoiceChat.Constants)
local ExperienceChat = require(CorePackages.Workspace.Packages.ExpChat)

local GetFFlagEnableInExpPhoneVoiceUpsellEntrypoints =
	require(CorePackages.Workspace.Packages.SharedFlags).GetFFlagEnableInExpPhoneVoiceUpsellEntrypoints
local GetFFlagSeamlessVoiceFTUX = require(CorePackages.Workspace.Packages.SharedFlags).GetFFlagSeamlessVoiceFTUX
local GetFFlagEnableSeamlessVoiceV2 = require(CorePackages.Workspace.Packages.SharedFlags).GetFFlagEnableSeamlessVoiceV2

return function()
	if
		GetFFlagEnableInExpPhoneVoiceUpsellEntrypoints()
		and VoiceChatServiceManager:FetchPhoneVerificationUpsell(VoiceConstants.IN_EXP_PHONE_UPSELL_IXP_LAYER)
			== VoiceConstants.PHONE_UPSELL_VALUE_PROP.VoiceChat
	then
		VoiceChatServiceManager:ShowInExperiencePhoneVoiceUpsell(
			VoiceConstants.IN_EXP_UPSELL_ENTRYPOINTS.LIKELY_SPEAKING,
			VoiceConstants.IN_EXP_PHONE_UPSELL_IXP_LAYER
		)
	elseif GetFFlagEnableSeamlessVoiceV2() and VoiceChatServiceManager:IsSeamlessVoice() then
		VoiceChatServiceManager:JoinVoice()
		ExperienceChat.Events.ShowLikelySpeakingBubblesChanged(false)
		VoiceChatServiceManager:RecordUserSeenModal("m3-likely-speaking-bubble-upsell")
	elseif GetFFlagSeamlessVoiceFTUX() and VoiceChatServiceManager.isShowingFTUX then
		VoiceChatServiceManager:HideFTUX(game:GetService("AppStorageService"))
	else
		VoiceChatServiceManager:ShowInExperienceVoiceUpsell(VoiceConstants.IN_EXP_UPSELL_ENTRYPOINTS.LIKELY_SPEAKING)
	end
end
