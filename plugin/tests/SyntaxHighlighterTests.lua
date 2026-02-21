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
    end)
end

return run
