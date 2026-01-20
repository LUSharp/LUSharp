--[[
	Init script to allow us to use the PublishAssetPrompt folder as a module.
]]
local CorePackages = game:GetService("CorePackages")
local CoreGui = game:GetService("CoreGui")
local LocalizationService = game:GetService("LocalizationService")

local renderWithCoreScriptsStyleProvider = require(script.Parent.Common.renderWithCoreScriptsStyleProvider)

local Localization = require(CorePackages.Workspace.Packages.InExperienceLocales).Localization
local LocalizationProvider = require(CorePackages.Workspace.Packages.Localization).LocalizationProvider

local Roact = require(CorePackages.Packages.Roact)
local Rodux = require(CorePackages.Packages.Rodux)
local RoactRodux = require(CorePackages.Packages.RoactRodux)

local PublishAssetPromptApp = require(script.Components.PublishAssetPromptApp)
local Reducer = require(script.Reducer)
local ConnectAssetServiceEvents = require(script.ConnectAssetServiceEvents)

local PublishAssetPrompt = {}
PublishAssetPrompt.__index = PublishAssetPrompt

function PublishAssetPrompt.new()
	local self = setmetatable({}, PublishAssetPrompt)
	local PublishAssetPromptApp = Roact.createElement(PublishAssetPromptApp)

	self.store = Rodux.Store.new(Reducer, nil, {
		Rodux.thunkMiddleware,
	})

	local providerWrappedApp = Roact.createElement(RoactRodux.StoreProvider, {
		store = self.store,
	}, {
		ThemeProvider = renderWithCoreScriptsStyleProvider({
			LocalizationProvider = Roact.createElement(LocalizationProvider, {
				localization = Localization.new(LocalizationService.RobloxLocaleId),
			}, {
				PublishAssetPromptApp = PublishAssetPromptApp,
			}),
		}),
	})
	-- Root should be a Folder so that style provider stylesheet elements can be portaled properly; otherwise, they will attach to CoreGui
	self.root = Roact.createElement("Folder", {
		Name = "PublishAssetPrompt",
	}, providerWrappedApp)

	self.element = Roact.mount(self.root, CoreGui, "PublishAssetPrompt")

	self.serviceConnections = ConnectAssetServiceEvents(self.store)

	return self
end

return PublishAssetPrompt.new()
