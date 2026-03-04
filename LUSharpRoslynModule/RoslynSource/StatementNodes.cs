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

public class ForStatementSyntax : StatementSyntax
{
    public StatementSyntax Declaration { get; }
    public ExpressionSyntax Condition { get; }
    public ExpressionSyntax[] Incrementors { get; }
    public StatementSyntax Body { get; }

    public ForStatementSyntax(StatementSyntax declaration, ExpressionSyntax condition, ExpressionSyntax[] incrementors, StatementSyntax body) : base(8811)
    {
        Declaration = declaration;
        Condition = condition;
        Incrementors = incrementors;
        Body = body;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitForStatement(this);
    }

    public override string Accept()
    {
        string init = Declaration != null ? Declaration.Accept() : ";";
        string cond = Condition != null ? Condition.Accept() : "";
        string inc = "";
        for (int i = 0; i < Incrementors.Length; i++)
        {
            if (i > 0) inc = inc + ", ";
            inc = inc + Incrementors[i].Accept();
        }
        return "for (" + init + " " + cond + "; " + inc + ") " + Body.Accept();
    }
}

public class ForEachStatementSyntax : StatementSyntax
{
    public string TypeName { get; }
    public string Identifier { get; }
    public ExpressionSyntax Expression { get; }
    public StatementSyntax Body { get; }

    public ForEachStatementSyntax(string typeName, string identifier, ExpressionSyntax expression, StatementSyntax body) : base(8812)
    {
        TypeName = typeName;
        Identifier = identifier;
        Expression = expression;
        Body = body;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitForEachStatement(this);
    }

    public override string Accept()
    {
        return "foreach (" + TypeName + " " + Identifier + " in " + Expression.Accept() + ") " + Body.Accept();
    }
}

public class DoStatementSyntax : StatementSyntax
{
    public StatementSyntax Body { get; }
    public ExpressionSyntax Condition { get; }

    public DoStatementSyntax(StatementSyntax body, ExpressionSyntax condition) : base(8810)
    {
        Body = body;
        Condition = condition;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitDoStatement(this);
    }

    public override string Accept()
    {
        return "do " + Body.Accept() + " while (" + Condition.Accept() + ");";
    }
}

public class BreakStatementSyntax : StatementSyntax
{
    public BreakStatementSyntax() : base(8803)
    {
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitBreakStatement(this);
    }

    public override string Accept()
    {
        return "break;";
    }
}

public class ContinueStatementSyntax : StatementSyntax
{
    public ContinueStatementSyntax() : base(8804)
    {
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitContinueStatement(this);
    }

    public override string Accept()
    {
        return "continue;";
    }
}

public class SwitchStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Expression { get; }
    public SwitchSectionSyntax[] Sections { get; }

    public SwitchStatementSyntax(ExpressionSyntax expression, SwitchSectionSyntax[] sections) : base(8821)
    {
        Expression = expression;
        Sections = sections;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitSwitchStatement(this);
    }

    public override string Accept()
    {
        string result = "switch (" + Expression.Accept() + ") {";
        for (int i = 0; i < Sections.Length; i++)
            result = result + "\n" + Sections[i].Accept();
        result = result + "\n}";
        return result;
    }
}

public class SwitchSectionSyntax : SyntaxNode
{
    public ExpressionSyntax[] Labels { get; } // null entry = default label
    public StatementSyntax[] Statements { get; }

    public SwitchSectionSyntax(ExpressionSyntax[] labels, StatementSyntax[] statements) : base(8822)
    {
        Labels = labels;
        Statements = statements;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitSwitchSection(this);
    }

    public override string Accept()
    {
        string result = "";
        for (int i = 0; i < Labels.Length; i++)
        {
            if (Labels[i] == null)
                result = result + "default:\n";
            else
                result = result + "case " + Labels[i].Accept() + ":\n";
        }
        for (int i = 0; i < Statements.Length; i++)
            result = result + "  " + Statements[i].Accept() + "\n";
        return result;
    }
}

public class TryStatementSyntax : StatementSyntax
{
    public BlockSyntax Block { get; }
    public CatchClauseSyntax[] Catches { get; }
    public BlockSyntax FinallyBlock { get; }

    public TryStatementSyntax(BlockSyntax block, CatchClauseSyntax[] catches, BlockSyntax finallyBlock) : base(8825)
    {
        Block = block;
        Catches = catches;
        FinallyBlock = finallyBlock;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitTryStatement(this);
    }

    public override string Accept()
    {
        string result = "try " + Block.Accept();
        for (int i = 0; i < Catches.Length; i++)
            result = result + " " + Catches[i].Accept();
        if (FinallyBlock != null)
            result = result + " finally " + FinallyBlock.Accept();
        return result;
    }
}

public class CatchClauseSyntax : SyntaxNode
{
    public string ExceptionTypeName { get; }
    public string Identifier { get; }
    public BlockSyntax Block { get; }

    public CatchClauseSyntax(string exceptionTypeName, string identifier, BlockSyntax block) : base(8826)
    {
        ExceptionTypeName = exceptionTypeName;
        Identifier = identifier;
        Block = block;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitCatchClause(this);
    }

    public override string Accept()
    {
        if (ExceptionTypeName != null)
        {
            string decl = ExceptionTypeName;
            if (Identifier != null) decl = decl + " " + Identifier;
            return "catch (" + decl + ") " + Block.Accept();
        }
        return "catch " + Block.Accept();
    }
}

public class ThrowStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Expression { get; }

    public ThrowStatementSyntax(ExpressionSyntax expression) : base(8808)
    {
        Expression = expression;
    }

    public override void AcceptWalker(SyntaxWalker walker)
    {
        walker.VisitThrowStatement(this);
    }

    public override string Accept()
    {
        if (Expression == null)
            return "throw;";
        return "throw " + Expression.Accept() + ";";
    }
}
