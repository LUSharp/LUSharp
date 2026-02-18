local UIManagerRoot = script.Parent
local Utils = require(UIManagerRoot.Utils)

type DragBarStateValue = "Hovering" | "Dragging" | "Idle"

local DragBarState = {
	Hovering = "Hovering" :: DragBarStateValue,
	Dragging = "Dragging" :: DragBarStateValue,
	Idle = "Idle" :: DragBarStateValue,
}

local DragBar = {}

local DRAGGING_TRANSPARENCY = 0
local HOVERING_TRANSPARENCY = 0.2
local IDLE_TRANSPARENCY = 0.6

export type DragBarClassType = typeof(setmetatable(
	{} :: {
		part: Part,
		dragBarImage: ImageLabel,
		uiGroupOffSet: CFrame?,
		hovering: boolean,
		dragging: boolean,
		dragBarState: DragBarStateValue,
		partsParent: Instance?,
	},
	DragBar
))

DragBar.__index = DragBar

function DragBar.getCFrame(self: DragBarClassType)
	return self.part.CFrame
end

function DragBar.setCFrame(self: DragBarClassType, cframe: CFrame)
	self.part.CFrame = cframe
end

function DragBar.updateTransparency(self: DragBarClassType)
	if self.dragBarState == DragBarState.Hovering then
		self.dragBarImage.ImageTransparency = HOVERING_TRANSPARENCY
	elseif self.dragBarState == DragBarState.Dragging then
		self.dragBarImage.ImageTransparency = DRAGGING_TRANSPARENCY
	else
		self.dragBarImage.ImageTransparency = IDLE_TRANSPARENCY
	end
end

function DragBar.updateState(self: DragBarClassType)
	if self.dragging then
		self.dragBarState = DragBarState.Dragging
	elseif self.hovering and not self.dragging then
		self.dragBarState = DragBarState.Hovering
	else
		self.dragBarState = DragBarState.Idle
	end
	self:updateTransparency()
end

function DragBar.startDrag(self: DragBarClassType, uiGroupOffSet: CFrame)
	self.dragging = true
	self.uiGroupOffSet = uiGroupOffSet
	self:updateState()
end

function DragBar.dragEnd(self: DragBarClassType)
	self.dragging = false
	self:updateState()
end

function DragBar.startHover(self: DragBarClassType)
	self.hovering = true
	self:updateState()
end

function DragBar.hoverEnd(self: DragBarClassType)
	self.hovering = false
	self:updateState()
end

function DragBar.show(self: DragBarClassType)
	self.part.Parent = self.partsParent
end

function DragBar.hide(self: DragBarClassType)
	self.part.Parent = nil
end

function DragBar.rescale(self: DragBarClassType, rescalingFactor: number)
	self.part.Size = self.part.Size * rescalingFactor
	if self.uiGroupOffSet then
		self.uiGroupOffSet = Utils.rescaleCFramePosition(self.uiGroupOffSet, rescalingFactor)
	end
end

function DragBar.new(part: Part, dragBarImage: ImageLabel): DragBarClassType
	local self = {
		part = part,
		dragBarImage = dragBarImage,
		hovering = false,
		dragging = false,
		dragBarState = DragBarState.Idle,
		partsParent = part.Parent,
		uiGroupOffSet = nil,
	}
	setmetatable(self, DragBar)
	self.dragBarImage.ImageTransparency = IDLE_TRANSPARENCY
	return self
end

return DragBar
