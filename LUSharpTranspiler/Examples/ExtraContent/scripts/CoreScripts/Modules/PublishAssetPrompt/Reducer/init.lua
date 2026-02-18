local CorePackages = game:GetService("CorePackages")

local Rodux = require(CorePackages.Packages.Rodux)

local PromptRequestReducer = require(script.PromptRequestReducer)

local Reducer = Rodux.combineReducers({
	promptRequest = PromptRequestReducer,
})

return Reducer
