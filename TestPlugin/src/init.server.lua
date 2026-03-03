--!strict
-- LUSharp TestPlugin — Validates transpiled Roslyn Luau modules
-- Compare output with: dotnet run --project LUSharpRoslynModule -- reference <command>

local PLUGIN_VERSION = "0.2.0"

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

local function runSyntaxFactsTest()
	-- Load SyntaxKind first (SyntaxFacts depends on it)
	local syntaxKindModule = modules:FindFirstChild("SyntaxKind")
	if not syntaxKindModule then
		warn("[LUSharp-Test] SyntaxKind module not found, skipping SyntaxFacts test")
		return
	end

	local syntaxFactsModule = modules:FindFirstChild("SyntaxFacts")
	if not syntaxFactsModule then
		warn("[LUSharp-Test] SyntaxFacts module not found, skipping")
		return
	end

	local ok1, skResult = pcall(require, syntaxKindModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxKind failed to load: " .. tostring(skResult))
		return
	end

	local ok2, sfResult = pcall(require, syntaxFactsModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SyntaxFacts failed to load: " .. tostring(sfResult))
		return
	end

	local SyntaxKind = skResult.SyntaxKind
	local SyntaxFacts = sfResult.SyntaxFacts

	if not SyntaxFacts then
		warn("[LUSharp-Test] FAIL: SyntaxFacts table not found in module return")
		return
	end

	-- Helper: bool to C#-style string
	local function b(v: boolean): string
		return v and "True" or "False"
	end

	-- === Character Classification ===
	print("=== Character Classification ===")
	local testChars = {
		string.byte("0"), string.byte("9"),
		string.byte("a"), string.byte("f"), string.byte("g"),
		string.byte("A"), string.byte("F"), string.byte("G"),
		string.byte(" "), string.byte("\t"), string.byte("\n"), string.byte("\r"),
		string.byte("z"),
	}
	for _, c in testChars do
		print("char=" .. c
			.. "|hex=" .. b(SyntaxFacts.IsHexDigit(c))
			.. "|dec=" .. b(SyntaxFacts.IsDecDigit(c))
			.. "|bin=" .. b(SyntaxFacts.IsBinaryDigit(c))
			.. "|ws=" .. b(SyntaxFacts.IsWhitespace(c))
			.. "|nl=" .. b(SyntaxFacts.IsNewLine(c)))
	end

	-- === Hex Values ===
	print("=== Hex Values ===")
	local hexChars = {
		string.byte("0"), string.byte("5"), string.byte("9"),
		string.byte("a"), string.byte("c"), string.byte("f"),
		string.byte("A"), string.byte("C"), string.byte("F"),
	}
	for _, c in hexChars do
		print("char=" .. c
			.. "|hexval=" .. SyntaxFacts.HexValue(c)
			.. "|decval=" .. SyntaxFacts.DecValue(c))
	end

	-- === Keyword Classification ===
	print("=== Keyword Classification ===")
	local classKinds = {
		SyntaxKind.None,
		SyntaxKind.ClassKeyword,
		SyntaxKind.IntKeyword,
		SyntaxKind.IfKeyword,
		SyntaxKind.SemicolonToken,
		SyntaxKind.PlusToken,
		SyntaxKind.IdentifierToken,
		SyntaxKind.NumericLiteralToken,
		SyntaxKind.TrueKeyword,
		SyntaxKind.FalseKeyword,
		SyntaxKind.PublicKeyword,
		SyntaxKind.AsyncKeyword,
	}
	for _, kind in classKinds do
		print("kind=" .. kind
			.. "|kw=" .. b(SyntaxFacts.IsKeywordKind(kind))
			.. "|reserved=" .. b(SyntaxFacts.IsReservedKeyword(kind))
			.. "|punct=" .. b(SyntaxFacts.IsPunctuation(kind))
			.. "|literal=" .. b(SyntaxFacts.IsLiteral(kind))
			.. "|token=" .. b(SyntaxFacts.IsAnyToken(kind)))
	end

	-- === GetText ===
	print("=== GetText ===")
	local textKinds = {
		SyntaxKind.PlusToken,
		SyntaxKind.MinusToken,
		SyntaxKind.AsteriskToken,
		SyntaxKind.SlashToken,
		SyntaxKind.EqualsToken,
		SyntaxKind.SemicolonToken,
		SyntaxKind.OpenBraceToken,
		SyntaxKind.CloseBraceToken,
		SyntaxKind.ClassKeyword,
		SyntaxKind.IntKeyword,
		SyntaxKind.StringKeyword,
		SyntaxKind.BoolKeyword,
		SyntaxKind.VoidKeyword,
		SyntaxKind.ReturnKeyword,
		SyntaxKind.IfKeyword,
		SyntaxKind.ElseKeyword,
		SyntaxKind.WhileKeyword,
		SyntaxKind.ForKeyword,
		SyntaxKind.TrueKeyword,
		SyntaxKind.FalseKeyword,
		SyntaxKind.NullKeyword,
		SyntaxKind.PublicKeyword,
		SyntaxKind.PrivateKeyword,
		SyntaxKind.StaticKeyword,
	}
	for _, kind in textKinds do
		local text = SyntaxFacts.GetText(kind)
		print("kind=" .. kind .. "|text=" .. text)
	end

	-- === GetKeywordKind ===
	print("=== GetKeywordKind ===")
	local keywords = { "class", "int", "string", "bool", "void", "return", "if", "else", "while", "for", "true", "false", "null", "public", "private", "static", "new", "this" }
	for _, kw in keywords do
		local kind = SyntaxFacts.GetKeywordKind(kw)
		print("text=" .. kw .. "|kind=" .. kind)
	end

	warn("[LUSharp-Test] SyntaxFacts test completed")
end

local function runTokenizerTest()
	local slidingWindowModule = modules:FindFirstChild("SlidingTextWindow")
	local tokenizerModule = modules:FindFirstChild("SimpleTokenizer")
	local syntaxKindModule = modules:FindFirstChild("SyntaxKind")

	if not slidingWindowModule or not tokenizerModule or not syntaxKindModule then
		warn("[LUSharp-Test] Tokenizer modules not found, skipping")
		return
	end

	local ok1, skResult = pcall(require, syntaxKindModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxKind failed to load: " .. tostring(skResult))
		return
	end

	local ok2, swResult = pcall(require, slidingWindowModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SlidingTextWindow failed to load: " .. tostring(swResult))
		return
	end

	local ok3, stResult = pcall(require, tokenizerModule)
	if not ok3 then
		warn("[LUSharp-Test] FAIL: SimpleTokenizer failed to load: " .. tostring(stResult))
		return
	end

	local SyntaxKind = skResult.SyntaxKind
	local SyntaxKind_Name = skResult.SyntaxKind_Name
	local SimpleTokenizer = stResult.SimpleTokenizer

	if not SimpleTokenizer then
		warn("[LUSharp-Test] FAIL: SimpleTokenizer table not found in module return")
		return
	end

	local function printTokens(label: string, input: string)
		print("=== " .. label .. " ===")
		local tokenizer = SimpleTokenizer.new(input)
		local tokens = SimpleTokenizer.Tokenize(tokenizer)
		for _, token in tokens do
			local kindName = SyntaxKind_Name[token.Kind] or tostring(token.Kind)
			print("Kind=" .. kindName .. " Text=" .. token.Text)
		end
	end

	printTokens("Test 1: int x = 5;", "int x = 5;")
	printTokens("Test 2: if (x >= 10) { return true; }", "if (x >= 10) { return true; }")

	warn("[LUSharp-Test] Tokenizer test completed")
end

-- Run all tests
warn("[LUSharp-Test] Starting tests...")
runSyntaxKindTest()
runSyntaxFactsTest()
runTokenizerTest()
warn("[LUSharp-Test] All tests finished")
