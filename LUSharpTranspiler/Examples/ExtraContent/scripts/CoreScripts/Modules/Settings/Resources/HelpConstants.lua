local RIGHT_ALIGNED_QUEST_GAMEPAD_LABELS_X = -0.015
local LEFT_ALIGNED_QUEST_GAMEPAD_LABELS_X = 0.818
local QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH = 0.17

local Images = {
	GamepadQuest = {
		Image = "rbxassetid://13262267483",
		HeightRatio = 215 / 420, -- https://create.roblox.com/store/asset/13262267483
	},
}

local QuestGamepadLabelsInfo = {
	{
		labelKey = "ResetCameraLabel",
		xPosition = RIGHT_ALIGNED_QUEST_GAMEPAD_LABELS_X,
		yPosition = 0.335,
		xAlignment = Enum.TextXAlignment.Right,
		width = QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH,
	},
	{
		labelKey = "MenuLabel",
		xPosition = RIGHT_ALIGNED_QUEST_GAMEPAD_LABELS_X,
		yPosition = 0.397,
		xAlignment = Enum.TextXAlignment.Right,
		width = QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH,
	},
	{
		labelKey = "SelectItemLabel",
		xPosition = 0.442,
		yPosition = 0.621,
		xAlignment = Enum.TextXAlignment.Center,
		width = 0.09,
	},
	{
		labelKey = "BackExitLabel",
		xPosition = LEFT_ALIGNED_QUEST_GAMEPAD_LABELS_X,
		yPosition = 0.287,
		xAlignment = Enum.TextXAlignment.Left,
		width = QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH,
	},
	{
		labelKey = "FirstPersonCameraLabel",
		xPosition = LEFT_ALIGNED_QUEST_GAMEPAD_LABELS_X,
		yPosition = 0.342,
		xAlignment = Enum.TextXAlignment.Left,
		width = QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH,
	},
	{
		labelKey = "JumpSelectLabel",
		xPosition = LEFT_ALIGNED_QUEST_GAMEPAD_LABELS_X,
		yPosition = 0.413,
		xAlignment = Enum.TextXAlignment.Left,
		width = QUEST_GAMEPAD_LABELS_ON_SIDES_WIDTH,
	},
}

return {
	Images = Images,
	Labels = {
		QuestGamepadLabels = {
			LabelRelativeTextHeight = 0.026,
			LocalizationKeyPrefix = "CoreScripts.InGameMenu.Controls.",
			LabelsInfo = QuestGamepadLabelsInfo,
		},
	},
}
