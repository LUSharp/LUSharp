local UIManagerRoot = script.Parent
local DragBar = require(UIManagerRoot.DragBar)
local Constants = require(UIManagerRoot.Constants)
local CoreGui = game:GetService("CoreGui")

local DEFAULT_DRAG_BAR_SIZE = Vector3.new(0.6, 0.12, 0.00001)

return function(props: Constants.DropBarProps): DragBar.DragBarClassType
	local draggablePart = Instance.new("Part")
	draggablePart.Size = DEFAULT_DRAG_BAR_SIZE
	draggablePart.Name = props.name .. "_DragBar"
	draggablePart.Color = Color3.new(1, 1, 1)
	draggablePart.Parent = workspace.CurrentCamera
	draggablePart.Anchored = true
	draggablePart.CanTouch = true
	draggablePart.CanCollide = false
	draggablePart.CastShadow = false
	draggablePart.Material = Enum.Material.Glass
	draggablePart.Transparency = 1

	local dragDetector = Instance.new("DragDetector")
	dragDetector.Enabled = true
	dragDetector.Parent = draggablePart
	dragDetector.DragStyle = Enum.DragDetectorDragStyle.BestForDevice

	local surfaceGui = Instance.new("SurfaceGui", CoreGui)

	surfaceGui.Name = "dragBar_SurfaceGui"
	surfaceGui.Enabled = true
	surfaceGui.CanvasSize = Vector2.new(draggablePart.Size.X, draggablePart.Size.Y)
		* Constants.VR_PANEL_RESOLUTION_MULTIPLIER
	surfaceGui.AlwaysOnTop = true
	surfaceGui.Shape = Enum.SurfaceGuiShape.Flat
	surfaceGui.HorizontalCurvature = 0
	surfaceGui.Active = true
	surfaceGui.Adornee = draggablePart
	surfaceGui.ZIndexBehavior = Enum.ZIndexBehavior.Sibling
	surfaceGui.LightInfluence = 0
	surfaceGui.ZOffset = 0
	surfaceGui.Face = Enum.NormalId.Back

	local imageLabel = Instance.new("ImageLabel")
	imageLabel.Image = "rbxasset://textures/ui/InGameMenu/DragBar_Bright.png"
	imageLabel.Parent = surfaceGui
	imageLabel.BackgroundTransparency = 1
	imageLabel.BackgroundColor3 = Color3.new(1, 1, 1)
	imageLabel.AnchorPoint = Vector2.new(0.5, 0.5)
	imageLabel.Position = UDim2.new(0.5, 0, 0.5, 0)
	imageLabel.Size = UDim2.new(0.5, 0, 0.25, 0)
	imageLabel.ImageColor3 = Color3.new(1, 1, 1)

	-- Needed since the part is not replicated to server
	dragDetector.RunLocally = true
	if props.dragFunction then
		dragDetector.DragContinue:Connect(props.dragFunction)
	end
	if props.dragStartFunction then
		dragDetector.DragStart:Connect(props.dragStartFunction)
	end
	if props.dragEndFunction then
		dragDetector.DragEnd:Connect(props.dragEndFunction)
	end
	local dragBar = DragBar.new(draggablePart, imageLabel)
	return dragBar
end
