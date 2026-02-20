local SyntaxHighlighter = require("../src/SyntaxHighlighter")

local function run(describe, it, expect)
    describe("SyntaxHighlighter", function()
        it("styles keywords", function()
            local out = SyntaxHighlighter.highlight("class Foo { }")
            expect(out):toContain('<font color="#C586C0">class</font>')
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
            expect(out):toContain('</font>  <font color="#9CDCFE">Foo</font>')
            expect(out):toContain("\n\t<font")
            expect(out):toContain('return</font>  <font color="#B5CEA8">42</font>')
        end)
    end)
end

return run
