local Lexer = require("../src/Lexer")

local function run(describe, it, expect)
    describe("Lexer", function()
        it("tokenizes keywords", function()
            local tokens = Lexer.tokenize("class void public static")
            expect(#tokens):toBe(5) -- 4 keywords + EOF
            expect(tokens[1].type):toBe("keyword")
            expect(tokens[1].value):toBe("class")
            expect(tokens[2].type):toBe("keyword")
            expect(tokens[2].value):toBe("void")
        end)

        it("tokenizes identifiers", function()
            local tokens = Lexer.tokenize("myVar _foo Bar123")
            expect(tokens[1].type):toBe("identifier")
            expect(tokens[1].value):toBe("myVar")
        end)

        it("tokenizes async and await as keywords", function()
            local tokens = Lexer.tokenize("async await")
            expect(tokens[1].type):toBe("keyword")
            expect(tokens[1].value):toBe("async")
            expect(tokens[2].type):toBe("keyword")
            expect(tokens[2].value):toBe("await")
        end)

        it("tokenizes numbers", function()
            local tokens = Lexer.tokenize("42 3.14 0xFF 1.5f")
            expect(tokens[1].type):toBe("number")
            expect(tokens[1].value):toBe("42")
            expect(tokens[2].type):toBe("number")
            expect(tokens[2].value):toBe("3.14")
        end)

        it("tokenizes strings", function()
            local tokens = Lexer.tokenize('"hello world"')
            expect(tokens[1].type):toBe("string")
            expect(tokens[1].value):toBe('"hello world"')
        end)

        it("tokenizes interpolated strings", function()
            local tokens = Lexer.tokenize('$"Hello {name}"')
            expect(tokens[1].type):toBe("interpolated_string")
        end)

        it("tokenizes operators", function()
            local tokens = Lexer.tokenize("+ - * / == != <= >= && || ??")
            expect(tokens[1].type):toBe("operator")
            expect(tokens[1].value):toBe("+")
            expect(tokens[5].value):toBe("==")
        end)

        it("tokenizes punctuation", function()
            local tokens = Lexer.tokenize("( ) { } [ ] ; , . :")
            expect(tokens[1].type):toBe("punctuation")
            expect(tokens[1].value):toBe("(")
        end)

        it("skips single-line comments", function()
            local tokens = Lexer.tokenize("x // this is a comment\ny")
            expect(tokens[1].type):toBe("identifier")
            expect(tokens[1].value):toBe("x")
            expect(tokens[2].type):toBe("identifier")
            expect(tokens[2].value):toBe("y")
        end)

        it("skips multi-line comments", function()
            local tokens = Lexer.tokenize("x /* comment\n spans lines */ y")
            expect(tokens[1].value):toBe("x")
            expect(tokens[2].value):toBe("y")
        end)

        it("preserves comments when requested", function()
            local tokens = Lexer.tokenize("x // comment\ny", { preserveComments = true })
            expect(tokens[2].type):toBe("comment")
            expect(tokens[2].value):toBe("// comment")
        end)

        it("tracks line and column", function()
            local tokens = Lexer.tokenize("foo\nbar")
            expect(tokens[1].line):toBe(1)
            expect(tokens[1].column):toBe(1)
            expect(tokens[2].line):toBe(2)
            expect(tokens[2].column):toBe(1)
        end)

        it("tokenizes a full class declaration", function()
            local tokens = Lexer.tokenize([[
public class ServerMain : RobloxScript {
    public void GameEntry() {
        print("Hello");
    }
}]])
            local lastToken = tokens[#tokens]
            expect(lastToken.type):toBe("eof")
        end)

        it("handles arrow operator", function()
            local tokens = Lexer.tokenize("x => x + 1")
            expect(tokens[2].type):toBe("operator")
            expect(tokens[2].value):toBe("=>")
        end)

        it("handles generic angle brackets", function()
            local tokens = Lexer.tokenize("List<int>")
            expect(tokens[1].value):toBe("List")
            expect(tokens[2].value):toBe("<")
            expect(tokens[3].value):toBe("int")
            expect(tokens[4].value):toBe(">")
        end)
    end)
end

return run
