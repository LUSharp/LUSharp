-- Define an enum type for spatial UIs
export type SpatialUITypeValue = "ScreenUI" | "SpatialUI" | "SpatialUIPartOnly" | "SpatialUIRoact"
export type SpatialUIGroupValue = "MainUIGroup" | "WristUIGroup"
export type PanelTypeValue = "Chat" | "RobloxGui" | "BottomBar" | "MoreMenu"
export type SpatialUIGroupTypeValue = "MainUIGroup" | "WristUIGroup"

export type SpatialUIProps = {
	-- Size of the "virtual screen" the GUI thinks it is rendered on
	virtualScreenSize: Vector2?,
	-- Size of the screen in the 3D space
	partSize: Vector2,
	-- Is the panel static in the world, following the wrist, following the head?
	cframe: CFrame?, -- cframe of the panel
	alwaysOnTop: boolean?, -- should the panel render on top of geometry
	parent: Instance?, -- parent object, e.g. folder of parts
	hidden: boolean?, -- whether to hide the panel
	curvature: number?,
	partOnly: boolean?, -- whether only the panel part should be created
	name: string, -- Name
	transparency: number?,
}

export type PanelCreationProps = {
	panelType: PanelTypeValue,
	screenGuiProps: ScreenGuiProps, -- Props for ScreenGui, this is passed directly to the ScreenGui constructor if a ScreenGui is used
}

export type ScreenGuiProps = {
	Name: string,
	ResetOnSpawn: boolean,
	DisplayOrder: number,
	ZIndexBehavior: Enum.ZIndexBehavior?,
}

export type PanelCompatProps = {
	spatialPanelProps: SpatialUIProps?,
	screenGuiProps: ScreenGuiProps?,
	type: SpatialUITypeValue,
}

export type CompatPanel = {
	type: SpatialUITypeValue,
	panelObject: Instance,
}

export type DropBarProps = {
	name: string,
	dragFunction: (
		dragger: Player,
		cursorRay: Ray,
		hiFrame: CFrame,
		vrInputFrame: Instance,
		isModeSwitchKeyDown: boolean
	) -> ()?,
	dragStartFunction: (
		dragger: Player,
		cursorRay: Ray,
		hiFrame: CFrame,
		hitFrame: CFrame,
		clickedPart: BasePart,
		vrInputFrame: Instance,
		isModeSwitchKeyDown: boolean
	) -> ()?,
	dragEndFunction: (playerWhoDragged: Player) -> ()?,
}

export type CameraFixedUIObjectProps = {
	-- Offset of the panel within the UI group relative to the origin computed by headCrame:ToWorlsSpace(uiGroupHeadRotation)
	uiGroupElementOffset: CFrame,
}

export type PanelPositionProps = {
	-- The UI group that the panel belongs to
	uiGroup: SpatialUIGroupTypeValue,
	cameraFixedPanelProp: CameraFixedUIObjectProps?,
}

export type PanelStruct = {
	panelObject: Instance,
	uiType: SpatialUITypeValue,
	panelType: PanelTypeValue,
	panelPositionProps: PanelPositionProps?,
	headScale: number,
}

local SpatialUIType = {
	ScreenUI = "ScreenUI" :: SpatialUITypeValue,
	SpatialUI = "SpatialUI" :: SpatialUITypeValue,
	SpatialUIPartOnly = "SpatialUIPartOnly" :: SpatialUITypeValue,
	SpatialUIRoact = "SpatialUIRoact" :: SpatialUITypeValue,
}

-- Define an enum type for panel types
local PanelType = {
	Chat = "Chat" :: PanelTypeValue,
	RobloxGui = "RobloxGui" :: PanelTypeValue,
	BottomBar = "BottomBar" :: PanelTypeValue,
	MoreMenu = "MoreMenu" :: PanelTypeValue,
}

local SpatialUIGroupType = {
	MainUIGroup = "MainUIGroup" :: SpatialUIGroupTypeValue,
	WristUIGroup = "WristUIGroup" :: SpatialUIGroupTypeValue,
}

local BOTTOM_BAR_BASE_PART_SIZE = 0.15

local METER_TO_STUD_FACTOR = 1 / 0.3

local CHAT_PANEL_SIZE = Vector2.new(0.1365, 0.1995) * METER_TO_STUD_FACTOR

local ROBLOX_GUI_PANEL_SIZE = Vector2.new(0.532, 0.3395) * METER_TO_STUD_FACTOR

local VR_PANEL_RESOLUTION_MULTIPLIER = 800

local ROBLOX_CHAT_UIGROUP_ELEMENT_ROTATION = CFrame.new(
	ROBLOX_GUI_PANEL_SIZE.X * 0.5 + CHAT_PANEL_SIZE.X * 0.5 + 0.02 * METER_TO_STUD_FACTOR,
	(ROBLOX_GUI_PANEL_SIZE.Y - CHAT_PANEL_SIZE.Y) / 2,
	0
)

local COMPAT_PANEL_PROPS_MAP: { [PanelTypeValue]: PanelCompatProps } = {
	[PanelType.Chat] = {
		type = SpatialUIType.SpatialUI,
		spatialPanelProps = {
			name = "ExperienceChat",
			faceCamera = true,
			lerp = true,
			partSize = CHAT_PANEL_SIZE,
			virtualScreenSize = CHAT_PANEL_SIZE * VR_PANEL_RESOLUTION_MULTIPLIER,
		},
	} :: PanelCompatProps,
	[PanelType.RobloxGui] = {
		type = SpatialUIType.SpatialUIPartOnly,
		spatialPanelProps = {
			name = "RobloxGuiPanel-PartOnly",
			faceCamera = true,
			lerp = true,
			partSize = ROBLOX_GUI_PANEL_SIZE,
			transparency = 0.5,
		},
	} :: PanelCompatProps,
}

local SPATIAL_PANEL_POSITION_PROPS_MAP: { [PanelTypeValue]: PanelPositionProps } = {
	[PanelType.Chat] = {
		uiGroup = SpatialUIGroupType.MainUIGroup,
		cameraFixedPanelProp = {
			uiGroupElementOffset = ROBLOX_CHAT_UIGROUP_ELEMENT_ROTATION,
		},
	} :: PanelPositionProps,
	[PanelType.RobloxGui] = {
		uiGroup = SpatialUIGroupType.MainUIGroup,
		cameraFixedPanelProp = { uiGroupElementOffset = CFrame.new(0, 0, 0) },
	} :: PanelPositionProps,
}

local ROBLOX_BOTTOM_BAR_UIGROUP_ELEMENT_ROTATION = CFrame.new(
	0,
	-ROBLOX_GUI_PANEL_SIZE.Y * 0.5 - 0.075 - 0.01 * METER_TO_STUD_FACTOR,
	0
) * CFrame.Angles(0, math.rad(180), 0) -- Additional rotation to face front

local MORE_MENU_UIGROUP_ELEMENT_ROTATION = CFrame.new(
	0.4,
	-- -ROBLOX_GUI_PANEL_SIZE.Y * 0.5 - 0.075 - 0.01 * METER_TO_STUD_FACTOR,
	-0.36,
	0
) * CFrame.Angles(0, math.rad(180), 0) -- Additional rotation to face front

local ROACT_PANEL_UIGROUP_MAP = {
	[PanelType.BottomBar] = SpatialUIGroupType.MainUIGroup,
	[PanelType.MoreMenu] = SpatialUIGroupType.MainUIGroup,
}

local ROACT_PANEL_UIGROUP_ELEMENT_OFFSET_MAP = {
	[PanelType.BottomBar] = ROBLOX_BOTTOM_BAR_UIGROUP_ELEMENT_ROTATION,
	[PanelType.MoreMenu] = MORE_MENU_UIGROUP_ELEMENT_ROTATION,
}

return {
	SpatialUIType = SpatialUIType,
	SpatialUIGroupType = SpatialUIGroupType,
	PanelType = PanelType,
	DEFAULT_VR_PANEL_SIZE = Vector2.new(10, 10),
	VR_PANEL_RESOLUTION_MULTIPLIER = VR_PANEL_RESOLUTION_MULTIPLIER,
	BOTTOM_BAR_BASE_PART_SIZE = BOTTOM_BAR_BASE_PART_SIZE,
	COMPAT_PANEL_PROPS_MAP = COMPAT_PANEL_PROPS_MAP,
	SPATIAL_PANEL_POSITION_PROPS_MAP = SPATIAL_PANEL_POSITION_PROPS_MAP,
	ROACT_PANEL_UIGROUP_MAP = ROACT_PANEL_UIGROUP_MAP,
	ROACT_PANEL_UIGROUP_ELEMENT_OFFSET_MAP = ROACT_PANEL_UIGROUP_ELEMENT_OFFSET_MAP,
}
