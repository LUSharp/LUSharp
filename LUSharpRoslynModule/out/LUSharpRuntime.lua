--!strict
-- LUSharp Runtime Library
-- Provides helpers for C# standard library operations that cannot be inlined.

local RT = {}

-- ── Dictionary helpers ──────────────────────────────────────

function RT.keys(dict: { [any]: any }): { any }
	local result = {}
	for k, _ in dict do
		table.insert(result, k)
	end
	return result
end

function RT.values(dict: { [any]: any }): { any }
	local result = {}
	for _, v in dict do
		table.insert(result, v)
	end
	return result
end

function RT.dictCount(dict: { [any]: any }): number
	local count = 0
	for _ in dict do
		count = count + 1
	end
	return count
end

function RT.containsValue(dict: { [any]: any }, value: any): boolean
	for _, v in dict do
		if v == value then
			return true
		end
	end
	return false
end

function RT.tryGetValue(dict: { [any]: any }, key: any): (boolean, any)
	local val = dict[key]
	return val ~= nil, val
end

-- ── TryParse helpers ────────────────────────────────────────

function RT.tryParse_int(str: string): (boolean, number)
	local n = tonumber(str)
	if n ~= nil then return true, math.floor(n) else return false, 0 end
end

function RT.tryParse_double(str: string): (boolean, number)
	local n = tonumber(str)
	if n ~= nil then return true, n else return false, 0 end
end

function RT.tryParse_bool(str: string): (boolean, boolean)
	local lower = string.lower(str)
	if lower == "true" then return true, true
	elseif lower == "false" then return true, false
	else return false, false end
end

-- ── Array helpers ───────────────────────────────────────────

function RT.reverseInPlace(arr: { any })
	local n = #arr
	for i = 1, math.floor(n / 2) do
		local j = n - i + 1
		arr[i], arr[j] = arr[j], arr[i]
	end
end

function RT.findIndex(arr: { any }, pred: (any) -> boolean): number
	for i, v in arr do
		if pred(v) then
			return i - 1
		end
	end
	return -1
end

-- ── String helpers ──────────────────────────────────────────

function RT.lastIndexOf(s: string, sub: string): number
	local last = -1
	local start = 1
	while true do
		local found = string.find(s, sub, start, true)
		if found == nil then
			break
		end
		last = (found :: number) - 1
		start = (found :: number) + 1
	end
	return last
end

-- Convert a char code array slice to a Luau string
-- Equivalent to C#: new string(char[], startIndex, length)
function RT.isinstance(obj: any, className: string): boolean
	if type(obj) ~= "table" then return false end
	-- Walk the class hierarchy: instance → Class → Parent → GrandParent ...
	-- Each class C has: C.__index = C, getmetatable(C) = {__index = ParentOrProxy}
	-- ParentOrProxy may be a deferred proxy with __index metamethod
	local cls = getmetatable(obj) -- the direct class table
	local depth = 0
	while cls ~= nil and type(cls) == "table" and depth < 20 do
		depth += 1
		-- Check this class (use normal access so deferred proxies resolve via metamethods)
		if cls.__className == className then return true end
		-- Find the parent class: getmetatable(cls).__index
		local clsMt = getmetatable(cls)
		if clsMt == nil or type(clsMt) ~= "table" or clsMt == cls then break end
		local parent = rawget(clsMt, "__index")
		if parent == nil or parent == cls then break end
		if type(parent) == "table" then
			-- parent is the next class (or a deferred proxy — __className access will resolve it)
			cls = parent
		elseif type(parent) == "function" then
			-- __index is a function — try to get __className from it
			local ok, cn = pcall(parent, clsMt, "__className")
			if ok and cn == className then return true end
			break
		else
			break
		end
	end
	return false
end

function RT.stringIndexOfAny(s: string, chars: { number }): number
	for i = 1, #s do
		local b = string.byte(s, i)
		for _, c in chars do
			if b == c then return i - 1 end
		end
	end
	return -1
end

function RT.charsToString(chars: { number }, startIndex: number, length: number): string
	if chars == nil or length <= 0 then return "" end
	local parts = table.create(length)
	for i = 1, length do
		parts[i] = string.char(chars[startIndex + i] or 0)
	end
	return table.concat(parts)
end

function RT.stringSplit(s: string, delim: string, count: number): { string }
	local result = {}
	local start = 1
	local splits = 0
	while splits < count - 1 do
		local found = string.find(s, delim, start, true)
		if found == nil then
			break
		end
		table.insert(result, string.sub(s, start, (found :: number) - 1))
		start = (found :: number) + #delim
		splits = splits + 1
	end
	table.insert(result, string.sub(s, start))
	return result
end

function RT.stringFormat(fmt: string, ...: any): string
	-- Convert C# {0}, {1} placeholders to %s
	local args = { ... }
	local result = string.gsub(fmt, "{(%d+)}", function(n)
		local idx = tonumber(n) :: number
		local val = args[idx + 1]
		if val == nil then
			return ""
		end
		return tostring(val)
	end)
	return result
end

function RT.tryParse(s: string): (boolean, number?)
	local n = tonumber(s)
	return n ~= nil, n
end

-- ── LINQ operations ─────────────────────────────────────────

function RT.where(arr: { any }, pred: (any) -> boolean): { any }
	local result = {}
	for _, v in arr do
		if pred(v) then
			table.insert(result, v)
		end
	end
	return result
end

function RT.select(arr: { any }, proj: (any) -> any): { any }
	local result = {}
	for _, v in arr do
		table.insert(result, proj(v))
	end
	return result
end

function RT.selectMany(arr: { any }, proj: (any) -> { any }): { any }
	local result = {}
	for _, v in arr do
		local inner = proj(v)
		for _, iv in inner do
			table.insert(result, iv)
		end
	end
	return result
end

function RT.first(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		for _, v in arr do
			if pred(v) then
				return v
			end
		end
		error("Sequence contains no matching element")
	end
	if #arr == 0 then
		error("Sequence contains no elements")
	end
	return arr[1]
end

function RT.firstOrDefault(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		for _, v in arr do
			if pred(v) then
				return v
			end
		end
		return nil
	end
	return arr[1]
end

function RT.last(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		for i = #arr, 1, -1 do
			if pred(arr[i]) then
				return arr[i]
			end
		end
		error("Sequence contains no matching element")
	end
	if #arr == 0 then
		error("Sequence contains no elements")
	end
	return arr[#arr]
end

function RT.lastOrDefault(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		for i = #arr, 1, -1 do
			if pred(arr[i]) then
				return arr[i]
			end
		end
		return nil
	end
	return arr[#arr]
end

function RT.single(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		local found: any = nil
		local count = 0
		for _, v in arr do
			if pred(v) then
				found = v
				count = count + 1
				if count > 1 then
					error("Sequence contains more than one matching element")
				end
			end
		end
		if count == 0 then
			error("Sequence contains no matching element")
		end
		return found
	end
	if #arr ~= 1 then
		error("Sequence does not contain exactly one element")
	end
	return arr[1]
end

function RT.singleOrDefault(arr: { any }, pred: ((any) -> boolean)?): any
	if pred then
		local found: any = nil
		local count = 0
		for _, v in arr do
			if pred(v) then
				found = v
				count = count + 1
				if count > 1 then
					error("Sequence contains more than one matching element")
				end
			end
		end
		return found
	end
	if #arr > 1 then
		error("Sequence contains more than one element")
	end
	return arr[1]
end

function RT.any(arr: { any }, pred: ((any) -> boolean)?): boolean
	if pred then
		for _, v in arr do
			if pred(v) then
				return true
			end
		end
		return false
	end
	return #arr > 0
end

function RT.all(arr: { any }, pred: (any) -> boolean): boolean
	for _, v in arr do
		if not pred(v) then
			return false
		end
	end
	return true
end

function RT.count(arr: { any }, pred: ((any) -> boolean)?): number
	if pred then
		local c = 0
		for _, v in arr do
			if pred(v) then
				c = c + 1
			end
		end
		return c
	end
	return #arr
end

function RT.sum(arr: { any }, proj: ((any) -> number)?): number
	local total = 0
	for _, v in arr do
		if proj then
			total = total + proj(v)
		else
			total = total + (v :: number)
		end
	end
	return total
end

function RT.min(arr: { any }, proj: ((any) -> number)?): number
	local result = math.huge
	for _, v in arr do
		local val = if proj then proj(v) else (v :: number)
		if val < result then
			result = val
		end
	end
	return result
end

function RT.max(arr: { any }, proj: ((any) -> number)?): number
	local result = -math.huge
	for _, v in arr do
		local val = if proj then proj(v) else (v :: number)
		if val > result then
			result = val
		end
	end
	return result
end

function RT.average(arr: { any }, proj: ((any) -> number)?): number
	local total = 0
	local c = 0
	for _, v in arr do
		if proj then
			total = total + proj(v)
		else
			total = total + (v :: number)
		end
		c = c + 1
	end
	if c == 0 then
		error("Sequence contains no elements")
	end
	return total / c
end

function RT.orderBy(arr: { any }, keyFn: (any) -> any): { any }
	local result = table.clone(arr)
	table.sort(result, function(a, b)
		return keyFn(a) < keyFn(b)
	end)
	return result
end

function RT.orderByDescending(arr: { any }, keyFn: (any) -> any): { any }
	local result = table.clone(arr)
	table.sort(result, function(a, b)
		return keyFn(a) > keyFn(b)
	end)
	return result
end

function RT.thenBy(arr: { any }, keyFn: (any) -> any): { any }
	-- ThenBy is a secondary sort; for simplicity, treat as stable sort
	return RT.orderBy(arr, keyFn)
end

function RT.thenByDescending(arr: { any }, keyFn: (any) -> any): { any }
	return RT.orderByDescending(arr, keyFn)
end

function RT.groupBy(arr: { any }, keyFn: (any) -> any): { { Key: any, Values: { any } } }
	local groups: { [any]: { any } } = {}
	local order = {}
	for _, v in arr do
		local key = keyFn(v)
		if groups[key] == nil then
			groups[key] = {}
			table.insert(order, key)
		end
		table.insert(groups[key] :: { any }, v)
	end
	local result = {}
	local groupMt = { __index = { get_Key = function(self) return self.Key end } }
	for _, key in order do
		table.insert(result, setmetatable({ Key = key, Values = groups[key] :: { any } }, groupMt))
	end
	return result
end

function RT.distinct(arr: { any }): { any }
	local seen: { [any]: boolean } = {}
	local result = {}
	for _, v in arr do
		if not seen[v] then
			seen[v] = true
			table.insert(result, v)
		end
	end
	return result
end

function RT.zip(a: { any }, b: { any }, fn: ((any, any) -> any)?): { any }
	local result = {}
	local len = math.min(#a, #b)
	for i = 1, len do
		if fn then
			table.insert(result, fn(a[i], b[i]))
		else
			table.insert(result, { a[i], b[i] })
		end
	end
	return result
end

function RT.aggregate(arr: { any }, seed: any, fn: (any, any) -> any): any
	local acc = seed
	for _, v in arr do
		acc = fn(acc, v)
	end
	return acc
end

function RT.take(arr: { any }, n: number): { any }
	local result = {}
	for i = 1, math.min(n, #arr) do
		table.insert(result, arr[i])
	end
	return result
end

function RT.skip(arr: { any }, n: number): { any }
	local result = {}
	for i = n + 1, #arr do
		table.insert(result, arr[i])
	end
	return result
end

function RT.takeWhile(arr: { any }, pred: (any) -> boolean): { any }
	local result = {}
	for _, v in arr do
		if not pred(v) then
			break
		end
		table.insert(result, v)
	end
	return result
end

function RT.skipWhile(arr: { any }, pred: (any) -> boolean): { any }
	local result = {}
	local skipping = true
	for _, v in arr do
		if skipping and not pred(v) then
			skipping = false
		end
		if not skipping then
			table.insert(result, v)
		end
	end
	return result
end

function RT.concat(a: { any }, b: { any }): { any }
	local result = table.clone(a)
	table.move(b, 1, #b, #result + 1, result)
	return result
end

function RT.except(a: { any }, b: { any }): { any }
	local bSet: { [any]: boolean } = {}
	for _, v in b do
		bSet[v] = true
	end
	local result = {}
	for _, v in a do
		if not bSet[v] then
			table.insert(result, v)
		end
	end
	return result
end

function RT.intersect(a: { any }, b: { any }): { any }
	local bSet: { [any]: boolean } = {}
	for _, v in b do
		bSet[v] = true
	end
	local result = {}
	local seen: { [any]: boolean } = {}
	for _, v in a do
		if bSet[v] and not seen[v] then
			seen[v] = true
			table.insert(result, v)
		end
	end
	return result
end

function RT.union(a: { any }, b: { any }): { any }
	local seen: { [any]: boolean } = {}
	local result = {}
	for _, v in a do
		if not seen[v] then
			seen[v] = true
			table.insert(result, v)
		end
	end
	for _, v in b do
		if not seen[v] then
			seen[v] = true
			table.insert(result, v)
		end
	end
	return result
end

function RT.sequenceEqual(a: { any }, b: { any }): boolean
	if #a ~= #b then
		return false
	end
	for i = 1, #a do
		if a[i] ~= b[i] then
			return false
		end
	end
	return true
end

function RT.toDictionary(arr: { any }, keyFn: (any) -> any, valFn: ((any) -> any)?): { [any]: any }
	local result: { [any]: any } = {}
	for _, v in arr do
		local key = keyFn(v)
		result[key] = if valFn then valFn(v) else v
	end
	return result
end

function RT.toLookup(arr: { any }, keyFn: (any) -> any): { [any]: { any } }
	local result: { [any]: { any } } = {}
	for _, v in arr do
		local key = keyFn(v)
		if result[key] == nil then
			result[key] = {}
		end
		table.insert(result[key] :: { any }, v)
	end
	return result
end

function RT.toHashSet(arr: { any }): { [any]: boolean }
	local result: { [any]: boolean } = {}
	for _, v in arr do
		result[v] = true
	end
	return result
end

function RT.contains(arr: { any }, val: any): boolean
	return table.find(arr, val) ~= nil
end

-- ── Enumerable generators ───────────────────────────────────

function RT.range(start: number, count: number): { number }
	local result = {}
	for i = start, start + count - 1 do
		table.insert(result, i)
	end
	return result
end

function RT.repeat_(value: any, count: number): { any }
	local result = {}
	for _ = 1, count do
		table.insert(result, value)
	end
	return result
end

-- ── Array helpers (static Array.*) ────────────────────────────

function RT.arrayClear(arr: { any }, index: number, length: number)
	for i = index + 1, index + length do
		arr[i] = nil
	end
end

function RT.arrayFill(arr: { any }, value: any, startIndex: number, count: number)
	for i = startIndex + 1, startIndex + count do
		arr[i] = value
	end
end

function RT.lastIndexOfArray(arr: { any }, value: any): number
	for i = #arr, 1, -1 do
		if arr[i] == value then
			return i - 1
		end
	end
	return -1
end

function RT.binarySearch(arr: { any }, value: any): number
	local lo = 1
	local hi = #arr
	while lo <= hi do
		local mid = math.floor((lo + hi) / 2)
		if arr[mid] < value then
			lo = mid + 1
		elseif arr[mid] > value then
			hi = mid - 1
		else
			return mid - 1
		end
	end
	return -(lo - 1) - 1
end

function RT.arrayResize(arr: { any }, newSize: number): { any }
	local result = table.create(newSize)
	for i = 1, math.min(#arr, newSize) do
		result[i] = arr[i]
	end
	return result
end

-- ── List helpers ──────────────────────────────────────────────

function RT.removeAll(arr: { any }, pred: (any) -> boolean): number
	local removed = 0
	local i = 1
	while i <= #arr do
		if pred(arr[i]) then
			table.remove(arr, i)
			removed = removed + 1
		else
			i = i + 1
		end
	end
	return removed
end

function RT.removeRange(arr: { any }, index: number, count: number)
	for _ = 1, count do
		table.remove(arr, index + 1)
	end
end

function RT.getRange(arr: { any }, index: number, count: number): { any }
	local result = {}
	for i = index + 1, index + count do
		table.insert(result, arr[i])
	end
	return result
end

-- ── HashSet helpers ───────────────────────────────────────────

function RT.unionWith(set: { [any]: boolean }, other: { [any]: boolean })
	for k, _ in other do
		set[k] = true
	end
end

function RT.intersectWith(set: { [any]: boolean }, other: { [any]: boolean })
	for k, _ in set do
		if not other[k] then
			set[k] = nil
		end
	end
end

function RT.exceptWith(set: { [any]: boolean }, other: { [any]: boolean })
	for k, _ in other do
		set[k] = nil
	end
end

function RT.symmetricExceptWith(set: { [any]: boolean }, other: { [any]: boolean })
	for k, _ in other do
		if set[k] then
			set[k] = nil
		else
			set[k] = true
		end
	end
end

function RT.isSubsetOf(set: { [any]: boolean }, other: { [any]: boolean }): boolean
	for k, _ in set do
		if not other[k] then
			return false
		end
	end
	return true
end

function RT.isSupersetOf(set: { [any]: boolean }, other: { [any]: boolean }): boolean
	for k, _ in other do
		if not set[k] then
			return false
		end
	end
	return true
end

function RT.overlaps(set: { [any]: boolean }, other: { [any]: boolean }): boolean
	for k, _ in other do
		if set[k] then
			return true
		end
	end
	return false
end

function RT.setEquals(set: { [any]: boolean }, other: { [any]: boolean }): boolean
	for k, _ in set do
		if not other[k] then
			return false
		end
	end
	for k, _ in other do
		if not set[k] then
			return false
		end
	end
	return true
end

-- ── Stopwatch helper ──────────────────────────────────────────

RT.Stopwatch = {}

function RT.Stopwatch.StartNew(): { _start: number, _elapsed: number, _running: boolean }
	return { _start = os.clock(), _elapsed = 0, _running = true }
end

function RT.Stopwatch.Start(sw: any)
	if not sw._running then
		sw._start = os.clock()
		sw._running = true
	end
end

function RT.Stopwatch.Stop(sw: any)
	if sw._running then
		sw._elapsed = sw._elapsed + (os.clock() - sw._start)
		sw._running = false
	end
end

function RT.Stopwatch.Reset(sw: any)
	sw._elapsed = 0
	sw._running = false
end

function RT.Stopwatch.Restart(sw: any)
	sw._elapsed = 0
	sw._start = os.clock()
	sw._running = true
end

function RT.Stopwatch.GetElapsedMilliseconds(sw: any): number
	local elapsed = sw._elapsed
	if sw._running then
		elapsed = elapsed + (os.clock() - sw._start)
	end
	return elapsed * 1000
end

function RT.Stopwatch.GetElapsedSeconds(sw: any): number
	local elapsed = sw._elapsed
	if sw._running then
		elapsed = elapsed + (os.clock() - sw._start)
	end
	return elapsed
end

function RT.Stopwatch.GetTimestamp(): number
	return os.clock()
end

-- ── Guid helper ───────────────────────────────────────────────

function RT.newGuid(): string
	local template = "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx"
	local result = string.gsub(template, "[xy]", function(c)
		local v = if c == "x" then math.random(0, 15) else math.random(8, 11)
		return string.format("%x", v)
	end)
	return result
end

-- ── Async helpers ───────────────────────────────────────────

function RT.whenAll(tasks: { any })
	-- In Roblox, tasks are coroutines managed by task.spawn
	-- WhenAll waits for all spawned threads to complete
	local threads = {}
	local completed = 0
	local total = #tasks

	for _, t in tasks do
		local co = coroutine.create(function()
			t()
			completed = completed + 1
		end)
		table.insert(threads, co)
		task.spawn(co)
	end

	while completed < total do
		task.wait()
	end
end

function RT.whenAny(tasks: { any })
	local done = false
	for _, t in tasks do
		task.spawn(function()
			t()
			done = true
		end)
	end
	while not done do
		task.wait()
	end
end

RT.CancellationToken = {}

function RT.CancellationToken.new()
	return {
		IsCancellationRequested = false,
		Cancel = function(self: any)
			self.IsCancellationRequested = true
		end,
		ThrowIfCancellationRequested = function(self: any)
			if self.IsCancellationRequested then
				error("OperationCanceledException")
			end
		end,
	}
end

-- ── Base64 encoding/decoding ──────────────────────────────────
local b64chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

function RT.toBase64(data: { number }): string
	local result = {}
	local n = #data
	for i = 1, n, 3 do
		local a, b, c = data[i], data[i + 1] or 0, data[i + 2] or 0
		local triplet = a * 65536 + b * 256 + c
		table.insert(result, string.sub(b64chars, bit32.rshift(triplet, 18) + 1, bit32.rshift(triplet, 18) + 1))
		table.insert(result, string.sub(b64chars, bit32.band(bit32.rshift(triplet, 12), 63) + 1, bit32.band(bit32.rshift(triplet, 12), 63) + 1))
		table.insert(result, if i + 1 <= n then string.sub(b64chars, bit32.band(bit32.rshift(triplet, 6), 63) + 1, bit32.band(bit32.rshift(triplet, 6), 63) + 1) else "=")
		table.insert(result, if i + 2 <= n then string.sub(b64chars, bit32.band(triplet, 63) + 1, bit32.band(triplet, 63) + 1) else "=")
	end
	return table.concat(result)
end

function RT.fromBase64(s: string): { number }
	local result = {}
	local lookup = {}
	for i = 1, 64 do lookup[string.sub(b64chars, i, i)] = i - 1 end
	s = string.gsub(s, "[^A-Za-z0-9+/]", "")
	for i = 1, #s, 4 do
		local a = lookup[string.sub(s, i, i)] or 0
		local b = lookup[string.sub(s, i + 1, i + 1)] or 0
		local c = lookup[string.sub(s, i + 2, i + 2)] or 0
		local d = lookup[string.sub(s, i + 3, i + 3)] or 0
		local triplet = a * 262144 + b * 4096 + c * 64 + d
		table.insert(result, bit32.band(bit32.rshift(triplet, 16), 255))
		if i + 2 <= #s then table.insert(result, bit32.band(bit32.rshift(triplet, 8), 255)) end
		if i + 3 <= #s then table.insert(result, bit32.band(triplet, 255)) end
	end
	return result
end

-- ── Encoding (UTF-8) ──

RT.Encoding = {}
RT.Encoding.UTF8 = {}

function RT.Encoding.UTF8.GetBytes(s: string): { number }
	local result = {}
	for i = 1, #s do
		table.insert(result, string.byte(s, i))
	end
	return result
end

function RT.Encoding.UTF8.GetByteCount(s: string): number
	return #s
end

function RT.Encoding.UTF8.GetChars(bytes: { number }, offset: number, count: number, chars: { number }?, charsOffset: number?): number
	if chars then
		for i = 0, count - 1 do
			chars[(charsOffset or 0) + i + 1] = bytes[offset + i + 1]
		end
	end
	return count
end

function RT.Encoding.UTF8.GetMaxCharCount(byteCount: number): number
	return byteCount
end

function RT.Encoding.UTF8.GetString(bytes: { number }, offset: number?, count: number?): string
	offset = offset or 0
	count = count or #bytes
	local chars = {}
	for i = 1, count do
		table.insert(chars, string.char(bytes[offset + i]))
	end
	return table.concat(chars)
end

-- ── Expression tree stubs (for reflection-heavy code) ──

RT.Expression = {}

function RT.Expression.Constant(value: any, type: any?): any
	return { NodeType = 9, Value = value, Type = type }
end

function RT.Expression.Convert(expr: any, type: any): any
	return { NodeType = 10, Operand = expr, Type = type }
end

function RT.Expression.Call(method: any, args: { any }?): any
	return { NodeType = 6, Method = method, Arguments = args or {} }
end

function RT.Expression.New(ctor: any, args: { any }?): any
	return { NodeType = 31, Constructor = ctor, Arguments = args or {} }
end

function RT.Expression.Parameter(type: any, name: string?): any
	return { NodeType = 38, Type = type, Name = name }
end

function RT.Expression.Variable(type: any, name: string?): any
	return RT.Expression.Parameter(type, name)
end

function RT.Expression.Assign(left: any, right: any): any
	return { NodeType = 46, Left = left, Right = right }
end

function RT.Expression.ArrayIndex(array: any, index: any): any
	return { NodeType = 5, Array = array, Index = index }
end

function RT.Expression.Block(variables: { any }?, expressions: { any }): any
	return { NodeType = 47, Variables = variables or {}, Expressions = expressions }
end

function RT.Expression.Lambda(body: any, params: { any }?): any
	return { NodeType = 18, Body = body, Parameters = params or {}, Compile = function() return function() end end }
end

function RT.Expression.Condition(test: any, ifTrue: any, ifFalse: any): any
	return { NodeType = 8, Test = test, IfTrue = ifTrue, IfFalse = ifFalse }
end

function RT.Expression.Throw(value: any, type: any?): any
	return { NodeType = 60, Value = value, Type = type }
end

function RT.Expression.Default(type: any): any
	return { NodeType = 51, Type = type }
end

function RT.Expression.Empty(): any
	return { NodeType = 52 }
end

function RT.Expression.Equal(left: any, right: any): any
	return { NodeType = 13, Left = left, Right = right }
end

function RT.Expression.Field(expression: any, field: any): any
	return { NodeType = 23, Expression = expression, Member = field }
end

function RT.Expression.MakeMemberAccess(expression: any, member: any): any
	return { NodeType = 23, Expression = expression, Member = member }
end

function RT.Expression.NewArrayInit(type: any, initializers: { any }?): any
	return { NodeType = 33, Type = type, Expressions = initializers or {} }
end

function RT.Expression.TypeIs(expression: any, type: any): any
	return { NodeType = 45, Expression = expression, TypeOperand = type }
end

function RT.Expression.Unbox(expression: any, type: any): any
	return { NodeType = 44, Operand = expression, Type = type }
end

-- ── ILGenerator / DynamicMethod stubs ──

RT.DynamicMethod = {}

function RT.DynamicMethod.new(name: string, returnType: any, paramTypes: { any }?, owner: any?): any
	return { Name = name, _instructions = {} }
end

function RT.DynamicMethod.GetILGenerator(self: any): any
	return {
		Emit = function() end,
		DeclareLocal = function() return {} end,
		DefineLabel = function() return {} end,
		MarkLabel = function() end,
		BeginCatchBlock = function() end,
		EndExceptionBlock = function() end,
	}
end

function RT.DynamicMethod.CreateDelegate(self: any, delegateType: any): any
	return function() end
end

-- ── IO stubs: StringReader / StringWriter ──

RT.StringReader = {}
RT.StringReader.__index = RT.StringReader

function RT.StringReader.new(s: string): any
	return setmetatable({ _s = s, _pos = 0, _length = #s }, RT.StringReader)
end

function RT.StringReader.Read(self: any, buffer: { number }, index: number, count: number): number
	local remaining = self._length - self._pos
	if remaining <= 0 then return 0 end
	if count > remaining then count = remaining end
	local i = 0
	while i < count do
		buffer[index + i + 1] = string.byte(self._s, self._pos + i + 1)
		i += 1
	end
	self._pos += count
	return count
end

function RT.StringReader.Peek(self: any): number
	if self._pos >= self._length then return -1 end
	return string.byte(self._s, self._pos + 1)
end

function RT.StringReader.ReadLine(self: any): string?
	if self._pos >= self._length then return nil end
	local start = self._pos
	while self._pos < self._length do
		local ch = string.byte(self._s, self._pos + 1)
		if ch == 13 or ch == 10 then
			local line = string.sub(self._s, start + 1, self._pos)
			self._pos += 1
			if ch == 13 and self._pos < self._length and string.byte(self._s, self._pos + 1) == 10 then
				self._pos += 1
			end
			return line
		end
		self._pos += 1
	end
	return string.sub(self._s, start + 1)
end

function RT.StringReader.Close(self: any) end

RT.StringWriter = {}
RT.StringWriter.__index = RT.StringWriter

function RT.StringWriter.__tostring(self: any): string
	return table.concat(self._parts)
end

function RT.StringWriter.new(...: any): any
	return setmetatable({ _parts = {}, _length = 0, NewLine = "\r\n" }, RT.StringWriter)
end

function RT.StringWriter.Write(self: any, value: any, index: number?, count: number?): ()
	if type(value) == "string" then
		table.insert(self._parts, value)
		self._length += #value
	elseif type(value) == "number" then
		if index ~= nil and count ~= nil then
			-- Write(chars: {number}, index, count) - but first arg is table; see Write_chars below
			error("Use Write_chars for buffer writes")
		end
		-- Single char code
		local s = string.char(value)
		table.insert(self._parts, s)
		self._length += 1
	elseif type(value) == "table" and index ~= nil and count ~= nil then
		-- Write(chars: {number}, index: number, count: number)
		local parts = {}
		local i = 0
		while i < count do
			table.insert(parts, string.char(value[index + i + 1]))
			i += 1
		end
		local s = table.concat(parts)
		table.insert(self._parts, s)
		self._length += count
	elseif value ~= nil then
		local s = tostring(value)
		table.insert(self._parts, s)
		self._length += #s
	end
end

function RT.StringWriter.WriteLine(self: any, s: string?): ()
	if s then table.insert(self._parts, s) self._length += #s end
	table.insert(self._parts, "\n")
	self._length += 1
end

function RT.StringWriter.ToString(self: any): string
	return table.concat(self._parts)
end

-- Overload aliases used by transpiled code (C# TextWriter.Write overloads)
function RT.StringWriter.Write_Char(self: any, charCode: number): ()
	table.insert(self._parts, string.char(charCode))
	self._length += 1
end

function RT.StringWriter.Write_String(self: any, s: string): ()
	if s ~= nil then
		table.insert(self._parts, s)
		self._length += #s
	end
end

function RT.StringWriter.Write_(self: any, chars: { number }, index: number, count: number): ()
	local parts = {}
	local i = 0
	while i < count do
		table.insert(parts, string.char(chars[index + i + 1]))
		i += 1
	end
	local s = table.concat(parts)
	table.insert(self._parts, s)
	self._length += count
end

function RT.StringWriter.Close(self: any) end
function RT.StringWriter.Flush(self: any) end

-- === Collection / KeyedCollection base classes ===
-- Collection<T>: simple list wrapper with Items array
local Collection = {}
Collection.__index = Collection
RT.Collection = Collection

function Collection.new(): any
	local self = setmetatable({}, Collection)
	self.Items = {}
	return self
end

function Collection.get_Count(self: any): number
	return #self.Items
end

function Collection.Add(self: any, item: any): ()
	self:InsertItem(#self.Items, item)
end

function Collection.Insert(self: any, index: number, item: any): ()
	self:InsertItem(index, item)
end

function Collection.InsertItem(self: any, index: number, item: any): ()
	table.insert(self.Items, index + 1, item)
end

function Collection.Remove(self: any, item: any): boolean
	local idx = table.find(self.Items, item)
	if idx then
		self:RemoveItem(idx - 1)
		return true
	end
	return false
end

function Collection.RemoveItem(self: any, index: number): ()
	table.remove(self.Items, index + 1)
end

function Collection.SetItem(self: any, index: number, item: any): ()
	self.Items[index + 1] = item
end

function Collection.ClearItems(self: any): ()
	table.clear(self.Items)
end

function Collection.Clear(self: any): ()
	self:ClearItems()
end

Collection.__len = function(self: any)
	return #self.Items
end

Collection.__iter = function(self: any)
	return next, self.Items
end

-- KeyedCollection<TKey, TItem>: Collection + dictionary lookup by key
-- Subclasses must define GetKeyForItem(self, item) → key
local KeyedCollection = setmetatable({}, { __index = Collection })
KeyedCollection.__index = KeyedCollection
RT.KeyedCollection = KeyedCollection

function KeyedCollection.new(): any
	local self = setmetatable({}, KeyedCollection)
	self.Items = {}
	self.Dictionary = {}
	return self
end

function KeyedCollection.Add(self: any, item: any): ()
	table.insert(self.Items, item)
	if self.GetKeyForItem then
		local key = self:GetKeyForItem(item)
		if key ~= nil then
			self.Dictionary[key] = item
		end
	end
end

function KeyedCollection.Remove(self: any, item: any): boolean
	if self.GetKeyForItem then
		local key = self:GetKeyForItem(item)
		if key ~= nil then self.Dictionary[key] = nil end
	end
	local idx = table.find(self.Items, item)
	if idx then table.remove(self.Items, idx) return true end
	return false
end

function KeyedCollection.Contains(self: any, key: string): boolean
	return self.Dictionary[key] ~= nil
end

function KeyedCollection.Clear(self: any): ()
	table.clear(self.Items)
	table.clear(self.Dictionary)
end

KeyedCollection.__len = function(self: any)
	return #self.Items
end

KeyedCollection.__iter = function(self: any)
	return next, self.Items
end

-- === .NET Type helpers ===
-- Maps Luau typeof() results to C# type names for interop with .NET type maps
local _luauToCSharpType = {
	["number"] = "double",
	["string"] = "string",
	["boolean"] = "bool",
	["table"] = "Object",
	["nil"] = "Object",
	["function"] = "Object",
}

function RT.getType(value: any): string
	local luauType = typeof(value)
	if luauType == "number" then
		-- Distinguish int vs double: whole numbers map to "int" (C# Int32)
		if value == math.floor(value) and value == value then
			return "int"
		end
		return "double"
	end
	return _luauToCSharpType[luauType] or luauType
end

-- === .NET Enum reflection stubs ===
-- Works with Luau table-based enums: { Name1 = 0, Name2 = 1, ... }
function RT.Enum_GetNames(enumType: any): { string }
	local names = {}
	if type(enumType) == "table" then
		for k, v in enumType do
			if type(k) == "string" and type(v) == "number" then
				table.insert(names, k)
			end
		end
	end
	return names
end

function RT.Enum_GetValues(enumType: any): { number }
	local values = {}
	if type(enumType) == "table" then
		for k, v in enumType do
			if type(k) == "string" and type(v) == "number" then
				table.insert(values, v)
			end
		end
		table.sort(values)
	end
	return values
end

function RT.Enum_GetName(enumType: any, value: any): string?
	if type(enumType) == "table" then
		for k, v in enumType do
			if v == value then return k end
		end
	end
	return nil
end

function RT.Enum_IsDefined(enumType: any, value: any): boolean
	if type(enumType) == "table" then
		for _, v in enumType do
			if v == value then return true end
		end
	end
	return false
end

function RT.Enum_Parse(enumType: any, name: string): any
	if type(enumType) == "table" then
		return enumType[name]
	end
	return nil
end

-- === .NET Type/FieldInfo reflection stubs ===
-- Type.GetField(enumTable, name) → returns a stub { _value = enumTable[name], _name = name }
function RT.Type_GetField(typeObj: any, name: string): any
	if type(typeObj) == "table" and typeObj[name] ~= nil then
		return { _value = typeObj[name], _name = name, GetValue = function(self, _) return self._value end, GetCustomAttributes = function(self, ...) return {} end }
	end
	return nil
end

function RT.FieldInfo_GetValue(field: any): any
	if type(field) == "table" and field._value ~= nil then
		return field._value
	end
	return nil
end

function RT.Type_IsEnum(typeObj: any): boolean
	if type(typeObj) == "table" then
		for k, v in typeObj do
			if type(k) == "string" and type(v) == "number" then return true end
		end
	end
	return false
end

-- Expose .NET base collection types as globals for transpiled modules
-- that inherit from them (e.g., JsonPropertyCollection : KeyedCollection)
rawset(_G, "KeyedCollection", RT.KeyedCollection)
rawset(_G, "Collection", RT.Collection)

-- ── IList<T> runtime helpers ──────────────────────────────────
-- Custom IList<T> implementations (e.g. JPropertyList) use method dispatch;
-- plain tables (List<T>) use native table operations as fallback.

local function hasMethod(obj, name)
	if obj == nil then return false end
	-- Traverse full __index chain via normal table access
	local val = obj[name]
	return type(val) == "function"
end

function RT.ilistInsert(list: any, index: number, item: any)
	if list == nil then return end
	if hasMethod(list, "Insert") then
		list:Insert(index, item)
	else
		table.insert(list, index + 1, item)
	end
end

function RT.ilistAdd(list: any, item: any)
	if list == nil then return end
	if hasMethod(list, "Add") then
		list:Add(item)
	else
		table.insert(list, item)
	end
end

function RT.ilistRemoveAt(list: any, index: number)
	if list == nil then return end
	if hasMethod(list, "RemoveAt") then
		list:RemoveAt(index)
	else
		table.remove(list, index + 1)
	end
end

function RT.ilistContains(list: any, item: any): boolean
	if list == nil then return false end
	if hasMethod(list, "Contains") then
		return list:Contains(item)
	end
	return table.find(list, item) ~= nil
end

function RT.ilistIndexOf(list: any, item: any): number
	if list == nil then return -1 end
	if hasMethod(list, "IndexOf") then
		return list:IndexOf(item)
	end
	return (table.find(list, item) or 0) - 1
end

function RT.ilistClear(list: any)
	if list == nil then return end
	if hasMethod(list, "Clear") then
		list:Clear()
	else
		table.clear(list)
	end
end

function RT.ilistCount(list: any): number
	if list == nil then return 0 end
	if hasMethod(list, "get_Count") then
		return list:get_Count()
	end
	return #list
end

function RT.ilistGet(list: any, index: number): any
	if list == nil then return nil end
	-- Collection<T> pattern: numeric items are in .Items, not in __index_get
	-- (__index_get may be a string-keyed indexer like JPropertyKeyedCollection)
	if type(index) == "number" and list.Items ~= nil then
		return list.Items[index + 1]
	end
	if hasMethod(list, "__index_get") then
		return list:__index_get(index)
	end
	return list[index + 1]
end

function RT.ilistSet(list: any, index: number, value: any)
	if list == nil then return end
	-- Collection<T> pattern: numeric items are in .Items
	if type(index) == "number" and list.Items ~= nil then
		list.Items[index + 1] = value
		return
	end
	if hasMethod(list, "__index_set") then
		list:__index_set(index, value)
	else
		list[index + 1] = value
	end
end

return RT
