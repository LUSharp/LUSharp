using System.Collections.Generic;
using RoslynLuau;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

/// <summary>
/// Simplified C# tokenizer that produces SyntaxKind tokens.
/// Uses SlidingTextWindow for character access and SyntaxFacts for classification.
/// </summary>
public class SimpleTokenizer
{
    private SlidingTextWindow _window;
    private readonly List<TokenInfo> _tokens;

    public SimpleTokenizer(string text)
    {
        _window = new SlidingTextWindow(text);
        _tokens = new List<TokenInfo>();
    }

    public List<TokenInfo> Tokenize()
    {
        _tokens.Clear();

        while (!_window.IsReallyAtEnd())
        {
            _window.Start();
            var token = ScanToken();
            if (token.Kind != SyntaxKind.None)
            {
                _tokens.Add(token);
            }
        }

        // Add end-of-file token
        _tokens.Add(new TokenInfo(SyntaxKind.EndOfFileToken, "", _window.Position, 0));

        return _tokens;
    }

    private TokenInfo ScanToken()
    {
        char ch = _window.PeekChar(0);

        // Whitespace
        if (SyntaxFacts.IsWhitespace(ch))
        {
            return ScanWhitespace();
        }

        // Newlines
        if (SyntaxFacts.IsNewLine(ch))
        {
            return ScanNewLine();
        }

        // Identifiers and keywords
        if (ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
        {
            return ScanIdentifierOrKeyword();
        }

        // Numbers
        if (SyntaxFacts.IsDecDigit(ch))
        {
            return ScanNumericLiteral();
        }

        // Interpolated string: $"..."
        if (ch == '$' && !_window.IsReallyAtEnd())
        {
            int peekPos = _window.Position + 1;
            if (peekPos < _window.TextLength && _window.CharAt(peekPos) == '"')
            {
                return ScanInterpolatedStringLiteral();
            }
        }

        // String literals
        if (ch == '"')
        {
            return ScanStringLiteral();
        }

        // Character literals
        if (ch == '\'')
        {
            return ScanCharacterLiteral();
        }

        // Punctuation/operators
        return ScanPunctuation();
    }

    private TokenInfo ScanWhitespace()
    {
        int start = _window.Position;
        while (!_window.IsReallyAtEnd() && SyntaxFacts.IsWhitespace(_window.PeekChar(0)))
        {
            _window.AdvanceChar(1);
        }
        return new TokenInfo(SyntaxKind.WhitespaceTrivia, _window.GetText(false), start, _window.Position - start);
    }

    private TokenInfo ScanNewLine()
    {
        int start = _window.Position;
        _window.AdvancePastNewLine();
        return new TokenInfo(SyntaxKind.EndOfLineTrivia, _window.GetText(false), start, _window.Position - start);
    }

    private TokenInfo ScanIdentifierOrKeyword()
    {
        int start = _window.Position;
        while (!_window.IsReallyAtEnd())
        {
            char ch = _window.PeekChar(0);
            if (ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                _window.AdvanceChar(1);
            }
            else
            {
                break;
            }
        }

        string text = _window.GetText(false);
        SyntaxKind keywordKind = SyntaxFacts.GetKeywordKind(text);

        if (keywordKind != SyntaxKind.None)
        {
            return new TokenInfo(keywordKind, text, start, text.Length);
        }

        return new TokenInfo(SyntaxKind.IdentifierToken, text, start, text.Length);
    }

    private TokenInfo ScanNumericLiteral()
    {
        int start = _window.Position;
        while (!_window.IsReallyAtEnd() && SyntaxFacts.IsDecDigit(_window.PeekChar(0)))
        {
            _window.AdvanceChar(1);
        }

        // Handle decimal point
        if (_window.PeekChar(0) == '.' && SyntaxFacts.IsDecDigit(_window.PeekChar(1)))
        {
            _window.AdvanceChar(1); // skip '.'
            while (!_window.IsReallyAtEnd() && SyntaxFacts.IsDecDigit(_window.PeekChar(0)))
            {
                _window.AdvanceChar(1);
            }
        }

        string text = _window.GetText(false);
        return new TokenInfo(SyntaxKind.NumericLiteralToken, text, start, text.Length);
    }

    private TokenInfo ScanInterpolatedStringLiteral()
    {
        int start = _window.Position;
        _window.AdvanceChar(1); // skip $
        _window.AdvanceChar(1); // skip "

        int braceDepth = 0;
        while (!_window.IsReallyAtEnd())
        {
            char ch = _window.PeekChar(0);
            if (ch == '{' && braceDepth == 0)
            {
                // Check for {{ (escaped brace)
                int nextPos = _window.Position + 1;
                if (nextPos < _window.TextLength && _window.CharAt(nextPos) == '{')
                {
                    _window.AdvanceChar(1);
                    _window.AdvanceChar(1);
                    continue;
                }
                braceDepth++;
                _window.AdvanceChar(1);
                continue;
            }
            if (ch == '}' && braceDepth > 0)
            {
                braceDepth--;
                _window.AdvanceChar(1);
                continue;
            }
            if (ch == '"' && braceDepth == 0)
            {
                _window.AdvanceChar(1);
                break;
            }
            if (ch == '\\')
            {
                _window.AdvanceChar(1);
                if (!_window.IsReallyAtEnd())
                    _window.AdvanceChar(1);
            }
            else
            {
                _window.AdvanceChar(1);
            }
        }

        string text = _window.GetText(false);
        return new TokenInfo(SyntaxKind.InterpolatedStringToken, text, start, text.Length);
    }

    private TokenInfo ScanStringLiteral()
    {
        int start = _window.Position;
        _window.AdvanceChar(1); // skip opening "

        while (!_window.IsReallyAtEnd())
        {
            char ch = _window.PeekChar(0);
            if (ch == '"')
            {
                _window.AdvanceChar(1);
                break;
            }
            if (ch == '\\')
            {
                _window.AdvanceChar(1); // skip backslash
                if (!_window.IsReallyAtEnd())
                    _window.AdvanceChar(1); // skip escaped char
            }
            else
            {
                _window.AdvanceChar(1);
            }
        }

        string text = _window.GetText(false);
        return new TokenInfo(SyntaxKind.StringLiteralToken, text, start, text.Length);
    }

    private TokenInfo ScanCharacterLiteral()
    {
        int start = _window.Position;
        _window.AdvanceChar(1); // skip opening '

        if (_window.PeekChar(0) == '\\')
        {
            _window.AdvanceChar(1); // skip backslash
            if (!_window.IsReallyAtEnd())
            {
                char esc = _window.PeekChar(0);
                if (esc == 'u')
                {
                    _window.AdvanceChar(1); // skip u
                    for (int i = 0; i < 4 && !_window.IsReallyAtEnd() && SyntaxFacts.IsHexDigit(_window.PeekChar(0)); i++)
                        _window.AdvanceChar(1);
                }
                else if (esc == 'U')
                {
                    _window.AdvanceChar(1); // skip U
                    for (int i = 0; i < 8 && !_window.IsReallyAtEnd() && SyntaxFacts.IsHexDigit(_window.PeekChar(0)); i++)
                        _window.AdvanceChar(1);
                }
                else
                {
                    _window.AdvanceChar(1); // skip single escaped char
                }
            }
        }
        else if (!_window.IsReallyAtEnd())
        {
            _window.AdvanceChar(1); // skip the char
        }

        if (_window.PeekChar(0) == '\'')
            _window.AdvanceChar(1); // skip closing '

        string text = _window.GetText(false);
        return new TokenInfo(SyntaxKind.CharacterLiteralToken, text, start, text.Length);
    }

    private TokenInfo ScanPunctuation()
    {
        int start = _window.Position;
        char ch = _window.NextChar();

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
            '?' => ScanQuestionToken(),
            '~' => SyntaxKind.TildeToken,
            '+' => ScanPlusToken(),
            '-' => ScanMinusToken(),
            '*' => SyntaxKind.AsteriskToken,
            '/' => ScanSlashToken(),
            '%' => SyntaxKind.PercentToken,
            '&' => ScanAmpersandToken(),
            '|' => ScanBarToken(),
            '^' => SyntaxKind.CaretToken,
            '!' => ScanExclamationToken(),
            '=' => ScanEqualsToken(),
            '<' => ScanLessThanToken(),
            '>' => ScanGreaterThanToken(),
            _ => SyntaxKind.None,
        };

        string text = _window.GetText(false);
        return new TokenInfo(kind, text, start, text.Length);
    }

    // Multi-character operator helpers
    private SyntaxKind ScanPlusToken()
    {
        if (_window.PeekChar(0) == '+') { _window.AdvanceChar(1); return SyntaxKind.PlusPlusToken; }
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.PlusEqualsToken; }
        return SyntaxKind.PlusToken;
    }

    private SyntaxKind ScanMinusToken()
    {
        if (_window.PeekChar(0) == '-') { _window.AdvanceChar(1); return SyntaxKind.MinusMinusToken; }
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.MinusEqualsToken; }
        if (_window.PeekChar(0) == '>') { _window.AdvanceChar(1); return SyntaxKind.MinusGreaterThanToken; }
        return SyntaxKind.MinusToken;
    }

    private SyntaxKind ScanSlashToken()
    {
        if (_window.PeekChar(0) == '/')
        {
            // Single-line comment — skip to end of line
            while (!_window.IsReallyAtEnd() && !SyntaxFacts.IsNewLine(_window.PeekChar(0)))
                _window.AdvanceChar(1);
            return SyntaxKind.SingleLineCommentTrivia;
        }
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.SlashEqualsToken; }
        return SyntaxKind.SlashToken;
    }

    private SyntaxKind ScanAmpersandToken()
    {
        if (_window.PeekChar(0) == '&') { _window.AdvanceChar(1); return SyntaxKind.AmpersandAmpersandToken; }
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.AmpersandEqualsToken; }
        return SyntaxKind.AmpersandToken;
    }

    private SyntaxKind ScanBarToken()
    {
        if (_window.PeekChar(0) == '|') { _window.AdvanceChar(1); return SyntaxKind.BarBarToken; }
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.BarEqualsToken; }
        return SyntaxKind.BarToken;
    }

    private SyntaxKind ScanQuestionToken()
    {
        if (_window.PeekChar(0) == '?')
        {
            _window.AdvanceChar(1);
            if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.QuestionQuestionEqualsToken; }
            return SyntaxKind.QuestionQuestionToken;
        }
        if (_window.PeekChar(0) == '.') { _window.AdvanceChar(1); return SyntaxKind.QuestionDotToken; }
        return SyntaxKind.QuestionToken;
    }

    private SyntaxKind ScanExclamationToken()
    {
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.ExclamationEqualsToken; }
        return SyntaxKind.ExclamationToken;
    }

    private SyntaxKind ScanEqualsToken()
    {
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.EqualsEqualsToken; }
        if (_window.PeekChar(0) == '>') { _window.AdvanceChar(1); return SyntaxKind.EqualsGreaterThanToken; }
        return SyntaxKind.EqualsToken;
    }

    private SyntaxKind ScanLessThanToken()
    {
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.LessThanEqualsToken; }
        if (_window.PeekChar(0) == '<') { _window.AdvanceChar(1); return SyntaxKind.LessThanLessThanToken; }
        return SyntaxKind.LessThanToken;
    }

    private SyntaxKind ScanGreaterThanToken()
    {
        if (_window.PeekChar(0) == '=') { _window.AdvanceChar(1); return SyntaxKind.GreaterThanEqualsToken; }
        // Note: >> is handled contextually in the parser, not the lexer
        return SyntaxKind.GreaterThanToken;
    }
}
