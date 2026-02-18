--!nonstrict
local TeleportService = game:GetService("TeleportService")
local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")
local HttpRbxApiService = game:GetService("HttpRbxApiService")
local Promise = require(CorePackages.Packages.Promise)
local Roact = require(CorePackages.Packages.Roact)

local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local httpRequest = require(RobloxGui.Modules.Common.httpRequest)
local httpImpl = httpRequest(HttpRbxApiService)

local RobloxTranslator = require(CorePackages.Workspace.Packages.RobloxTranslator)
local GetGameNameAndDescription = require(CorePackages.Workspace.Packages.GameDetailRodux).GetGameNameAndDescription
local EngineFeatureTeleportHistoryButtons = game:GetEngineFeature("TeleportHistoryButtons")

local ReturnDestinationUniverseId = 0
local sourceUniverseId = 0
local _destinationPlaceId = 0

if EngineFeatureTeleportHistoryButtons then
	sourceUniverseId, _destinationPlaceId = TeleportService:GetThirdPartyTeleportInfo(true)
end

if EngineFeatureTeleportHistoryButtons and sourceUniverseId > 0 then
	ReturnDestinationUniverseId = sourceUniverseId
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
			ReturnDestinationPlaceName = truncateWithEllipsis(result.Name, 30)
		end):await()
		resolve()
	else
		resolve()
	end
end)

local FrontButtonController = {}
FrontButtonController.getTooltipText = function()
	if ReturnDestinationPlaceName == nil then
		return RobloxTranslator:FormatByKey("InGame.CommonUI.Label.ReturnFallback")
	else
		return RobloxTranslator:FormatByKey("InGame.CommonUI.Label.ReturnTo", {
			placeName = ReturnDestinationPlaceName,
		})
	end
end
-- This function is not called?
FrontButtonController.getTooltipTextTimeout = function()
	if ReturnDestinationPlaceName == nil then
		Promise.any({ ReturnDestinationPlaceNamePromise, Promise.delay(1) }):await()
	end
	return FrontButtonController.getTooltipText()
end
FrontButtonController.getMenuText = function()
	local menuText, updateMenuText = Roact.createBinding(FrontButtonController.getTooltipText())
	if ReturnDestinationPlaceName == nil then
		pcall(function()
			ReturnDestinationPlaceNamePromise:andThen(function()
				updateMenuText(FrontButtonController.getTooltipText())
			end)
		end)
	end
	return menuText
end
FrontButtonController.initiateFrontButtonTeleport = function()
	if EngineFeatureTeleportHistoryButtons then
		TeleportService:TeleportTrustedBackForth(true)
	end
end
FrontButtonController.hasReturnUniverse = function()
	return ReturnDestinationUniverseId > 0
end
return FrontButtonController