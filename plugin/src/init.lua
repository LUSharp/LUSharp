-- LUSharp Studio Plugin
-- Entry point

local toolbar = plugin:CreateToolbar("LUSharp")
local buildButton = toolbar:CreateButton(
    "Build", "Compile C# to Luau", "rbxassetid://0", "Build"
)
local buildAllButton = toolbar:CreateButton(
    "BuildAll", "Compile all C# scripts", "rbxassetid://0", "Build All"
)
local newScriptButton = toolbar:CreateButton(
    "NewScript", "Create a new C# script", "rbxassetid://0", "New C# Script"
)

print("[LUSharp] Plugin loaded")
