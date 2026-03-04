--!strict
-- Runtime stubs for System.Collections.Generic types

local Collections = {}

-- List<T> implementation
local List = {}
List.__index = List

export type List<T> = {
	_items: {T},
	Count: number,
	Add: (self: List<T>, item: T) -> (),
	Remove: (self: List<T>, item: T) -> boolean,
	RemoveAt: (self: List<T>, index: number) -> (),
	Insert: (self: List<T>, index: number, item: T) -> (),
	Contains: (self: List<T>, item: T) -> boolean,
	IndexOf: (self: List<T>, item: T) -> number,
	Clear: (self: List<T>) -> (),
	Sort: (self: List<T>, comparer: ((T, T) -> boolean)?) -> (),
	ToArray: (self: List<T>) -> {T},
}

function Collections.List_new(): any
	local self = setmetatable({}, List)
	self._items = {}
	self.Count = 0
	return self
end

function List:Add(item: any)
	table.insert(self._items, item)
	self.Count = #self._items
end

function List:Remove(item: any): boolean
	for i, v in self._items do
		if v == item then
			table.remove(self._items, i)
			self.Count = #self._items
			return true
		end
	end
	return false
end

function List:RemoveAt(index: number)
	table.remove(self._items, index + 1) -- 0-based to 1-based
	self.Count = #self._items
end

function List:Insert(index: number, item: any)
	table.insert(self._items, index + 1, item) -- 0-based to 1-based
	self.Count = #self._items
end

function List:Contains(item: any): boolean
	for _, v in self._items do
		if v == item then return true end
	end
	return false
end

function List:IndexOf(item: any): number
	for i, v in self._items do
		if v == item then return i - 1 end -- 1-based to 0-based
	end
	return -1
end

function List:Clear()
	table.clear(self._items)
	self.Count = 0
end

function List:Sort(comparer: ((any, any) -> boolean)?)
	if comparer then
		table.sort(self._items, comparer)
	else
		table.sort(self._items)
	end
end

function List:ToArray(): {any}
	local result = table.create(#self._items)
	for i, v in self._items do
		result[i] = v
	end
	return result
end

-- Allow indexing: list[i] (0-based)
function List:__index(key: any): any
	if type(key) == "number" then
		return self._items[key + 1]
	end
	return List[key]
end

function List:__newindex(key: any, value: any)
	if type(key) == "number" then
		self._items[key + 1] = value
	else
		rawset(self, key, value)
	end
end

function List:__len(): number
	return self.Count
end

-- Dictionary<K,V> implementation
local Dictionary = {}
Dictionary.__index = Dictionary

function Collections.Dictionary_new(): any
	local self = setmetatable({}, Dictionary)
	self._entries = {}
	self.Count = 0
	return self
end

function Dictionary:Add(key: any, value: any)
	if self._entries[key] == nil then
		self.Count += 1
	end
	self._entries[key] = value
end

function Dictionary:Remove(key: any): boolean
	if self._entries[key] ~= nil then
		self._entries[key] = nil
		self.Count -= 1
		return true
	end
	return false
end

function Dictionary:ContainsKey(key: any): boolean
	return self._entries[key] ~= nil
end

function Dictionary:TryGetValue(key: any): (boolean, any)
	local value = self._entries[key]
	if value ~= nil then
		return true, value
	end
	return false, nil
end

function Dictionary:Clear()
	table.clear(self._entries)
	self.Count = 0
end

function Dictionary:Keys(): {any}
	local keys = {}
	for k in self._entries do
		table.insert(keys, k)
	end
	return keys
end

function Dictionary:Values(): {any}
	local values = {}
	for _, v in self._entries do
		table.insert(values, v)
	end
	return values
end

-- HashSet<T> implementation
local HashSet = {}
HashSet.__index = HashSet

function Collections.HashSet_new(): any
	local self = setmetatable({}, HashSet)
	self._set = {}
	self.Count = 0
	return self
end

function HashSet:Add(item: any): boolean
	if self._set[item] then return false end
	self._set[item] = true
	self.Count += 1
	return true
end

function HashSet:Remove(item: any): boolean
	if not self._set[item] then return false end
	self._set[item] = nil
	self.Count -= 1
	return true
end

function HashSet:Contains(item: any): boolean
	return self._set[item] == true
end

function HashSet:Clear()
	table.clear(self._set)
	self.Count = 0
end

-- StringBuilder implementation
local StringBuilder = {}
StringBuilder.__index = StringBuilder

function Collections.StringBuilder_new(): any
	local self = setmetatable({}, StringBuilder)
	self._parts = {}
	return self
end

function StringBuilder:Append(value: any): any
	table.insert(self._parts, tostring(value))
	return self
end

function StringBuilder:AppendLine(value: any?): any
	if value ~= nil then
		table.insert(self._parts, tostring(value))
	end
	table.insert(self._parts, "\n")
	return self
end

function StringBuilder:ToString(): string
	return table.concat(self._parts)
end

function StringBuilder:Clear(): any
	table.clear(self._parts)
	return self
end

function StringBuilder:__len(): number
	local len = 0
	for _, part in self._parts do
		len += #part
	end
	return len
end

return Collections
