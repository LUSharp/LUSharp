--!strict
-- Runtime stubs for System namespace types

local System = {}

-- System.String
System.String = {}

function System.String.IsNullOrEmpty(s: string?): boolean
	return s == nil or s == ""
end

function System.String.IsNullOrWhiteSpace(s: string?): boolean
	if s == nil then return true end
	return string.match(s, "^%s*$") ~= nil
end

function System.String.Concat(...: any): string
	local args = {...}
	local result = ""
	for _, v in args do
		result ..= tostring(v)
	end
	return result
end

function System.String.Join(separator: string, values: {string}): string
	return table.concat(values, separator)
end

function System.String.Format(format: string, ...: any): string
	-- Simplified: replace {0}, {1}, etc.
	local args = {...}
	local result = format
	for i, arg in args do
		result = string.gsub(result, "{" .. (i - 1) .. "}", tostring(arg))
	end
	return result
end

-- System.Char
System.Char = {}

function System.Char.IsLetter(c: number): boolean
	return (c >= 65 and c <= 90) or (c >= 97 and c <= 122)
end

function System.Char.IsDigit(c: number): boolean
	return c >= 48 and c <= 57
end

function System.Char.IsLetterOrDigit(c: number): boolean
	return System.Char.IsLetter(c) or System.Char.IsDigit(c)
end

function System.Char.IsWhiteSpace(c: number): boolean
	return c == 32 or c == 9 or c == 10 or c == 13
end

function System.Char.IsUpper(c: number): boolean
	return c >= 65 and c <= 90
end

function System.Char.IsLower(c: number): boolean
	return c >= 97 and c <= 122
end

function System.Char.ToUpper(c: number): number
	if c >= 97 and c <= 122 then return c - 32 end
	return c
end

function System.Char.ToLower(c: number): number
	if c >= 65 and c <= 90 then return c + 32 end
	return c
end

-- System.Math
System.Math = {}
System.Math.Max = math.max
System.Math.Min = math.min
System.Math.Abs = math.abs
System.Math.Floor = math.floor
System.Math.Ceiling = math.ceil

function System.Math.Clamp(value: number, min: number, max: number): number
	return math.clamp(value, min, max)
end

-- System.Convert
System.Convert = {}

function System.Convert.ToInt32(value: any): number
	return math.floor(tonumber(value) or 0)
end

function System.Convert.ToString(value: any): string
	return tostring(value)
end

return System
