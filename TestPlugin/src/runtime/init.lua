--!strict
-- LUSharp .NET BCL Runtime for Luau
-- Provides .NET type equivalents used by transpiled Roslyn code

local Runtime = {}

Runtime.System = {}
Runtime.Collections = {}
Runtime.Text = {}

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
