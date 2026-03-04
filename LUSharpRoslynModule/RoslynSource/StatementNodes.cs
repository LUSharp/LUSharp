namespace RoslynLuau;

public class BlockSyntax : StatementSyntax
{
    public StatementSyntax[] Statements { get; }

    public BlockSyntax(StatementSyntax[] statements) : base(8792)
    {
        Statements = statements;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitBlock(this);
    }

    public override string Accept()
    {
        string result = "{\n";
        for (int i = 0; i < Statements.Length; i++)
        {
            result = result + "  " + Statements[i].Accept() + "\n";
        }
        result = result + "}";
        return result;
    }
}

public class ReturnStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Expression { get; }

    public ReturnStatementSyntax(ExpressionSyntax expression) : base(8805)
    {
        Expression = expression;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitReturnStatement(this);
    }

    public override string Accept()
    {
        if (Expression == null)
            return "return;";
        return "return " + Expression.Accept() + ";";
    }
}

public class ExpressionStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Expression { get; }

    public ExpressionStatementSyntax(ExpressionSyntax expression) : base(8797)
    {
        Expression = expression;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitExpressionStatement(this);
    }

    public override string Accept()
    {
        return Expression.Accept() + ";";
    }
}

public class LocalDeclarationStatementSyntax : StatementSyntax
{
    public string TypeName { get; }
    public string VariableName { get; }
    public ExpressionSyntax Initializer { get; }

    public LocalDeclarationStatementSyntax(string typeName, string variableName, ExpressionSyntax initializer) : base(8793)
    {
        TypeName = typeName;
        VariableName = variableName;
        Initializer = initializer;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitLocalDeclaration(this);
    }

    public override string Accept()
    {
        if (Initializer == null)
            return TypeName + " " + VariableName + ";";
        return TypeName + " " + VariableName + " = " + Initializer.Accept() + ";";
    }
}

public class IfStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Condition { get; }
    public StatementSyntax ThenBody { get; }
    public StatementSyntax ElseBody { get; }

    public IfStatementSyntax(ExpressionSyntax condition, StatementSyntax thenBody, StatementSyntax elseBody) : base(8803)
    {
        Condition = condition;
        ThenBody = thenBody;
        ElseBody = elseBody;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitIfStatement(this);
    }

    public override string Accept()
    {
        string result = "if (" + Condition.Accept() + ") " + ThenBody.Accept();
        if (ElseBody != null)
        {
            result = result + " else " + ElseBody.Accept();
        }
        return result;
    }
}

public class WhileStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Condition { get; }
    public StatementSyntax Body { get; }

    public WhileStatementSyntax(ExpressionSyntax condition, StatementSyntax body) : base(8802)
    {
        Condition = condition;
        Body = body;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitWhileStatement(this);
    }

    public override string Accept()
    {
        return "while (" + Condition.Accept() + ") " + Body.Accept();
    }
}
