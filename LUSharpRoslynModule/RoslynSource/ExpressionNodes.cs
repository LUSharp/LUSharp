namespace RoslynLuau;

public class LiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Token { get; }

    public LiteralExpressionSyntax(int kind, SyntaxToken token) : base(kind)
    {
        Token = token;
    }

    public override string Accept()
    {
        return Token.Text;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitLiteralExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Literal(" + Token.Text + ")";
    }
}

public class IdentifierNameSyntax : ExpressionSyntax
{
    public SyntaxToken Identifier { get; }

    public IdentifierNameSyntax(SyntaxToken identifier) : base(8616)
    {
        Identifier = identifier;
    }

    public override string Accept()
    {
        return Identifier.Text;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitIdentifierName(this);
    }

    public override string ToDisplayString()
    {
        return "Identifier(" + Identifier.Text + ")";
    }
}

public class BinaryExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Left { get; }
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public BinaryExpressionSyntax(int kind, ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right) : base(kind)
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public override string Accept()
    {
        return Left.Accept() + " " + OperatorToken.Text + " " + Right.Accept();
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitBinaryExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Binary(" + Left.ToDisplayString() + " " + OperatorToken.Text + " " + Right.ToDisplayString() + ")";
    }
}

public class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }

    public ParenthesizedExpressionSyntax(ExpressionSyntax expression) : base(8632)
    {
        Expression = expression;
    }

    public override string Accept()
    {
        return "(" + Expression.Accept() + ")";
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitParenthesizedExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Paren(" + Expression.ToDisplayString() + ")";
    }
}

public class PrefixUnaryExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Operand { get; }

    public PrefixUnaryExpressionSyntax(int kind, SyntaxToken operatorToken, ExpressionSyntax operand) : base(kind)
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public override string Accept()
    {
        return OperatorToken.Text + Operand.Accept();
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitPrefixUnaryExpression(this);
    }

    public override string ToDisplayString()
    {
        return "PrefixUnary(" + OperatorToken.Text + Operand.ToDisplayString() + ")";
    }
}

public class InvocationExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }
    public ExpressionSyntax[] Arguments { get; }

    public InvocationExpressionSyntax(ExpressionSyntax expression, ExpressionSyntax[] arguments) : base(8634)
    {
        Expression = expression;
        Arguments = arguments;
    }

    public override string Accept()
    {
        string args = "";
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) args = args + ", ";
            args = args + Arguments[i].Accept();
        }
        return Expression.Accept() + "(" + args + ")";
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitInvocationExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Invocation(" + Expression.ToDisplayString() + ")";
    }
}

public class MemberAccessExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }
    public SyntaxToken Name { get; }

    public MemberAccessExpressionSyntax(ExpressionSyntax expression, SyntaxToken name) : base(8689)
    {
        Expression = expression;
        Name = name;
    }

    public override string Accept()
    {
        return Expression.Accept() + "." + Name.Text;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitMemberAccessExpression(this);
    }

    public override string ToDisplayString()
    {
        return "MemberAccess(" + Expression.ToDisplayString() + "." + Name.Text + ")";
    }
}

public class AssignmentExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Left { get; }
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public AssignmentExpressionSyntax(int kind, ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right) : base(kind)
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public override string Accept()
    {
        return Left.Accept() + " " + OperatorToken.Text + " " + Right.Accept();
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitAssignmentExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Assignment(" + Left.ToDisplayString() + " " + OperatorToken.Text + " " + Right.ToDisplayString() + ")";
    }
}

public class ObjectCreationExpressionSyntax : ExpressionSyntax
{
    public string TypeName { get; }
    public ExpressionSyntax[] Arguments { get; }

    public ObjectCreationExpressionSyntax(string typeName, ExpressionSyntax[] arguments) : base(8649)
    {
        TypeName = typeName;
        Arguments = arguments;
    }

    public override string Accept()
    {
        string args = "";
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) args += ", ";
            args += Arguments[i].Accept();
        }
        return "new " + TypeName + "(" + args + ")";
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitObjectCreationExpression(this);
    }

    public override string ToDisplayString()
    {
        return "New(" + TypeName + ")";
    }
}

public class LambdaExpressionSyntax : ExpressionSyntax
{
    public string[] ParameterNames { get; }
    public ExpressionSyntax ExpressionBody { get; }
    public StatementSyntax BlockBody { get; }

    public LambdaExpressionSyntax(string[] parameterNames, ExpressionSyntax expressionBody, StatementSyntax blockBody) : base(8643)
    {
        ParameterNames = parameterNames;
        ExpressionBody = expressionBody;
        BlockBody = blockBody;
    }

    public override string Accept()
    {
        string parms = "";
        if (ParameterNames.Length == 1)
        {
            parms = ParameterNames[0];
        }
        else
        {
            parms = "(";
            for (int i = 0; i < ParameterNames.Length; i++)
            {
                if (i > 0) parms += ", ";
                parms += ParameterNames[i];
            }
            parms += ")";
        }

        if (ExpressionBody != null)
            return parms + " => " + ExpressionBody.Accept();
        if (BlockBody != null)
            return parms + " => " + BlockBody.Accept();
        return parms + " => {}";
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitLambdaExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Lambda(" + ParameterNames.Length + " params)";
    }
}

public class ElementAccessExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }
    public ExpressionSyntax Index { get; }

    public ElementAccessExpressionSyntax(ExpressionSyntax expression, ExpressionSyntax index) : base((int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.ElementAccessExpression)
    {
        Expression = expression;
        Index = index;
    }

    public override string Accept()
    {
        return Expression.Accept() + "[" + Index.Accept() + "]";
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitElementAccess(this);
    }

    public override string ToDisplayString()
    {
        return "ElementAccess(" + Expression.ToDisplayString() + "[" + Index.ToDisplayString() + "])";
    }
}

public class PostfixUnaryExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Operand { get; }
    public Microsoft.CodeAnalysis.CSharp.SyntaxKind OperatorKind { get; }

    public PostfixUnaryExpressionSyntax(int kind, ExpressionSyntax operand, Microsoft.CodeAnalysis.CSharp.SyntaxKind operatorKind) : base(kind)
    {
        Operand = operand;
        OperatorKind = operatorKind;
    }

    public override string Accept()
    {
        string opText = OperatorKind == Microsoft.CodeAnalysis.CSharp.SyntaxKind.PlusPlusToken ? "++" : "--";
        return Operand.Accept() + opText;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitPostfixUnary(this);
    }

    public override string ToDisplayString()
    {
        string opText = OperatorKind == Microsoft.CodeAnalysis.CSharp.SyntaxKind.PlusPlusToken ? "++" : "--";
        return "PostfixUnary(" + Operand.ToDisplayString() + opText + ")";
    }
}

public class ConditionalExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Condition { get; }
    public ExpressionSyntax WhenTrue { get; }
    public ExpressionSyntax WhenFalse { get; }

    public ConditionalExpressionSyntax(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse) : base((int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConditionalExpression)
    {
        Condition = condition;
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    public override string Accept()
    {
        return Condition.Accept() + " ? " + WhenTrue.Accept() + " : " + WhenFalse.Accept();
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitConditionalExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Conditional(" + Condition.ToDisplayString() + " ? " + WhenTrue.ToDisplayString() + " : " + WhenFalse.ToDisplayString() + ")";
    }
}

public class CastExpressionSyntax : ExpressionSyntax
{
    public string TypeName { get; }
    public ExpressionSyntax Expression { get; }

    public CastExpressionSyntax(string typeName, ExpressionSyntax expression) : base(8640)
    {
        TypeName = typeName;
        Expression = expression;
    }

    public override string Accept()
    {
        return "(" + TypeName + ")" + Expression.Accept();
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitCastExpression(this);
    }

    public override string ToDisplayString()
    {
        return "Cast(" + TypeName + ")";
    }
}
