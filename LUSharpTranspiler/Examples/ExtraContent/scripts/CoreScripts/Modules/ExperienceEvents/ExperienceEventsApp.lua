--[[
	CoreScript entry point for the ExperienceEvents module.
]]

local CoreGui = game:GetService("CoreGui")
local CorePackages = game:GetService("CorePackages")

local RobloxGui = CoreGui:WaitForChild("RobloxGui")

local VirtualEvents = require(CorePackages.Workspace.Packages.VirtualEvents)

local EventsInExperienceApp = VirtualEvents.EventsInExperienceApp.createApp()

EventsInExperienceApp.mountCoreUI(RobloxGui)

return EventsInExperienceApp
