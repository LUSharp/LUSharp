local SyntaxHighlighter = require("../src/SyntaxHighlighter")

local function run(describe, it, expect)
    describe("SyntaxHighlighter", function()
        it("styles keywords", function()
            local out = SyntaxHighlighter.highlight("class Foo { }")
            expect(out):toContain('<font color="#C586C0">class</font>')
        end)

        it("styles keywords in Light theme", function()
            local out = SyntaxHighlighter.highlight("class Foo { }", { theme = "Light" })
            expect(out):toContain('<font color="#0000FF">class</font>')
        end)

        it("styles strings and comments", function()
            local out = SyntaxHighlighter.highlight('// greet\nstring s = "hi";')
            expect(out):toContain('<font color="#6A9955">// greet</font>')
            expect(out):toContain('<font color="#CE9178">&quot;hi&quot;</font>')
        end)

        it("escapes rich text special characters", function()
            local out = SyntaxHighlighter.highlight('if (a < b && b > c) { var s = "<&>"; }')
            expect(out):toContain("&lt;")
            expect(out):toContain("&gt;")
            expect(out):toContain("&amp;")
            expect(out):toContain("&quot;&lt;&amp;&gt;&quot;")
        end)

        it("preserves whitespace and newlines", function()
            local source = "class  Foo\n\t{\n\t\treturn  42;\n\t}"
            local out = SyntaxHighlighter.highlight(source)
            expect(out):toContain('</font>  <font color="#4EC9B0">Foo</font>')
            expect(out):toContain("\n\t<font")
            expect(out):toContain('return</font>  <font color="#B5CEA8">42</font>')
        end)

        it("styles type and method identifiers", function()
            local out = SyntaxHighlighter.highlight("class Foo { void GameEntry() { Foo.New(); } }")
            expect(out):toContain('<font color="#4EC9B0">Foo</font>')
            expect(out):toContain('<font color="#DCDCAA">GameEntry</font>')
            expect(out):toContain('<font color="#DCDCAA">New</font>')
        end)

        it("styles enum-like chains", function()
            local out = SyntaxHighlighter.highlight("var mat = Enum.Material.Plastic;")
            expect(out):toContain('<font color="#B5CEA8">Material</font>')
            expect(out):toContain('<font color="#B5CEA8">Plastic</font>')
        end)

        it("styles local variable declarations", function()
            local out = SyntaxHighlighter.highlight("int count = 0; count = count + 1;")
            expect(out):toContain('<font color="#D7BA7D">count</font>')
        end)

        it("styles full using namespace chain", function()
            local out = SyntaxHighlighter.highlight("using System.Collections.Generic;")
            expect(out):toContain('<font color="#C8C8C8">System</font>')
            expect(out):toContain('<font color="#C8C8C8">Collections</font>')
            expect(out):toContain('<font color="#C8C8C8">Generic</font>')
        end)

        it("styles full namespace declaration chain", function()
            local out = SyntaxHighlighter.highlight("namespace Game.Shared.Core { }")
            expect(out):toContain('<font color="#C8C8C8">Game</font>')
            expect(out):toContain('<font color="#C8C8C8">Shared</font>')
            expect(out):toContain('<font color="#C8C8C8">Core</font>')
        end)

        it("styles generic, array, and nullable parameter identifiers as local vars", function()
            local out = SyntaxHighlighter.highlight("class Foo { void Log(List<int> items, string[] names, int? count) { Log(items, names, count); } }")
            expect(out):toContain('<font color="#D7BA7D">items</font>')
            expect(out):toContain('<font color="#D7BA7D">names</font>')
            expect(out):toContain('<font color="#D7BA7D">count</font>')
        end)

        it("keeps method names method-colored while call arguments use argument coloring", function()
            local out = SyntaxHighlighter.highlight("class Foo { void Log(int value) { Log(value); } }")
            expect(out):toContain('<font color="#DCDCAA">Log</font>')
            expect(out):toContain('<font color="#F5B971">value</font>')

            local methodCount = 0
            for _ in out:gmatch('<font color="#DCDCAA">Log</font>') do
                methodCount += 1
            end

            expect(methodCount):toBe(2)
            expect(out:find('<font color="#D7BA7D">Log</font>', 1, true) == nil):toBe(true)
        end)

        it("styles nested property chains and events distinctly", function()
            local out = SyntaxHighlighter.highlight("var players = game.GetService(\"Players\"); players.LocalPlayer.CharacterAdded.Connect(OnCharacterAdded); var anchored = players.LocalPlayer.Character.PrimaryPart.Anchored;")
            expect(out):toContain('<font color="#4FC1FF">LocalPlayer</font>')
            expect(out):toContain('<font color="#C586C0">CharacterAdded</font>')
            expect(out):toContain('<font color="#DCDCAA">Connect</font>')
            expect(out):toContain('<font color="#4FC1FF">PrimaryPart</font>')
            expect(out):toContain('<font color="#4FC1FF">Anchored</font>')
        end)

        it("does not treat comparison rhs identifier as declaration local", function()
            local out = SyntaxHighlighter.highlight("class Foo { bool Check(int a) { return a > limit; } }")
            expect(out):toContain('<font color="#D7BA7D">a</font>')
            expect(out:find('<font color="#D7BA7D">limit</font>', 1, true) == nil):toBe(true)
        end)

        it("styles interpolation placeholders as local variables", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log(string name) { print($"Hello {name}"); } }')
            expect(out):toContain('<font color="#D7BA7D">name</font>')
            expect(out):toContain('<font color="#D4D4D4">{</font>')
            expect(out):toContain('<font color="#D4D4D4">}</font>')
        end)

        it("does not style placeholder braces in regular string literals", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log() { print("{name} is data"); } }')
            expect(out:find('{<font color="#D7BA7D">name</font>}', 1, true) == nil):toBe(true)
        end)

        it("styles dollar-prefixed interpolation placeholders as local variables", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log(string name) { print($"Hello ${name}"); } }')
            expect(out):toContain('<font color="#D7BA7D">name</font>')
            expect(out):toContain('<font color="#D4D4D4">{</font>')
            expect(out):toContain('<font color="#D4D4D4">}</font>')
        end)

        it("styles interpolation expressions with surrounding whitespace", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log(string data) { print($"{ data } is data"); } }')
            expect(out):toContain('<font color="#D7BA7D">data</font>')
        end)

        it("styles member chains inside interpolation expressions", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log() { var player = game.GetService("Players"); print($"Hello {player.LocalPlayer.Name}"); } }')
            expect(out):toContain('<font color="#D7BA7D">player</font>')
            expect(out):toContain('<font color="#4FC1FF">LocalPlayer</font>')
            expect(out):toContain('<font color="#4FC1FF">Name</font>')
        end)

        it("styles globals, methods, generics, and braces inside interpolation", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log() { print($"{game.GetService<Players>()}"); } }')
            expect(out):toContain('<font color="#9CDCFE">game</font>')
            expect(out):toContain('<font color="#DCDCAA">GetService</font>')
            expect(out):toContain('<font color="#4EC9B0">Players</font>')
            expect(out):toContain('<font color="#D4D4D4">{</font>')
            expect(out):toContain('<font color="#D4D4D4">}</font>')
        end)

        it("styles generic type arguments in GetService calls as types", function()
            local out = SyntaxHighlighter.highlight("var service = game.GetService<ServerScriptService>();")
            expect(out):toContain('<font color="#4EC9B0">ServerScriptService</font>')
        end)

        it("styles class names in parameter and argument positions", function()
            local out = SyntaxHighlighter.highlight('class Foo { void Log(Console consoleRef) { UseType(Console, Players); Console.WriteLine("x"); } }')
            expect(out):toContain('<font color="#4EC9B0">Console</font>')
            expect(out):toContain('<font color="#4EC9B0">Players</font>')
        end)
    end)
end

return run
