--!strict
-- LUSharp Runtime Library
-- Provides .NET BCL equivalents for transpiled C# code

local Runtime = {}

-- Load sub-modules
local System = require(script.System)
local Collections = require(script.Collections)

-- Re-export
Runtime.System = System
Runtime.Collections = Collections

-- Flags helpers
Runtime.Flags = {
	HasFlag = function(value: number, flag: number): boolean
		return bit32.band(value, flag) == flag
	end,
	SetFlag = function(value: number, flag: number): number
		return bit32.bor(value, flag)
	end,
	ClearFlag = function(value: number, flag: number): number
		return bit32.band(value, bit32.bnot(flag))
	end,
}

return Runtime
