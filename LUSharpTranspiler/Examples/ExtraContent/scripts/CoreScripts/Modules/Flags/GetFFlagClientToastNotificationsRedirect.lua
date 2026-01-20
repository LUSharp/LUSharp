local CorePackages = game:GetService("CorePackages")
local IXPServiceWrapper = require(CorePackages.Workspace.Packages.IxpServiceWrapper).IXPServiceWrapper
local FFlagClientToastNotificationsRedirect = game:DefineFastFlag("ClientToastNotificationsRedirect4", false)
local FFlagEnableClientToastNotificationExperiment = game:DefineFastFlag("EnableClientToastNotificationsRedirectExperiment", false)
local layerName = game:DefineFastString("ClientToastNotificationsRedirectLayerName", "Notification.Toast")
local layerValue = game:DefineFastString("ClientToastNotificationsRedirectLayerValue", "NotificationRedirect")

local hasFetchedIxp = false
local layerFetchSuccess = nil
local layerData = nil

return function()
    if FFlagEnableClientToastNotificationExperiment and not hasFetchedIxp then
        layerFetchSuccess, layerData = pcall(function()
            hasFetchedIxp = true
            return IXPServiceWrapper:GetLayerData(layerName)
        end)
    end

    return FFlagClientToastNotificationsRedirect or (layerFetchSuccess and layerData and layerData[layerValue])
end
