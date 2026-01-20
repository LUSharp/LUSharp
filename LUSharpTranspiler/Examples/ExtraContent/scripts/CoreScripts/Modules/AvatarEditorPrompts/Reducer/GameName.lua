local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")

local Rodux = require(CorePackages.Packages.Rodux)

local RobloxGui = CoreGui:WaitForChild("RobloxGui")
local RobloxTranslator = require(CorePackages.Workspace.Packages.RobloxTranslator)

local Actions = script.Parent.Parent.Actions

local GameNameFetched = require(Actions.GameNameFetched)

local DefaultPlaceHolderName = RobloxTranslator:FormatByKey("CoreScripts.AvatarEditorPrompts.GameNamePlaceHolder")

return Rodux.createReducer(DefaultPlaceHolderName, {
	[GameNameFetched.name] = function(state, action)
		return action.gameName
	end,
})
