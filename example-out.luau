-- Compiled by LUSharp (do not edit)
local NewScript2

local Players = game:GetService("Players")

local SomeEnum = table.freeze({
    State1 = 0;
    State2 = 1;
    State3 = 2;
})

export type SomeEnum = keyof<typeof(SomeEnum)>

local NewScript = {}
NewScript.__index = NewScript

function NewScript.new()
    local self = setmetatable({
        newScript = NewScript2.new();
    }, NewScript)
    return self
end

function NewScript:Main()
    local testStruct = SomeStruct.new()
    testStruct.Field1 = "some text"
    testStruct.Field2 = 5
    print(`Hello World: {testStruct.Field1}`)
    NewScript2:SomeFunc2()
    local items = { 3, 35, 52, 5, 235, 25, 23, 523, 52, 35, 235 }
    items[0] = 5
    for _, item in items do
        print(item)
        error()
    end
    local players = Players
    players.PlayerAdded:ConnectParallel(function(p) print(`{p.DisplayName} has joined the game!`) end)
end

function NewScript:SomeFunc()
    print("somefunc called in newscript")
end

local SomeStruct = {}
SomeStruct.__index = SomeStruct

function SomeStruct.new()
    local self = setmetatable({
        Field1 = nil;
        Field2 = nil;
    }, SomeStruct)
    return self
end

function NewScript.__init(__shared)
    NewScript2 = __shared.NewScript2
end

return NewScript