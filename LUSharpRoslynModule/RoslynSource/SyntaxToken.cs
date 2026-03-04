namespace RoslynLuau;

public struct SyntaxToken
{
    public int Kind { get; }
    public string Text { get; }
    public int Start { get; }
    public int Length { get; }

    public SyntaxToken(int kind, string text, int start, int length)
    {
        Kind = kind;
        Text = text;
        Start = start;
        Length = length;
    }

    public bool IsMissing()
    {
        return Kind == 0;
    }

    public override string ToString()
    {
        return Text;
    }
}
