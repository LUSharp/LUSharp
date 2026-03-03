namespace RoslynLuau;

public abstract class SyntaxNode
{
    public int Kind { get; set; }

    public SyntaxNode(int kind)
    {
        Kind = kind;
    }

    public abstract string GetDisplayText();

    public virtual string Describe()
    {
        return "SyntaxNode(Kind=" + Kind + ")";
    }
}

public class ExpressionNode : SyntaxNode
{
    public string Value { get; set; }

    public ExpressionNode(int kind, string value) : base(kind)
    {
        Value = value;
    }

    public override string GetDisplayText()
    {
        return Value;
    }

    public override string Describe()
    {
        return "ExpressionNode(Kind=" + Kind + ", Value=" + Value + ")";
    }
}

public class LiteralExpression : ExpressionNode
{
    public LiteralExpression(string value) : base(8750, value)
    {
    }

    public override string GetDisplayText()
    {
        return "Literal:" + Value;
    }
}
