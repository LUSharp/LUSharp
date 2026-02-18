local CorePackages = game:GetService("CorePackages")
local IXPServiceWrapper = require(CorePackages.Workspace.Packages.IxpServiceWrapper).IXPServiceWrapper
local FFlagFriendshipNotifsUseSendrEnabledForAll = game:DefineFastFlag("FriendshipNotifsUseSendrEnabledForAll", false)
local FFlagFriendshipNotifsUseSendrExperiment = game:DefineFastFlag("FriendshipNotifsUseSendrExperiment_v2", false)
local layerName = game:DefineFastString("FriendshipNotifsUseSendrLayerName", "Notification.Toast")
local layerValue = game:DefineFastString("FriendshipNotifsUseSendrLayerValue", "EnableNotificationViaSENDR")

local hasFetchedIxp = false
local layerFetchSuccess = nil
local layerData = nil

return function()
    if FFlagFriendshipNotifsUseSendrExperiment and not hasFetchedIxp then
        layerFetchSuccess, layerData = pcall(function()
            hasFetchedIxp = true
            return IXPServiceWrapper:GetLayerData(layerName)
        end)
    end

    return FFlagFriendshipNotifsUseSendrEnabledForAll or (layerFetchSuccess and layerData and layerData[layerValue])
end
