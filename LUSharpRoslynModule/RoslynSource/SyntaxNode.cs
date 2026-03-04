namespace RoslynLuau;

public abstract class SyntaxNode
{
    public int Kind { get; }
    public int Start { get; set; }
    public int Length { get; set; }

    protected SyntaxNode(int kind)
    {
        Kind = kind;
    }

    public abstract string Accept();

    public virtual void AcceptWalker(SyntaxWalker walker)
    {
        walker.DefaultVisit(this);
    }

    public virtual string ToDisplayString()
    {
        return "SyntaxNode(" + Kind + ")";
    }
}

public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(int kind) : base(kind)
    {
    }
}

public abstract class StatementSyntax : SyntaxNode
{
    protected StatementSyntax(int kind) : base(kind)
    {
    }
}

public abstract class MemberDeclarationSyntax : SyntaxNode
{
    protected MemberDeclarationSyntax(int kind) : base(kind)
    {
    }
}
