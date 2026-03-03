using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Reference tokenizer that mirrors the simplified SimpleTokenizer's behavior
/// using Roslyn's real SyntaxKind and SyntaxFacts (via reflection for internal methods).
/// Produces output in the same format the transpiled Luau version should produce.
/// </summary>
public static class TokenizerReference
{
    // Access internal Roslyn methods via reflection
    private static readonly Type s_factsType = typeof(SyntaxFacts);

    private static bool IsDecDigit(char c)
    {
        var method = s_factsType.GetMethod("IsDecDigit", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return method != null ? (bool)method.Invoke(null, new object[] { c })! : (c >= '0' && c <= '9');
    }

    private static bool IsWhitespace(char c)
    {
        var method = s_factsType.GetMethod("IsWhitespace", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return method != null ? (bool)method.Invoke(null, new object[] { c })! : (c == ' ' || c == '\t');
    }

    private static bool IsNewLine(char c)
    {
        var method = s_factsType.GetMethod("IsNewLine", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return method != null ? (bool)method.Invoke(null, new object[] { c })! : (c == '\n' || c == '\r');
    }

    private const char InvalidCharacter = '\uffff';

    /// <summary>
    /// Simplified sliding window over a string (mirrors SlidingTextWindow).
    /// </summary>
    private class Window
    {
        private readonly string _text;
        private readonly int _textEnd;
        public int Position;
        public int LexemeStartPosition;

        public Window(string text)
        {
            _text = text;
            _textEnd = text.Length;
            Position = 0;
            LexemeStartPosition = 0;
        }

        public void Start() => LexemeStartPosition = Position;
        public bool IsReallyAtEnd() => Position >= _textEnd;

        public char PeekChar()
        {
            if (Position >= _textEnd) return InvalidCharacter;
            return _text[Position];
        }

        public char PeekChar(int delta)
        {
            int pos = Position + delta;
            if (pos < 0 || pos >= _textEnd) return InvalidCharacter;
            return _text[pos];
        }

        public char NextChar()
        {
            if (Position >= _textEnd) return InvalidCharacter;
            char c = _text[Position];
            Position++;
            return c;
        }

        public void AdvanceChar() => Position++;
        public void AdvanceChar(int n) => Position += n;

        public string GetText()
        {
            int length = Position - LexemeStartPosition;
            if (length == 0) return string.Empty;
            return _text.Substring(LexemeStartPosition, length);
        }

        public int GetNewLineWidth()
        {
            char c = PeekChar();
            if (c == '\r')
            {
                if (PeekChar(1) == '\n') return 2;
                return 1;
            }
            if (c == '\n' || c == '\u0085' || c == '\u2028' || c == '\u2029')
                return 1;
            return 0;
        }

        public void AdvancePastNewLine() => AdvanceChar(GetNewLineWidth());
    }

    private struct TokenInfo
    {
        public SyntaxKind Kind;
        public string Text;
        public int Start;
        public int Length;
    }

    /// <summary>
    /// Tokenize a string and return (Kind, Text) pairs — mirrors SimpleTokenizer.Tokenize().
    /// </summary>
    private static List<TokenInfo> Tokenize(string input)
    {
        var window = new Window(input);
        var tokens = new List<TokenInfo>();

        while (!window.IsReallyAtEnd())
        {
            window.Start();
            var token = ScanToken(window);
            if (token.Kind != SyntaxKind.None)
            {
                tokens.Add(token);
            }
        }

        tokens.Add(new TokenInfo
        {
            Kind = SyntaxKind.EndOfFileToken,
            Text = "",
            Start = window.Position,
            Length = 0
        });

        return tokens;
    }

    private static TokenInfo ScanToken(Window w)
    {
        char ch = w.PeekChar();

        if (IsWhitespace(ch)) return ScanWhitespace(w);
        if (IsNewLine(ch)) return ScanNewLine(w);
        if (ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
            return ScanIdentifierOrKeyword(w);
        if (IsDecDigit(ch)) return ScanNumericLiteral(w);
        if (ch == '"') return ScanStringLiteral(w);
        if (ch == '\'') return ScanCharacterLiteral(w);
        return ScanPunctuation(w);
    }

    private static TokenInfo ScanWhitespace(Window w)
    {
        int start = w.Position;
        while (!w.IsReallyAtEnd() && IsWhitespace(w.PeekChar()))
            w.AdvanceChar();
        return new TokenInfo { Kind = SyntaxKind.WhitespaceTrivia, Text = w.GetText(), Start = start, Length = w.Position - start };
    }

    private static TokenInfo ScanNewLine(Window w)
    {
        int start = w.Position;
        w.AdvancePastNewLine();
        return new TokenInfo { Kind = SyntaxKind.EndOfLineTrivia, Text = w.GetText(), Start = start, Length = w.Position - start };
    }

    private static TokenInfo ScanIdentifierOrKeyword(Window w)
    {
        int start = w.Position;
        while (!w.IsReallyAtEnd())
        {
            char ch = w.PeekChar();
            if (ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                w.AdvanceChar();
            else
                break;
        }
        string text = w.GetText();
        SyntaxKind keywordKind = SyntaxFacts.GetKeywordKind(text);
        if (keywordKind != SyntaxKind.None)
            return new TokenInfo { Kind = keywordKind, Text = text, Start = start, Length = text.Length };
        return new TokenInfo { Kind = SyntaxKind.IdentifierToken, Text = text, Start = start, Length = text.Length };
    }

    private static TokenInfo ScanNumericLiteral(Window w)
    {
        int start = w.Position;
        while (!w.IsReallyAtEnd() && IsDecDigit(w.PeekChar()))
            w.AdvanceChar();
        if (w.PeekChar() == '.' && IsDecDigit(w.PeekChar(1)))
        {
            w.AdvanceChar();
            while (!w.IsReallyAtEnd() && IsDecDigit(w.PeekChar()))
                w.AdvanceChar();
        }
        string text = w.GetText();
        return new TokenInfo { Kind = SyntaxKind.NumericLiteralToken, Text = text, Start = start, Length = text.Length };
    }

    private static TokenInfo ScanStringLiteral(Window w)
    {
        int start = w.Position;
        w.AdvanceChar(); // skip opening "
        while (!w.IsReallyAtEnd())
        {
            char ch = w.PeekChar();
            if (ch == '"') { w.AdvanceChar(); break; }
            if (ch == '\\') { w.AdvanceChar(); if (!w.IsReallyAtEnd()) w.AdvanceChar(); }
            else w.AdvanceChar();
        }
        string text = w.GetText();
        return new TokenInfo { Kind = SyntaxKind.StringLiteralToken, Text = text, Start = start, Length = text.Length };
    }

    private static TokenInfo ScanCharacterLiteral(Window w)
    {
        int start = w.Position;
        w.AdvanceChar(); // skip opening '
        if (w.PeekChar() == '\\') { w.AdvanceChar(); if (!w.IsReallyAtEnd()) w.AdvanceChar(); }
        else if (!w.IsReallyAtEnd()) w.AdvanceChar();
        if (w.PeekChar() == '\'') w.AdvanceChar();
        string text = w.GetText();
        return new TokenInfo { Kind = SyntaxKind.CharacterLiteralToken, Text = text, Start = start, Length = text.Length };
    }

    private static TokenInfo ScanPunctuation(Window w)
    {
        int start = w.Position;
        char ch = w.NextChar();

        SyntaxKind kind = ch switch
        {
            '{' => SyntaxKind.OpenBraceToken,
            '}' => SyntaxKind.CloseBraceToken,
            '(' => SyntaxKind.OpenParenToken,
            ')' => SyntaxKind.CloseParenToken,
            '[' => SyntaxKind.OpenBracketToken,
            ']' => SyntaxKind.CloseBracketToken,
            ';' => SyntaxKind.SemicolonToken,
            ',' => SyntaxKind.CommaToken,
            '.' => SyntaxKind.DotToken,
            ':' => SyntaxKind.ColonToken,
            '?' => SyntaxKind.QuestionToken,
            '~' => SyntaxKind.TildeToken,
            '+' => ScanPlus(w),
            '-' => ScanMinus(w),
            '*' => SyntaxKind.AsteriskToken,
            '/' => ScanSlash(w),
            '%' => SyntaxKind.PercentToken,
            '&' => ScanAmpersand(w),
            '|' => ScanBar(w),
            '^' => SyntaxKind.CaretToken,
            '!' => ScanExclamation(w),
            '=' => ScanEquals(w),
            '<' => ScanLessThan(w),
            '>' => ScanGreaterThan(w),
            _ => SyntaxKind.None,
        };

        string text = w.GetText();
        return new TokenInfo { Kind = kind, Text = text, Start = start, Length = text.Length };
    }

    private static SyntaxKind ScanPlus(Window w)
    {
        if (w.PeekChar() == '+') { w.AdvanceChar(); return SyntaxKind.PlusPlusToken; }
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.PlusEqualsToken; }
        return SyntaxKind.PlusToken;
    }

    private static SyntaxKind ScanMinus(Window w)
    {
        if (w.PeekChar() == '-') { w.AdvanceChar(); return SyntaxKind.MinusMinusToken; }
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.MinusEqualsToken; }
        if (w.PeekChar() == '>') { w.AdvanceChar(); return SyntaxKind.MinusGreaterThanToken; }
        return SyntaxKind.MinusToken;
    }

    private static SyntaxKind ScanSlash(Window w)
    {
        if (w.PeekChar() == '/')
        {
            while (!w.IsReallyAtEnd() && !IsNewLine(w.PeekChar()))
                w.AdvanceChar();
            return SyntaxKind.SingleLineCommentTrivia;
        }
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.SlashEqualsToken; }
        return SyntaxKind.SlashToken;
    }

    private static SyntaxKind ScanAmpersand(Window w)
    {
        if (w.PeekChar() == '&') { w.AdvanceChar(); return SyntaxKind.AmpersandAmpersandToken; }
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.AmpersandEqualsToken; }
        return SyntaxKind.AmpersandToken;
    }

    private static SyntaxKind ScanBar(Window w)
    {
        if (w.PeekChar() == '|') { w.AdvanceChar(); return SyntaxKind.BarBarToken; }
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.BarEqualsToken; }
        return SyntaxKind.BarToken;
    }

    private static SyntaxKind ScanExclamation(Window w)
    {
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.ExclamationEqualsToken; }
        return SyntaxKind.ExclamationToken;
    }

    private static SyntaxKind ScanEquals(Window w)
    {
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.EqualsEqualsToken; }
        if (w.PeekChar() == '>') { w.AdvanceChar(); return SyntaxKind.EqualsGreaterThanToken; }
        return SyntaxKind.EqualsToken;
    }

    private static SyntaxKind ScanLessThan(Window w)
    {
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.LessThanEqualsToken; }
        if (w.PeekChar() == '<') { w.AdvanceChar(); return SyntaxKind.LessThanLessThanToken; }
        return SyntaxKind.LessThanToken;
    }

    private static SyntaxKind ScanGreaterThan(Window w)
    {
        if (w.PeekChar() == '=') { w.AdvanceChar(); return SyntaxKind.GreaterThanEqualsToken; }
        return SyntaxKind.GreaterThanToken;
    }

    // ─── Public API ───

    private static void PrintTokens(string label, string input)
    {
        Console.WriteLine($"=== {label} ===");
        var tokens = Tokenize(input);
        foreach (var token in tokens)
        {
            Console.WriteLine($"Kind={token.Kind} Text={token.Text}");
        }
    }

    public static void PrintAll()
    {
        PrintTokens("Test 1: int x = 5;", "int x = 5;");
        PrintTokens("Test 2: if (x >= 10) { return true; }", "if (x >= 10) { return true; }");
    }
}
