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
	for _, key in order do
		table.insert(result, { Key = key, Values = groups[key] :: { any } })
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

return RT
