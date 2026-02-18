local CorePackages = game:GetService("CorePackages")

local Roact = require(CorePackages.Packages.Roact)
local Cryo = require(CorePackages.Packages.Cryo)

local Localization = require(CorePackages.Workspace.Packages.InExperienceLocales).Localization
local LocalizationProvider = require(CorePackages.Workspace.Packages.Localization).LocalizationProvider

local BlockingModalContainer = require(script.Parent.BlockingModalContainer)

local noOpt = function() end

return function(props)
	return Roact.createElement(LocalizationProvider, {
			localization = Localization.new("en-us"),
		}, {
			BlockingModalContainer = Roact.createElement(
				BlockingModalContainer,
				Cryo.Dictionary.join({
					blockingUtility = {
						BlockPlayerAsync = function(player)
							return true, true
						end,
					},
					translator = {
						FormatByKey = function(_, key)
							return key
						end,
					},
					analytics = {
						action = noOpt,
					},
					closeModal = noOpt,
					player = {
						DisplayName = "Dan",
						Name = "Dan",
						UserId = 12345,
					},
					source = "source",
				}, props)
			)
		})
end
