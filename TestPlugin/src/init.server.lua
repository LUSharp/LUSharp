--!strict
-- LUSharp TestPlugin — Validates transpiled Roslyn Luau modules
-- Compare output with: dotnet run --project LUSharpRoslynModule -- reference syntax-kind

local PLUGIN_VERSION = "0.1.0"

warn("[LUSharp-Test] TestPlugin v" .. PLUGIN_VERSION .. " loaded")

local modules = script:FindFirstChild("modules")
local runtime = script:FindFirstChild("runtime")

if not modules then
	warn("[LUSharp-Test] No modules folder found — nothing to test")
	return
end

local function runSyntaxKindTest()
	local syntaxKindModule = modules:FindFirstChild("SyntaxKind")
	if not syntaxKindModule then
		warn("[LUSharp-Test] SyntaxKind module not found, skipping")
		return
	end

	local ok, result = pcall(require, syntaxKindModule)
	if not ok then
		warn("[LUSharp-Test] FAIL: SyntaxKind failed to load: " .. tostring(result))
		return
	end

	local SyntaxKind = result.SyntaxKind
	local SyntaxKind_Name = result.SyntaxKind_Name

	if not SyntaxKind then
		warn("[LUSharp-Test] FAIL: SyntaxKind table not found in module return")
		return
	end

	-- Count members
	local count = 0
	for _ in SyntaxKind do
		count += 1
	end
	print("[LUSharp-Test] SyntaxKind member count: " .. count)

	-- Print all values for comparison with C# reference output
	-- Format: Name=Value (matches C# reference format)
	local entries: { { name: string, value: number } } = {}
	for name, value in SyntaxKind do
		table.insert(entries, { name = name, value = value })
	end

	-- Sort by value for deterministic output
	table.sort(entries, function(a, b) return a.value < b.value end)

	for _, entry in entries do
		print(entry.name .. "=" .. entry.value)
	end

	-- Spot check reverse lookup
	if SyntaxKind_Name then
		local spotValue = SyntaxKind.None
		if spotValue ~= nil and SyntaxKind_Name[spotValue] then
			print("[LUSharp-Test] Reverse lookup SyntaxKind[" .. spotValue .. "] = " .. SyntaxKind_Name[spotValue])
		end
	end

	warn("[LUSharp-Test] SyntaxKind test completed")
end

-- Run all tests
warn("[LUSharp-Test] Starting tests...")
runSyntaxKindTest()
warn("[LUSharp-Test] All tests finished")
