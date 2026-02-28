-- Compiled by LUSharp (do not edit)
local __registry = require(script.Parent:FindFirstChild("_LUSharpRuntime"))
if __registry["NewScript2"] then return __registry["NewScript2"] end
__registry["NewScript2"] = {}

local function __resolve(name)
    if __registry[name] then return __registry[name] end
    local m = script.Parent:FindFirstChild(name)
    if m and m:IsA("ModuleScript") then return require(m) end
    for _ = 1, 300 do task.wait(0.1) if __registry[name] then return __registry[name] end end
    error("[LUSharp] Could not resolve: " .. name)
end

local NewScript = __resolve("NewScript")

local NewScript2 = {}

function NewScript2.Main()
    NewScript:SomeFunc()
end

function NewScript2.SomeFunc2()
    print("somefunc2 called in newscript2")
end

for __k, __v in pairs(NewScript2) do __registry["NewScript2"][__k] = __v end
setmetatable(__registry["NewScript2"], getmetatable(NewScript2))

task.defer(NewScript2.Main)
return __registry["NewScript2"]