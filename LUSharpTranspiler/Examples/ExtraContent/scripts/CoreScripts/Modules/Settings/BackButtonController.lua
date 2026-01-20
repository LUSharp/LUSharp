--!nonstrict
local AdService = game:GetService("AdService")
local TeleportService = game:GetService("TeleportService")
local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")
local HttpRbxApiService = game:GetService("HttpRbxApiService")
local Promise = require(CorePackages.Packages.Promise)
local Roact = require(CorePackages.Packages.Roact)

local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local httpRequest = require(RobloxGui.Modules.Common.httpRequest)
local httpImpl = httpRequest(HttpRbxApiService)

local EngineFeatureTeleportHistoryButtons = game:GetEngineFeature("TeleportHistoryButtons")

local RobloxTranslator = require(CorePackages.Workspace.Packages.RobloxTranslator)
local GetGameNameAndDescription = require(CorePackages.Workspace.Packages.GameDetailRodux).GetGameNameAndDescription
local adsBack = false

local ReturnDestinationUniverseId = 0
local sourceUniverseId, destinationUniverseId = AdService:GetAdTeleportInfo()
if sourceUniverseId > 0 and (destinationUniverseId == 0 or destinationUniverseId == game.GameId) then
	ReturnDestinationUniverseId = sourceUniverseId
	if EngineFeatureTeleportHistoryButtons then
		adsBack = true
	end
end

if (EngineFeatureTeleportHistoryButtons and ReturnDestinationUniverseId == 0) then
	local sourceUniverseId, _destinationPlaceId = TeleportService:GetThirdPartyTeleportInfo(false)
	if sourceUniverseId > 0 then
		ReturnDestinationUniverseId = sourceUniverseId
	end
	adsBack = false
end

local function truncateWithEllipsis(text, maxLength)
	if #text > maxLength then
		return string.sub(text, 1, maxLength - 3) .. "..."
	else
		return text
	end
end

local ReturnDestinationPlaceName = nil
local ReturnDestinationPlaceNamePromise = Promise.defer(function(resolve)
	if ReturnDestinationUniverseId > 0 then
		GetGameNameAndDescription(httpImpl, ReturnDestinationUniverseId):andThen(function(result)
			ReturnDestinationPlaceName = result.Name
			if EngineFeatureTeleportHistoryButtons then
				ReturnDestinationPlaceName = truncateWithEllipsis(result.Name, 30)
			end
		end):await()
		resolve()
	else
		resolve()
	end
end)

local BackButtonController = {}

BackButtonController.getTooltipText = function()
	if ReturnDestinationPlaceName == nil then
		return RobloxTranslator:FormatByKey("InGame.CommonUI.Label.ReturnFallback")
	else
		return RobloxTranslator:FormatByKey("InGame.CommonUI.Label.ReturnTo", {
			placeName = ReturnDestinationPlaceName,
		})
	end
end

BackButtonController.getTooltipTextTimeout = function()
	if ReturnDestinationPlaceName == nil then
		Promise.any({ ReturnDestinationPlaceNamePromise, Promise.delay(1) }):await()
	end
	return BackButtonController.getTooltipText()
end

BackButtonController.getMenuText = function()
	local menuText, updateMenuText = Roact.createBinding(BackButtonController.getTooltipText())
	if ReturnDestinationPlaceName == nil then
		pcall(function()
			ReturnDestinationPlaceNamePromise:andThen(function()
				updateMenuText(BackButtonController.getTooltipText())
			end)
		end)
	end
	return menuText
end

BackButtonController.initiateBackButtonTeleport = function(teleportMethod)
	if EngineFeatureTeleportHistoryButtons then
		if adsBack then
			AdService:ReturnToPublisherExperience(teleportMethod)
		else
			TeleportService:TeleportTrustedBackForth(false)
		end
	end
	AdService:ReturnToPublisherExperience(teleportMethod)
end

BackButtonController.hasReturnUniverse = function()
	return ReturnDestinationUniverseId > 0
end

return BackButtonController
