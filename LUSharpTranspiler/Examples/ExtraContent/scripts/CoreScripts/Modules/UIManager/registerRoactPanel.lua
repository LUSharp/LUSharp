local UIManagerRoot = script.Parent
local UIManager = require(UIManagerRoot.UIManager)
local Constants = require(UIManagerRoot.Constants)

export type Ref<T> = { current: T }

return function(surfaceGui: Ref<SurfaceGui | nil>, panelType: Constants.PanelTypeValue)
	local cameraFixedPanelProp: Constants.CameraFixedUIObjectProps = nil
	local uiGroup: Constants.SpatialUIGroupTypeValue? = Constants.ROACT_PANEL_UIGROUP_MAP[panelType]
	if uiGroup then
		cameraFixedPanelProp = {
			uiGroupElementOffset = Constants.ROACT_PANEL_UIGROUP_ELEMENT_OFFSET_MAP[panelType],
		}
	end
	local panelStruct: Constants.PanelStruct = {
		panelObject = surfaceGui.current :: SurfaceGui,
		uiType = Constants.SpatialUIType.SpatialUIRoact,
		panelType = panelType,
		headScale = 1,
		panelPositionProps = { uiGroup = uiGroup, cameraFixedPanelProp = cameraFixedPanelProp } :: Constants.PanelPositionProps,
	}
	UIManager.getInstance():registerRoactPanel(panelType, panelStruct)
end
