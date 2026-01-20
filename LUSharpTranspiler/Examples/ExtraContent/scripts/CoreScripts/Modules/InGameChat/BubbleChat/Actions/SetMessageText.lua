local CorePackages = game:GetService("CorePackages")
local ExperienceChat = require(CorePackages.Workspace.Packages.ExpChat)

local Action = require(CorePackages.Packages.Rodux).makeActionCreator
local t = require(CorePackages.Packages.t)
local maybeAssert = require(script.Parent.Parent.Helpers.maybeAssert)

local check = t.tuple(t.string, t.string)

return Action(script.Name, function(messageId, newText)
	maybeAssert(check(messageId, newText))

	ExperienceChat.Events.LegacyBubbleTextUpdated(messageId, newText)

	return {
		messageId = messageId,
		newText = newText,
	}
end)
