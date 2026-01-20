export type Leave = "Leave"
export type Menu = "Menu"
export type Unibar = "Unibar"
export type Chat = "Chat"
export type Leaderboard = "Leaderboard"
export type Emotes = "Emotes"
export type Inventory = "Inventory"
export type Respawn = "Respawn"

export type GamepadMenuOptions = Leave | Menu | Unibar | Chat | Leaderboard | Emotes | Inventory | Respawn
local GamepadMenuOptions = {
	Leave = "Leave" :: Leave,
	Menu = "Menu" :: Menu,
	Unibar = "Unibar" :: Unibar,
	Chat = "Chat" :: Chat,
	Leaderboard = "Leaderboard" :: Leaderboard,
	Emotes = "Emotes" :: Emotes,
	Inventory = "Inventory" :: Inventory,
	Respawn = "Respawn" :: Respawn,
}

return GamepadMenuOptions
