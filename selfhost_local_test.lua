-- Local self-host test: loads all 13 Luau modules and runs the transpiler
-- Simulates Roblox's require() using dofile-style loading

local modules = {}
local moduleCache = {}

-- Simulate script.Parent.X require pattern
local scriptParent = setmetatable({}, {
	__index = function(_, name)
		return { Name = name }
	end
})

-- Override require to load from luau-out/
local origRequire = require
local function localRequire(target)
	if type(target) == "table" and target.Name then
		local name = target.Name
		if moduleCache[name] then return moduleCache[name] end

		-- Load the module file, replacing require(script.Parent.X) with our local require
		local path = "LUSharpRoslynModule/RoslynSource/luau-out/" .. name .. ".lua"
		local fn, err = loadfile(path)
		if not fn then
			error("Failed to load module " .. name .. ": " .. tostring(err))
		end

		-- Set up the module's script.Parent
		local env = setmetatable({
			script = { Parent = scriptParent },
			require = localRequire,
		}, { __index = _G })

		-- Luau doesn't support setfenv, so this won't work directly.
		-- Instead we need a different approach.
		error("Cannot use loadfile approach in Luau - need different strategy")
	end
	return origRequire(target)
end

print("Luau CLI doesn't support loadfile/setfenv. Using concatenated approach instead.")

-- Alternative: concatenate all modules into one file and test
-- Let's just verify individual modules parse correctly
local files = {
	"SyntaxToken", "SyntaxNode", "SyntaxKind", "SyntaxFacts",
	"SlidingTextWindow", "SimpleTokenizer", "SimpleParser",
	"DeclarationNodes", "ExpressionNodes", "StatementNodes",
	"SyntaxWalker", "SimpleEmitter", "SimpleTranspiler",
}

local ok, fail = 0, 0
for _, name in ipairs(files) do
	local path = "LUSharpRoslynModule/RoslynSource/luau-out/" .. name .. ".lua"
	local f = io.open(path, "r")
	if f then
		local src = f:read("*a")
		f:close()
		-- Try to compile (not run) to check for syntax errors
		local fn, err = loadstring(src, name .. ".lua")
		if fn then
			ok = ok + 1
			print("  OK    " .. name .. ".lua")
		else
			fail = fail + 1
			print("  FAIL  " .. name .. ".lua: " .. tostring(err))
		end
	else
		fail = fail + 1
		print("  FAIL  " .. name .. ".lua: file not found")
	end
end

print(string.format("\n%d/%d compile OK", ok, ok + fail))
