--[[
    Action that tracks the number of feedback submission attempts for the users session.
]]
local CorePackages = game:GetService("CorePackages")

local Action = require(CorePackages.Packages.Rodux).makeActionCreator

return Action(script.Name, function(newNumFeedbackSubmissionAttempts)
	return {
		numFeedbackSubmissionAttempts = newNumFeedbackSubmissionAttempts,
	}
end)
