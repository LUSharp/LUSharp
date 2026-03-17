namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

/// <summary>
/// Simplified SlidingTextWindow backed by a plain string.
/// Inspired by Roslyn's SlidingTextWindow but stripped of SourceText/ObjectPool dependencies.
/// </summary>
public struct SlidingTextWindow
{
    public const char InvalidCharacter = '\uffff';

    private readonly string _text;
    private readonly int _textEnd;
    public int Position;
    public int LexemeStartPosition;

    public int Width => Position - LexemeStartPosition;
    public int TextLength => _textEnd;

    public char CharAt(int pos)
    {
        if (pos >= 0 && pos < _textEnd) return _text[pos];
        return InvalidCharacter;
    }

    public SlidingTextWindow(string text)
    {
        _text = text;
        _textEnd = text.Length;
        Position = 0;
        LexemeStartPosition = 0;
    }

    public void Start()
    {
        LexemeStartPosition = Position;
    }

    public bool IsReallyAtEnd()
    {
        return Position >= _textEnd;
    }

    public char PeekChar(int delta)
    {
        int pos = Position + delta;
        if (pos < 0 || pos >= _textEnd)
            return InvalidCharacter;
        return _text[pos];
    }

    public char NextChar()
    {
        if (Position >= _textEnd)
            return InvalidCharacter;
        char c = _text[Position];
        Position++;
        return c;
    }

    public void AdvanceChar(int n)
    {
        Position += n;
    }

    public bool TryAdvance(char c)
    {
        if (PeekChar(0) == c)
        {
            AdvanceChar(1);
            return true;
        }
        return false;
    }

    public void Reset(int position)
    {
        Position = position;
    }

    public string GetText(bool intern)
    {
        int length = Position - LexemeStartPosition;
        if (length == 0)
            return string.Empty;
        return _text.Substring(LexemeStartPosition, length);
    }

    public int GetNewLineWidth()
    {
        char c = PeekChar(0);
        if (c == '\r')
        {
            if (PeekChar(1) == '\n')
                return 2;
            return 1;
        }
        if (c == '\n' || c == '\u0085' || c == '\u2028' || c == '\u2029')
            return 1;
        return 0;
    }

    public void AdvancePastNewLine()
    {
        AdvanceChar(GetNewLineWidth());
    }
}
