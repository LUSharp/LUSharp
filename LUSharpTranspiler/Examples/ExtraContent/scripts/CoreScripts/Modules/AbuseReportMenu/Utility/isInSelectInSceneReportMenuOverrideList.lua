local Players = game:GetService("Players")

game:DefineFastString("SelectInSceneReportMenuOverrideUserIds", "")

function isInSelectInSceneReportMenuOverrideList()
	local localPlayer = Players.LocalPlayer
	while not localPlayer do
		Players:GetPropertyChangedSignal("LocalPlayer"):Wait()
		localPlayer = Players.LocalPlayer
	end

	local forcedUserIds = game:GetFastString("SelectInSceneReportMenuOverrideUserIds")
	for forcedUserIdString in forcedUserIds:gmatch("%d+") do
		if localPlayer and tonumber(forcedUserIdString) == localPlayer.UserId then
			return true
		end
	end

	return false
end

return isInSelectInSceneReportMenuOverrideList
