export type VRBottomBar = "vr_bottom_bar"
export type MainMenu = "main_menu"
export type ToggleGui = "vr_toggle_button"
export type Chat = "chat"
export type Voice = "voice"
export type JoinVoice = "join_voice"
export type Safety = "vr_safety_bubble"
export type Leave = "leave_game"
export type MoreSubMenu = "nine_dot"

export type Leaderboard = "leaderboard"
export type Emotes = "emotes"
export type Inventory = "backpack"

export type VRBottomBarType = VRBottomBar | MoreSubMenu | MainMenu | ToggleGui | Chat | Voice | JoinVoice | Safety | Leave | MoreSubMenu | Leaderboard | Emotes | Inventory

local VRBottomBarType = {
	ButtomName = {
		MainMenu = "main_menu" :: MainMenu,
		ToggleGui = "vr_toggle_button" :: ToggleGui,
		Chat = "chat" :: Chat,
		Voice = "voice" :: Voice,
		JoinVoice = "join_voice" :: JoinVoice,
		Safety = "vr_safety_bubble" :: Safety,
		Leave = "leave_game" :: Leave,
		MoreSubMenu = "nine_dot" :: MoreSubMenu,
		Leaderboard = "leaderboard" :: Leaderboard,
		Emotes = "emotes" :: Emotes,
		Inventory = "backpack" :: Inventory,
	},
	Source = {
		VRBottomBar = "vr_bottom_bar" :: VRBottomBar,
		MoreSubMenu = "nine_dot" :: MoreSubMenu,
	},
}

return VRBottomBarType
