-- This does a shallow comparison of all props but also does a one level deep comparison of the passed in table keys
local function createShallowEqualAndTables(tableKeys: { string }): (prevProps: any, nextProps: any) -> boolean
	local tableKeysSet = {}
	for _, tableKey in tableKeys do
		tableKeysSet[tableKey] = true
	end
	
	return function(prevProps, nextProps): boolean
		for key, prevValue in prevProps do
			local nextValue = nextProps[key]

			if tableKeysSet[key] then
				if typeof(prevValue) ~= "table" or typeof(nextValue) ~= "table" then
					return false
				end
				
				for subKey, subPrevValue in prevValue do
					if subPrevValue ~= nextValue[subKey] then
						return false
					end
				end

				for subKey, _ in nextValue do
					if prevValue[subKey] == nil then
						return false
					end
				end
			elseif prevValue ~= nextValue then
				return false
			end
		end

		for key, _ in nextProps do
			if prevProps[key] == nil then
				return false
			end
		end

		return true
	end
end

return createShallowEqualAndTables
