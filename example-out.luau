-- Compiled by LUSharp (do not edit)
local __registry = require(script.Parent:FindFirstChild("_LUSharpRuntime"))
if __registry["NewScript"] then return __registry["NewScript"] end
__registry["NewScript"] = {}

local Players = game:GetService("Players")

local function __resolve(name)
    if __registry[name] then return __registry[name] end
    local m = script.Parent:FindFirstChild(name)
    if m and m:IsA("ModuleScript") then return require(m) end
    for _ = 1, 300 do task.wait(0.1) if __registry[name] then return __registry[name] end end
    error("[LUSharp] Could not resolve: " .. name)
end

local NewScript2 = __resolve("NewScript2")

local NewScript = {}

function NewScript.Main()
    print("Hello World")
    NewScript2:SomeFunc2()
    local items = {3, 35, 52, 5, 235, 25, 23, 523, 52, 35, 235}
    for _, item in items do
        print(item)
    end
    local players = Players
    players.PlayerAdded:ConnectParallel(function(p)
    print(`{p.DisplayName} has joined the game!`)
end)
end

function NewScript.SomeFunc()
    print("somefunc called in newscript")
end

for __k, __v in pairs(NewScript) do __registry["NewScript"][__k] = __v end
setmetatable(__registry["NewScript"], getmetatable(NewScript))

task.defer(NewScript.Main)
return __registry["NewScript"]