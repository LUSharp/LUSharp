local RobloxReplicatedStorage = game:GetService("RobloxReplicatedStorage")
local Players = game:GetService("Players")
local CorePackages = game:GetService("CorePackages")

local GetFFlagEnableReferredPlayerJoinRemoteEvent = require(CorePackages.Workspace.Packages.SharedFlags).GetFFlagEnableReferredPlayerJoinRemoteEvent

--[[
    If a player joined the game through a referral (i.e. another player invited them), 
    this script will fire a RemoteEvent to the player who referred them with information on the player that joined them. 
]]
if GetFFlagEnableReferredPlayerJoinRemoteEvent() then
    local ReferredPlayerJoinRemoteEvent = Instance.new("RemoteEvent")
    ReferredPlayerJoinRemoteEvent.Name = "ReferredPlayerJoin"
    ReferredPlayerJoinRemoteEvent.Parent = RobloxReplicatedStorage

    Players.PlayerAdded:Connect(function(player)
        local joinData = player:GetJoinData()
        local referredByPlayerId = joinData and tonumber(joinData.ReferredByPlayerId)
        if referredByPlayerId and referredByPlayerId ~= 0 then
            local referredByPlayer = Players:GetPlayerByUserId(referredByPlayerId)
            if referredByPlayer then
                ReferredPlayerJoinRemoteEvent:FireClient(referredByPlayer, player)
            end
        end
    end)
end
