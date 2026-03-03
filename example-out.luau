--!strict
-- Compiled by LUSharp (do not edit)
local NewScript2: any

local Players = game:GetService("Players")

local SomeEnum = ({
    ['State1'] = "State1";
    ['State2'] = "State2";
    ['State3'] = "State3";
})

export type SomeEnum = keyof<typeof(SomeEnum)>

type SomeStruct_self = {
    Field1: string?;
    Field2: number?;
}

local SomeStruct = {}
SomeStruct.__index = SomeStruct

export type SomeStruct = typeof(setmetatable({} :: SomeStruct_self, SomeStruct))

function SomeStruct.new(): SomeStruct
    local self = setmetatable({} :: SomeStruct_self, SomeStruct)
    return self
end

type NewScript_self = {
    newScript: any;
}

local NewScript = {}
NewScript.__index = NewScript

export type NewScript = typeof(setmetatable({} :: NewScript_self, NewScript))

function NewScript.new(): NewScript
    local self = setmetatable({
        newScript = NewScript2.new();
    } :: NewScript_self, NewScript)
    return self
end

function NewScript.Main()
    local testStruct = SomeStruct.new()
    testStruct.Field1 = "some text"
    testStruct.Field2 = 5
    print(`Hello World: {testStruct.Field1}`)
    NewScript2.SomeFunc2()
    local items = { 3, 35, 52, 5, 235, 25, 23, 523, 52, 35, 235 }
    items[1] = 5
    for _, item in items do
        print(item)
        error()
    end
    local players = Players
    players.PlayerAdded:ConnectParallel(function(p: Player) print(`{p.DisplayName} has joined the game!`) end)
end

function NewScript.SomeFunc()
    print("somefunc called in newscript")
end

function NewScript.__init(__shared: { [string]: any })
    NewScript2 = __shared.NewScript2
end

return NewScript
