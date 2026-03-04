--!strict
-- LUSharp TestPlugin — Validates transpiled Roslyn Luau modules
-- Compare output with: dotnet run --project LUSharpRoslynModule -- reference <command>

local PLUGIN_VERSION = "0.12.0"

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

local function runParserTest()
	local syntaxKindModule = modules:FindFirstChild("SyntaxKind")
	local syntaxTokenModule = modules:FindFirstChild("SyntaxToken")
	local syntaxNodeModule = modules:FindFirstChild("SyntaxNode")
	local expressionNodesModule = modules:FindFirstChild("ExpressionNodes")
	local statementNodesModule = modules:FindFirstChild("StatementNodes")
	local declarationNodesModule = modules:FindFirstChild("DeclarationNodes")
	local simpleParserModule = modules:FindFirstChild("SimpleParser")

	if not syntaxKindModule or not syntaxTokenModule or not syntaxNodeModule
		or not expressionNodesModule or not statementNodesModule
		or not declarationNodesModule or not simpleParserModule then
		warn("[LUSharp-Test] Parser modules not found, skipping")
		return
	end

	local ok1, skResult = pcall(require, syntaxKindModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxKind failed to load: " .. tostring(skResult))
		return
	end

	local ok2, spResult = pcall(require, simpleParserModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local SimpleParser = spResult.SimpleParser

	if not SimpleParser then
		warn("[LUSharp-Test] FAIL: SimpleParser table not found in module return")
		return
	end

	-- Helper: parse input and print Accept + tree walk
	local function parseAndPrint(label: string, input: string)
		print("=== " .. label .. " ===")
		print("Input: " .. input)

		local parser = SimpleParser.new(input)
		local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

		-- Print Accept() output (reconstructed source)
		print("--- Accept Output ---")
		local acceptOutput = compilationUnit:Accept()
		-- Strip trailing newline if present
		if string.sub(acceptOutput, #acceptOutput, #acceptOutput) == "\n" then
			acceptOutput = string.sub(acceptOutput, 1, #acceptOutput - 1)
		end
		for line in string.gmatch(acceptOutput, "[^\n]*") do
			print(line)
		end

		-- Print tree walk using ToDisplayString()
		print("--- Tree Walk ---")
		print(compilationUnit:ToDisplayString())
		for _, member in compilationUnit.Members do
			print("  " .. member:ToDisplayString())
			-- Walk child members if present (class, struct, enum all have Members)
			if member.Members then
				for _, childMember in member.Members do
					print("    " .. childMember:ToDisplayString())
				end
			end
		end
	end

	-- Test 1: Basic class (existing test)
	parseAndPrint("Parser Test 1: Class",
		"class Foo { int x = 5; void Bar() { return; } }")

	-- Test 2: Enum declaration
	parseAndPrint("Parser Test 2: Enum",
		"enum Color { Red, Green = 1, Blue }")

	-- Test 3: Struct with properties
	parseAndPrint("Parser Test 3: Struct",
		"struct Point { int X { get; set; } int Y { get; set; } }")

	-- Test 4: Class with constructor
	parseAndPrint("Parser Test 4: Constructor",
		"class MyClass { int _value; MyClass(int v) { _value = v; } }")

	warn("[LUSharp-Test] Parser test completed")
end

local function runWalkerTest()
	local syntaxWalkerModule = modules:FindFirstChild("SyntaxWalker")
	local simpleParserModule = modules:FindFirstChild("SimpleParser")

	if not syntaxWalkerModule or not simpleParserModule then
		warn("[LUSharp-Test] Walker modules not found, skipping")
		return
	end

	local ok1, swResult = pcall(require, syntaxWalkerModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxWalker failed to load: " .. tostring(swResult))
		return
	end

	local ok2, spResult = pcall(require, simpleParserModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local TreePrinter = swResult.TreePrinter
	local SimpleParser = spResult.SimpleParser

	if not TreePrinter or not SimpleParser then
		warn("[LUSharp-Test] FAIL: TreePrinter or SimpleParser table not found")
		return
	end

	-- Test input matching WalkerReference.cs
	local input = "class Calculator {\n"
		.. "    int _result;\n"
		.. "\n"
		.. "    Calculator(int initial) {\n"
		.. "        _result = initial;\n"
		.. "    }\n"
		.. "\n"
		.. "    int Add(int a, int b) {\n"
		.. "        return a + b;\n"
		.. "    }\n"
		.. "\n"
		.. "    void Reset() {\n"
		.. "        _result = 0;\n"
		.. "    }\n"
		.. "\n"
		.. "    static int Max(int a, int b) {\n"
		.. "        if (a > b) {\n"
		.. "            return a;\n"
		.. "        }\n"
		.. "        return b;\n"
		.. "    }\n"
		.. "}\n"
		.. "\n"
		.. "enum Operation { Add, Subtract = 1, Multiply }"

	print("=== Walker Test ===")
	print("Input:")
	-- Print input line by line
	for line in string.gmatch(input, "[^\n]+") do
		print(line)
	end
	-- Print the blank line between class and enum
	print("")
	print("")

	-- Parse and walk
	local parser = SimpleParser.new(input)
	local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

	local printer = TreePrinter.new()
	printer:Visit(compilationUnit)

	local output = TreePrinter.GetOutput(printer)

	print("=== Tree Output ===")
	-- Print tree output, strip trailing newline
	if string.sub(output, #output, #output) == "\n" then
		output = string.sub(output, 1, #output - 1)
	end
	for line in string.gmatch(output, "[^\n]*") do
		print(line)
	end

	warn("[LUSharp-Test] Walker test completed")
end

local function runExpandedParserTest()
	local syntaxWalkerModule = modules:FindFirstChild("SyntaxWalker")
	local simpleParserModule = modules:FindFirstChild("SimpleParser")

	if not syntaxWalkerModule or not simpleParserModule then
		warn("[LUSharp-Test] Expanded parser modules not found, skipping")
		return
	end

	local ok1, swResult = pcall(require, syntaxWalkerModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxWalker failed to load: " .. tostring(swResult))
		return
	end

	local ok2, spResult = pcall(require, simpleParserModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local TreePrinter = swResult.TreePrinter
	local SimpleParser = spResult.SimpleParser

	if not TreePrinter or not SimpleParser then
		warn("[LUSharp-Test] FAIL: TreePrinter or SimpleParser table not found")
		return
	end

	-- Test input matching ExpandedParserReference.cs
	local input = "class TestClass {\n"
		.. "    void ForLoop() {\n"
		.. "        for (int i = 0; i < 10; i++) {\n"
		.. "            if (i == 5) break;\n"
		.. "            continue;\n"
		.. "        }\n"
		.. "    }\n"
		.. "    void ForEachLoop(int[] items) {\n"
		.. "        foreach (var item in items) {\n"
		.. "            int x = item;\n"
		.. "        }\n"
		.. "    }\n"
		.. "    void SwitchTest(int val) {\n"
		.. "        switch (val) {\n"
		.. "            case 1:\n"
		.. "                return;\n"
		.. "            default:\n"
		.. "                break;\n"
		.. "        }\n"
		.. "    }\n"
		.. "    void TryCatchTest() {\n"
		.. "        try {\n"
		.. "            int x = 1;\n"
		.. "        } catch (Exception ex) {\n"
		.. "            throw;\n"
		.. "        }\n"
		.. "    }\n"
		.. "    void ExpressionTests(int[] arr) {\n"
		.. "        int x = arr[0];\n"
		.. "        x++;\n"
		.. "        int y = x > 0 ? 1 : 0;\n"
		.. "        var fn = x => x + 1;\n"
		.. "        var obj = new Foo() { X = 1 };\n"
		.. "    }\n"
		.. "}"

	print("=== Expanded Parser Test ===")
	print("Input:")
	for line in string.gmatch(input, "[^\n]+") do
		print(line)
	end
	print("")

	-- Parse and walk with TreePrinter
	local parser = SimpleParser.new(input)
	local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

	local printer = TreePrinter.new()
	printer:Visit(compilationUnit)

	local output = TreePrinter.GetOutput(printer)

	print("=== Tree Output ===")
	-- Print tree output, strip trailing newline
	if string.sub(output, #output, #output) == "\n" then
		output = string.sub(output, 1, #output - 1)
	end
	for line in string.gmatch(output, "[^\n]*") do
		print(line)
	end

	warn("[LUSharp-Test] Expanded parser test completed")
end

local function runSelfParseTest()
	local syntaxWalkerModule = modules:FindFirstChild("SyntaxWalker")
	local simpleParserModule = modules:FindFirstChild("SimpleParser")

	if not syntaxWalkerModule or not simpleParserModule then
		warn("[LUSharp-Test] Self-parse modules not found, skipping")
		return
	end

	local ok1, swResult = pcall(require, syntaxWalkerModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxWalker failed to load: " .. tostring(swResult))
		return
	end

	local ok2, spResult = pcall(require, simpleParserModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local TreePrinter = swResult.TreePrinter
	local SimpleParser = spResult.SimpleParser

	if not TreePrinter or not SimpleParser then
		warn("[LUSharp-Test] FAIL: TreePrinter or SimpleParser table not found")
		return
	end

	-- Self-parse test: parse the actual SyntaxToken.cs source
	-- This proves the transpiled Luau parser can parse its own defining C# source
	local syntaxTokenSource = "namespace RoslynLuau;\n"
		.. "\n"
		.. "public struct SyntaxToken\n"
		.. "{\n"
		.. "    public int Kind { get; }\n"
		.. "    public string Text { get; }\n"
		.. "    public int Start { get; }\n"
		.. "    public int Length { get; }\n"
		.. "\n"
		.. "    public SyntaxToken(int kind, string text, int start, int length)\n"
		.. "    {\n"
		.. "        Kind = kind;\n"
		.. "        Text = text;\n"
		.. "        Start = start;\n"
		.. "        Length = length;\n"
		.. "    }\n"
		.. "\n"
		.. "    public bool IsMissing()\n"
		.. "    {\n"
		.. "        return Kind == 0;\n"
		.. "    }\n"
		.. "\n"
		.. "    public override string ToString()\n"
		.. "    {\n"
		.. "        return Text;\n"
		.. "    }\n"
		.. "}"

	print("=== Self-Parse: SyntaxToken.cs ===")

	local parser = SimpleParser.new(syntaxTokenSource)
	local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

	local printer = TreePrinter.new()
	printer:Visit(compilationUnit)

	local output = TreePrinter.GetOutput(printer)

	print("=== Tree Output ===")
	-- Print tree output, strip trailing newline
	if string.sub(output, #output, #output) == "\n" then
		output = string.sub(output, 1, #output - 1)
	end
	for line in string.gmatch(output, "[^\n]*") do
		print(line)
	end

	-- Count tree lines
	local lineCount = 0
	for _ in string.gmatch(output, "[^\n]+") do
		lineCount += 1
	end
	print("")
	print("Total tree lines: " .. lineCount)

	-- Self-parse test 2: SlidingTextWindow.cs (simplified — no unicode escapes)
	local slidingWindowSource = "namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;\n"
		.. "\n"
		.. "public struct SlidingTextWindow\n"
		.. "{\n"
		.. "    public const char InvalidCharacter = '\\0';\n"
		.. "\n"
		.. "    private readonly string _text;\n"
		.. "    private readonly int _textEnd;\n"
		.. "    public int Position;\n"
		.. "    public int LexemeStartPosition;\n"
		.. "\n"
		.. "    public int Width => Position - LexemeStartPosition;\n"
		.. "\n"
		.. "    public SlidingTextWindow(string text)\n"
		.. "    {\n"
		.. "        _text = text;\n"
		.. "        _textEnd = text.Length;\n"
		.. "        Position = 0;\n"
		.. "        LexemeStartPosition = 0;\n"
		.. "    }\n"
		.. "\n"
		.. "    public void Start()\n"
		.. "    {\n"
		.. "        LexemeStartPosition = Position;\n"
		.. "    }\n"
		.. "\n"
		.. "    public bool IsReallyAtEnd()\n"
		.. "    {\n"
		.. "        return Position >= _textEnd;\n"
		.. "    }\n"
		.. "\n"
		.. "    public char PeekChar()\n"
		.. "    {\n"
		.. "        if (Position >= _textEnd)\n"
		.. "            return InvalidCharacter;\n"
		.. "        return _text[Position];\n"
		.. "    }\n"
		.. "\n"
		.. "    public char PeekChar(int delta)\n"
		.. "    {\n"
		.. "        int pos = Position + delta;\n"
		.. "        if (pos < 0 || pos >= _textEnd)\n"
		.. "            return InvalidCharacter;\n"
		.. "        return _text[pos];\n"
		.. "    }\n"
		.. "\n"
		.. "    public char NextChar()\n"
		.. "    {\n"
		.. "        if (Position >= _textEnd)\n"
		.. "            return InvalidCharacter;\n"
		.. "        char c = _text[Position];\n"
		.. "        Position++;\n"
		.. "        return c;\n"
		.. "    }\n"
		.. "\n"
		.. "    public void AdvanceChar()\n"
		.. "    {\n"
		.. "        Position++;\n"
		.. "    }\n"
		.. "\n"
		.. "    public void AdvanceChar(int n)\n"
		.. "    {\n"
		.. "        Position += n;\n"
		.. "    }\n"
		.. "\n"
		.. "    public bool TryAdvance(char c)\n"
		.. "    {\n"
		.. "        if (PeekChar() == c)\n"
		.. "        {\n"
		.. "            AdvanceChar();\n"
		.. "            return true;\n"
		.. "        }\n"
		.. "        return false;\n"
		.. "    }\n"
		.. "\n"
		.. "    public void Reset(int position)\n"
		.. "    {\n"
		.. "        Position = position;\n"
		.. "    }\n"
		.. "\n"
		.. "    public string GetText(bool intern)\n"
		.. "    {\n"
		.. "        int length = Position - LexemeStartPosition;\n"
		.. "        if (length == 0)\n"
		.. "            return string.Empty;\n"
		.. "        return _text.Substring(LexemeStartPosition, length);\n"
		.. "    }\n"
		.. "\n"
		.. "    public int GetNewLineWidth()\n"
		.. "    {\n"
		.. "        char c = PeekChar();\n"
		.. "        if (c == '\\r')\n"
		.. "        {\n"
		.. "            if (PeekChar(1) == '\\n')\n"
		.. "                return 2;\n"
		.. "            return 1;\n"
		.. "        }\n"
		.. "        if (c == '\\n')\n"
		.. "            return 1;\n"
		.. "        return 0;\n"
		.. "    }\n"
		.. "\n"
		.. "    public void AdvancePastNewLine()\n"
		.. "    {\n"
		.. "        AdvanceChar(GetNewLineWidth());\n"
		.. "    }\n"
		.. "}"

	print("")
	print("=== Self-Parse: SlidingTextWindow.cs (simplified) ===")

	local parser2 = SimpleParser.new(slidingWindowSource)
	local compilationUnit2 = SimpleParser.ParseCompilationUnit(parser2)

	local printer2 = TreePrinter.new()
	printer2:Visit(compilationUnit2)

	local output2 = TreePrinter.GetOutput(printer2)

	print("=== Tree Output ===")
	if string.sub(output2, #output2, #output2) == "\n" then
		output2 = string.sub(output2, 1, #output2 - 1)
	end
	for line in string.gmatch(output2, "[^\n]*") do
		print(line)
	end

	local lineCount2 = 0
	for _ in string.gmatch(output2, "[^\n]+") do
		lineCount2 += 1
	end
	print("")
	print("Total tree lines: " .. lineCount2)

	warn("[LUSharp-Test] Self-parse test completed")
end

local function runFullSelfParseTest()
	local syntaxWalkerModule = modules:FindFirstChild("SyntaxWalker")
	local simpleParserModule = modules:FindFirstChild("SimpleParser")

	if not syntaxWalkerModule or not simpleParserModule then
		warn("[LUSharp-Test] Full self-parse modules not found, skipping")
		return
	end

	local ok1, swResult = pcall(require, syntaxWalkerModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SyntaxWalker failed to load: " .. tostring(swResult))
		return
	end

	local ok2, spResult = pcall(require, simpleParserModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local TreePrinter = swResult.TreePrinter
	local SimpleParser = spResult.SimpleParser

	if not TreePrinter or not SimpleParser then
		warn("[LUSharp-Test] FAIL: TreePrinter or SimpleParser table not found")
		return
	end

	print("=== Full Self-Parse Validation (Luau) ===")

	-- Source files to parse (inline, since we can't read files from disk in Roblox)
	-- These are the actual C# source files that define the parser
	local sources: { { name: string, source: string } } = {}

	table.insert(sources, {
		name = "SyntaxToken.cs",
		source = "namespace RoslynLuau;\n"
			.. "\n"
			.. "public struct SyntaxToken\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    public string Text { get; }\n"
			.. "    public int Start { get; }\n"
			.. "    public int Length { get; }\n"
			.. "\n"
			.. "    public SyntaxToken(int kind, string text, int start, int length)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "        Text = text;\n"
			.. "        Start = start;\n"
			.. "        Length = length;\n"
			.. "    }\n"
			.. "\n"
			.. "    public bool IsMissing()\n"
			.. "    {\n"
			.. "        return Kind == 0;\n"
			.. "    }\n"
			.. "\n"
			.. "    public override string ToString()\n"
			.. "    {\n"
			.. "        return Text;\n"
			.. "    }\n"
			.. "}"
	})

	table.insert(sources, {
		name = "SyntaxNode.cs",
		source = "namespace RoslynLuau;\n"
			.. "\n"
			.. "public abstract class SyntaxNode\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    public int Start { get; set; }\n"
			.. "    public int Length { get; set; }\n"
			.. "\n"
			.. "    protected SyntaxNode(int kind)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "    }\n"
			.. "\n"
			.. "    public abstract string Accept();\n"
			.. "\n"
			.. "    public virtual void AcceptWalker(SyntaxWalker walker)\n"
			.. "    {\n"
			.. "        walker.DefaultVisit(this);\n"
			.. "    }\n"
			.. "\n"
			.. "    public virtual string ToDisplayString()\n"
			.. "    {\n"
			.. '        return "SyntaxNode(" + Kind + ")";\n'
			.. "    }\n"
			.. "}\n"
			.. "\n"
			.. "public abstract class ExpressionSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected ExpressionSyntax(int kind) : base(kind)\n"
			.. "    {\n"
			.. "    }\n"
			.. "}\n"
			.. "\n"
			.. "public abstract class StatementSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected StatementSyntax(int kind) : base(kind)\n"
			.. "    {\n"
			.. "    }\n"
			.. "}\n"
			.. "\n"
			.. "public abstract class MemberDeclarationSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected MemberDeclarationSyntax(int kind) : base(kind)\n"
			.. "    {\n"
			.. "    }\n"
			.. "}"
	})

	local passed = 0
	local total = #sources

	for _, entry in sources do
		local parseOk, parseErr = pcall(function()
			local parser = SimpleParser.new(entry.source)
			local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

			local printer = TreePrinter.new()
			printer:Visit(compilationUnit)

			local output = TreePrinter.GetOutput(printer)
			local lineCount = 0
			for _ in string.gmatch(output, "[^\n]+") do
				lineCount += 1
			end

			print("  OK   " .. entry.name .. string.rep(" ", 30 - #entry.name) .. " " .. lineCount .. " tree lines")
			passed += 1
		end)

		if not parseOk then
			print("  FAIL " .. entry.name .. string.rep(" ", 30 - #entry.name) .. " " .. tostring(parseErr))
		end
	end

	print("")
	print("Self-parse: " .. passed .. "/" .. total .. " files parsed successfully")

	warn("[LUSharp-Test] Full self-parse test completed")
end

local function runEmitterTest()
	local simpleParserModule = modules:FindFirstChild("SimpleParser")
	local simpleEmitterModule = modules:FindFirstChild("SimpleEmitter")

	if not simpleParserModule or not simpleEmitterModule then
		warn("[LUSharp-Test] Emitter modules not found, skipping")
		return
	end

	local ok1, spResult = pcall(require, simpleParserModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local ok2, seResult = pcall(require, simpleEmitterModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleEmitter failed to load: " .. tostring(seResult))
		return
	end

	local SimpleParser = spResult.SimpleParser
	local SimpleEmitter = seResult.SimpleEmitter

	if not SimpleParser or not SimpleEmitter then
		warn("[LUSharp-Test] FAIL: SimpleParser or SimpleEmitter table not found")
		return
	end

	-- Same input as EmitterReference.cs
	local input = "class Calculator {\n"
		.. "    int _result;\n"
		.. "\n"
		.. "    Calculator(int initial) {\n"
		.. "        _result = initial;\n"
		.. "    }\n"
		.. "\n"
		.. "    int Add(int a, int b) {\n"
		.. "        return a + b;\n"
		.. "    }\n"
		.. "\n"
		.. "    void Reset() {\n"
		.. "        _result = 0;\n"
		.. "    }\n"
		.. "\n"
		.. "    static int Max(int a, int b) {\n"
		.. "        if (a > b) {\n"
		.. "            return a;\n"
		.. "        }\n"
		.. "        return b;\n"
		.. "    }\n"
		.. "}\n"
		.. "\n"
		.. "enum Operation { Add, Subtract = 1, Multiply }"

	print("=== Emitter Test ===")
	print("Input:")
	for line in string.gmatch(input, "[^\n]+") do
		print(line)
	end
	print("")

	-- Parse
	local parser = SimpleParser.new(input)
	local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

	-- Emit Luau
	local emitter = SimpleEmitter.new()
	local luauOutput = SimpleEmitter.Emit(emitter, compilationUnit)

	print("=== Emitter Luau Output ===")
	-- Print each line
	if string.sub(luauOutput, #luauOutput, #luauOutput) == "\n" then
		luauOutput = string.sub(luauOutput, 1, #luauOutput - 1)
	end
	for line in string.gmatch(luauOutput, "[^\n]*") do
		print(line)
	end

	-- Count output lines
	local lineCount = 0
	for _ in string.gmatch(luauOutput, "[^\n]+") do
		lineCount += 1
	end
	print("")
	print("Total Luau output lines: " .. lineCount)

	warn("[LUSharp-Test] Emitter test completed")
end

local function runSelfEmitTest()
	local simpleParserModule = modules:FindFirstChild("SimpleParser")
	local simpleEmitterModule = modules:FindFirstChild("SimpleEmitter")

	if not simpleParserModule or not simpleEmitterModule then
		warn("[LUSharp-Test] Self-emit modules not found, skipping")
		return
	end

	local ok1, spResult = pcall(require, simpleParserModule)
	if not ok1 then
		warn("[LUSharp-Test] FAIL: SimpleParser failed to load: " .. tostring(spResult))
		return
	end

	local ok2, seResult = pcall(require, simpleEmitterModule)
	if not ok2 then
		warn("[LUSharp-Test] FAIL: SimpleEmitter failed to load: " .. tostring(seResult))
		return
	end

	local SimpleParser = spResult.SimpleParser
	local SimpleEmitter = seResult.SimpleEmitter

	if not SimpleParser or not SimpleEmitter then
		warn("[LUSharp-Test] FAIL: SimpleParser or SimpleEmitter table not found")
		return
	end

	print("=== Self-Emit Validation (Luau) ===")

	-- Test files: actual C# source code for key types
	local sources: { { name: string, source: string } } = {}

	-- SyntaxToken.cs (simple struct)
	table.insert(sources, {
		name = "SyntaxToken.cs",
		source = "namespace RoslynLuau;\n"
			.. "public struct SyntaxToken\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    public string Text { get; }\n"
			.. "    public int Start { get; }\n"
			.. "    public int Length { get; }\n"
			.. "    public SyntaxToken(int kind, string text, int start, int length)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "        Text = text;\n"
			.. "        Start = start;\n"
			.. "        Length = length;\n"
			.. "    }\n"
			.. "    public bool IsMissing()\n"
			.. "    {\n"
			.. "        return Kind == 0;\n"
			.. "    }\n"
			.. "}"
	})

	-- SyntaxNode.cs (abstract class hierarchy)
	table.insert(sources, {
		name = "SyntaxNode.cs",
		source = "namespace RoslynLuau;\n"
			.. "public abstract class SyntaxNode\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    protected SyntaxNode(int kind)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "    }\n"
			.. "    public abstract string Accept();\n"
			.. "}\n"
			.. "public abstract class ExpressionSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected ExpressionSyntax(int kind) : base(kind)\n"
			.. "    {\n"
			.. "    }\n"
			.. "}\n"
			.. "public abstract class StatementSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected StatementSyntax(int kind) : base(kind)\n"
			.. "    {\n"
			.. "    }\n"
			.. "}"
	})

	-- Calculator class (full: fields, ctor, instance+static methods, if/else)
	table.insert(sources, {
		name = "Calculator.cs",
		source = "class Calculator {\n"
			.. "    int _result;\n"
			.. "    Calculator(int initial) {\n"
			.. "        _result = initial;\n"
			.. "    }\n"
			.. "    int Add(int a, int b) {\n"
			.. "        return a + b;\n"
			.. "    }\n"
			.. "    void Reset() {\n"
			.. "        _result = 0;\n"
			.. "    }\n"
			.. "    static int Max(int a, int b) {\n"
			.. "        if (a > b) {\n"
			.. "            return a;\n"
			.. "        }\n"
			.. "        return b;\n"
			.. "    }\n"
			.. "}\n"
			.. "enum Operation { Add, Subtract = 1, Multiply }"
	})

	-- ControlFlow class (for, foreach, while, switch, try/catch)
	table.insert(sources, {
		name = "ControlFlow.cs",
		source = "class ControlFlow {\n"
			.. "    void ForLoop() {\n"
			.. "        for (int i = 0; i < 10; i++) {\n"
			.. "            if (i == 5) break;\n"
			.. "        }\n"
			.. "    }\n"
			.. "    void ForEachLoop(int[] items) {\n"
			.. "        foreach (var item in items) {\n"
			.. "            int x = item;\n"
			.. "        }\n"
			.. "    }\n"
			.. "    void TryCatch() {\n"
			.. "        try {\n"
			.. "            int x = 1;\n"
			.. "        } catch (Exception ex) {\n"
			.. "            throw;\n"
			.. "        }\n"
			.. "    }\n"
			.. "}"
	})

	local passed = 0
	local total = #sources

	for _, entry in sources do
		local parseOk, parseErr = pcall(function()
			local parser = SimpleParser.new(entry.source)
			local compilationUnit = SimpleParser.ParseCompilationUnit(parser)

			local emitter = SimpleEmitter.new()
			local luauOutput = SimpleEmitter.Emit(emitter, compilationUnit)

			-- Count output lines
			local lineCount = 0
			for _ in string.gmatch(luauOutput, "[^\n]+") do
				lineCount += 1
			end

			-- Check key patterns
			local hasStrict = string.find(luauOutput, "--!strict", 1, true) ~= nil
			local hasReturn = string.find(luauOutput, "return {", 1, true) ~= nil

			print("  OK   " .. entry.name .. string.rep(" ", 30 - #entry.name)
				.. " " .. lineCount .. " Luau lines"
				.. "  strict=" .. tostring(hasStrict)
				.. "  return=" .. tostring(hasReturn))
			passed += 1
		end)

		if not parseOk then
			print("  FAIL " .. entry.name .. string.rep(" ", 30 - #entry.name) .. " " .. tostring(parseErr))
		end
	end

	print("")
	print("Self-emit: " .. passed .. "/" .. total .. " files emitted in Luau")

	-- Print the Calculator Luau output for visual inspection
	if passed >= 3 then
		print("")
		print("=== Calculator.cs → Luau Output ===")
		local parser = SimpleParser.new(sources[3].source)
		local compilationUnit = SimpleParser.ParseCompilationUnit(parser)
		local emitter = SimpleEmitter.new()
		local luauOutput = SimpleEmitter.Emit(emitter, compilationUnit)
		if string.sub(luauOutput, #luauOutput, #luauOutput) == "\n" then
			luauOutput = string.sub(luauOutput, 1, #luauOutput - 1)
		end
		for line in string.gmatch(luauOutput, "[^\n]*") do
			print(line)
		end
	end

	warn("[LUSharp-Test] Self-emit test completed")
end

local function runTranspilerTest()
	local simpleTranspilerModule = modules:FindFirstChild("SimpleTranspiler")
	if not simpleTranspilerModule then
		warn("[LUSharp-Test] SimpleTranspiler module not found, skipping")
		return
	end

	local ok, stResult = pcall(require, simpleTranspilerModule)
	if not ok then
		warn("[LUSharp-Test] FAIL: SimpleTranspiler failed to load: " .. tostring(stResult))
		return
	end

	local SimpleTranspiler = stResult.SimpleTranspiler
	if not SimpleTranspiler then
		warn("[LUSharp-Test] FAIL: SimpleTranspiler table not found")
		return
	end

	print("=== Cross-Module Transpiler Validation (Luau) ===")

	-- Two source files: SyntaxToken (standalone) and SyntaxNode (references SyntaxToken)
	local sources = {
		"namespace RoslynLuau;\n"
			.. "public struct SyntaxToken\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    public string Text { get; }\n"
			.. "    public SyntaxToken(int kind, string text)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "        Text = text;\n"
			.. "    }\n"
			.. "}",
		"namespace RoslynLuau;\n"
			.. "public abstract class SyntaxNode\n"
			.. "{\n"
			.. "    public int Kind { get; }\n"
			.. "    protected SyntaxNode(int kind)\n"
			.. "    {\n"
			.. "        Kind = kind;\n"
			.. "    }\n"
			.. "    public abstract string Accept();\n"
			.. "}\n"
			.. "public abstract class ExpressionSyntax : SyntaxNode\n"
			.. "{\n"
			.. "    protected ExpressionSyntax(int kind) : base(kind) { }\n"
			.. "}",
		"namespace RoslynLuau;\n"
			.. "public class ReturnStatement : SyntaxNode\n"
			.. "{\n"
			.. "    public ExpressionSyntax Expression { get; }\n"
			.. "    public ReturnStatement(ExpressionSyntax expression) : base(8805)\n"
			.. "    {\n"
			.. "        Expression = expression;\n"
			.. "    }\n"
			.. "    public override string Accept()\n"
			.. "    {\n"
			.. "        return \"return \" + Expression.Accept();\n"
			.. "    }\n"
			.. "}",
	}

	local fileNames = {
		"SyntaxToken.cs",
		"SyntaxNode.cs",
		"StatementNodes.cs",
	}

	local fileCount = 3

	-- Phase 1: PreScan
	local transpiler = SimpleTranspiler.new()
	local scanOk, scanErr = pcall(function()
		SimpleTranspiler.PreScan(transpiler, sources, fileNames, fileCount)
	end)

	if not scanOk then
		warn("[LUSharp-Test] FAIL: PreScan failed: " .. tostring(scanErr))
		return
	end
	print("  PreScan completed successfully")

	-- Phase 2: TranspileAll
	local transpileOk, outputs = pcall(function()
		return SimpleTranspiler.TranspileAll(transpiler, sources, fileNames, fileCount)
	end)

	if not transpileOk then
		warn("[LUSharp-Test] FAIL: TranspileAll failed: " .. tostring(outputs))
		return
	end
	print("  TranspileAll completed successfully")

	-- Validate each output
	local passed = 0
	for i = 1, fileCount do
		local output = outputs[i]
		if output == nil or output == "" then
			print("  FAIL " .. fileNames[i] .. " — empty output")
		else
			local lineCount = 0
			for _ in string.gmatch(output, "[^\n]+") do
				lineCount += 1
			end

			local hasStrict = string.find(output, "--!strict", 1, true) ~= nil
			local hasReturn = string.find(output, "return {", 1, true) ~= nil
			local requireCount = 0
			local searchStart = 1
			while true do
				local pos = string.find(output, "require(script.Parent.", searchStart, true)
				if pos then
					requireCount += 1
					searchStart = pos + 1
				else
					break
				end
			end

			local pad = string.rep(" ", math.max(0, 25 - #fileNames[i]))
			print("  OK   " .. fileNames[i] .. pad
				.. " " .. lineCount .. " lines"
				.. "  requires=" .. requireCount
				.. "  strict=" .. tostring(hasStrict)
				.. "  return=" .. tostring(hasReturn))
			passed += 1
		end
	end

	print("")
	print("Transpiler: " .. passed .. "/" .. fileCount .. " files passed")

	-- Print StatementNodes output (should have require for SyntaxNode)
	if passed >= 3 then
		print("")
		print("=== StatementNodes.cs → Luau (with requires) ===")
		local output = outputs[3]
		-- Print first 20 lines
		local lineNum = 0
		for line in string.gmatch(output, "[^\n]*") do
			lineNum += 1
			if lineNum <= 20 then
				print(line)
			end
		end
		if lineNum > 20 then
			print("  ... (" .. lineNum .. " total lines)")
		end
	end

	warn("[LUSharp-Test] Transpiler test completed")
end

-- Run all tests
warn("[LUSharp-Test] Starting tests...")
runSyntaxKindTest()
runSyntaxFactsTest()
runTokenizerTest()
runParserTest()
runWalkerTest()
runExpandedParserTest()
runSelfParseTest()
runFullSelfParseTest()
runEmitterTest()
runSelfEmitTest()
runTranspilerTest()
warn("[LUSharp-Test] All tests finished")
