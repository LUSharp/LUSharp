# Transpiler


# Goals
- Server scripts go into server script service
- Shared scripts go to Replicated Storage
- Client scripts go to StarterPlayer/StarterPlayerScripts


# Implementation goals
- Backend helper types get compiled into ReplicatedStorage/rbxcs/types
- We should start with project management with rojo.



# Overal functionality goal

A core script will inherit the base class RobloxScript, and that will give it access to the custom environment. 
From the custom environment, it will have access to the Game property, which will be a DataModel instance. 
Thus you have full access to the [Roblox API.](https://create.roblox.com/docs/reference/engine)

# Output modern, fast, and secure LuaU scripts for roblox.
