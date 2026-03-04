namespace RoslynLuau;

/// <summary>
/// Base class for walking a SyntaxNode tree.
/// Override specific Visit methods to process nodes of interest.
/// Call base.Visit*() to continue walking children.
/// Uses double-dispatch via AcceptWalker on each node type.
/// </summary>
public class SyntaxWalker
{
    protected int _depth;

    public SyntaxWalker()
    {
        _depth = 0;
    }

    public int Depth { get { return _depth; } }

    public virtual void Visit(SyntaxNode node)
    {
        if (node == null) return;
        node.AcceptWalker(this);
    }

    public virtual void DefaultVisit(SyntaxNode node)
    {
        // Base implementation: do nothing
    }

    // === Declaration visitors ===

    public virtual void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        for (int i = 0; i < node.Members.Length; i++)
        {
            _depth++;
            Visit(node.Members[i]);
            _depth--;
        }
    }

    public virtual void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        for (int i = 0; i < node.Members.Length; i++)
        {
            _depth++;
            Visit(node.Members[i]);
            _depth--;
        }
    }

    public virtual void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        for (int i = 0; i < node.Members.Length; i++)
        {
            _depth++;
            Visit(node.Members[i]);
            _depth--;
        }
    }

    public virtual void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        // Enum members are not SyntaxNodes, so no recursion
    }

    public virtual void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body != null)
        {
            _depth++;
            Visit(node.Body);
            _depth--;
        }
    }

    public virtual void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (node.InitializerArguments != null)
        {
            for (int i = 0; i < node.InitializerArguments.Length; i++)
            {
                _depth++;
                Visit(node.InitializerArguments[i]);
                _depth--;
            }
        }
        if (node.Body != null)
        {
            _depth++;
            Visit(node.Body);
            _depth--;
        }
    }

    public virtual void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (node.Initializer != null)
        {
            _depth++;
            Visit(node.Initializer);
            _depth--;
        }
    }

    public virtual void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.ExpressionBody != null)
        {
            _depth++;
            Visit(node.ExpressionBody);
            _depth--;
        }
    }

    // === Statement visitors ===

    public virtual void VisitBlock(BlockSyntax node)
    {
        for (int i = 0; i < node.Statements.Length; i++)
        {
            _depth++;
            Visit(node.Statements[i]);
            _depth--;
        }
    }

    public virtual void VisitReturnStatement(ReturnStatementSyntax node)
    {
        if (node.Expression != null)
        {
            _depth++;
            Visit(node.Expression);
            _depth--;
        }
    }

    public virtual void VisitIfStatement(IfStatementSyntax node)
    {
        _depth++;
        Visit(node.Condition);
        _depth--;
        _depth++;
        Visit(node.ThenBody);
        _depth--;
        if (node.ElseBody != null)
        {
            _depth++;
            Visit(node.ElseBody);
            _depth--;
        }
    }

    public virtual void VisitWhileStatement(WhileStatementSyntax node)
    {
        _depth++;
        Visit(node.Condition);
        _depth--;
        _depth++;
        Visit(node.Body);
        _depth--;
    }

    public virtual void VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
    }

    public virtual void VisitLocalDeclaration(LocalDeclarationStatementSyntax node)
    {
        if (node.Initializer != null)
        {
            _depth++;
            Visit(node.Initializer);
            _depth--;
        }
    }

    public virtual void VisitForStatement(ForStatementSyntax node)
    {
        if (node.Declaration != null)
        {
            _depth++;
            Visit(node.Declaration);
            _depth--;
        }
        if (node.Condition != null)
        {
            _depth++;
            Visit(node.Condition);
            _depth--;
        }
        for (int i = 0; i < node.Incrementors.Length; i++)
        {
            _depth++;
            Visit(node.Incrementors[i]);
            _depth--;
        }
        _depth++;
        Visit(node.Body);
        _depth--;
    }

    public virtual void VisitForEachStatement(ForEachStatementSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
        _depth++;
        Visit(node.Body);
        _depth--;
    }

    public virtual void VisitDoStatement(DoStatementSyntax node)
    {
        _depth++;
        Visit(node.Body);
        _depth--;
        _depth++;
        Visit(node.Condition);
        _depth--;
    }

    public virtual void VisitBreakStatement(BreakStatementSyntax node)
    {
        DefaultVisit(node);
    }

    public virtual void VisitContinueStatement(ContinueStatementSyntax node)
    {
        DefaultVisit(node);
    }

    public virtual void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
        for (int i = 0; i < node.Sections.Length; i++)
        {
            _depth++;
            Visit(node.Sections[i]);
            _depth--;
        }
    }

    public virtual void VisitSwitchSection(SwitchSectionSyntax node)
    {
        for (int i = 0; i < node.Labels.Length; i++)
        {
            if (node.Labels[i] != null)
            {
                _depth++;
                Visit(node.Labels[i]);
                _depth--;
            }
        }
        for (int i = 0; i < node.Statements.Length; i++)
        {
            _depth++;
            Visit(node.Statements[i]);
            _depth--;
        }
    }

    public virtual void VisitTryStatement(TryStatementSyntax node)
    {
        _depth++;
        Visit(node.Block);
        _depth--;
        for (int i = 0; i < node.Catches.Length; i++)
        {
            _depth++;
            Visit(node.Catches[i]);
            _depth--;
        }
        if (node.FinallyBlock != null)
        {
            _depth++;
            Visit(node.FinallyBlock);
            _depth--;
        }
    }

    public virtual void VisitCatchClause(CatchClauseSyntax node)
    {
        _depth++;
        Visit(node.Block);
        _depth--;
    }

    public virtual void VisitThrowStatement(ThrowStatementSyntax node)
    {
        if (node.Expression != null)
        {
            _depth++;
            Visit(node.Expression);
            _depth--;
        }
    }

    // === Expression visitors ===

    public virtual void VisitLiteralExpression(LiteralExpressionSyntax node) { }
    public virtual void VisitIdentifierName(IdentifierNameSyntax node) { }

    public virtual void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        _depth++;
        Visit(node.Left);
        _depth--;
        _depth++;
        Visit(node.Right);
        _depth--;
    }

    public virtual void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        _depth++;
        Visit(node.Operand);
        _depth--;
    }

    public virtual void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
    }

    public virtual void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
        for (int i = 0; i < node.Arguments.Length; i++)
        {
            _depth++;
            Visit(node.Arguments[i]);
            _depth--;
        }
    }

    public virtual void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
    }

    public virtual void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        _depth++;
        Visit(node.Left);
        _depth--;
        _depth++;
        Visit(node.Right);
        _depth--;
    }

    public virtual void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        for (int i = 0; i < node.Arguments.Length; i++)
        {
            _depth++;
            Visit(node.Arguments[i]);
            _depth--;
        }
    }

    public virtual void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        if (node.SizeExpression != null)
        {
            _depth++;
            Visit(node.SizeExpression);
            _depth--;
        }
    }

    public virtual void VisitLambdaExpression(LambdaExpressionSyntax node)
    {
        if (node.ExpressionBody != null)
        {
            _depth++;
            Visit(node.ExpressionBody);
            _depth--;
        }
        if (node.BlockBody != null)
        {
            _depth++;
            Visit(node.BlockBody);
            _depth--;
        }
    }

    public virtual void VisitCastExpression(CastExpressionSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
    }

    public virtual void VisitElementAccess(ElementAccessExpressionSyntax node)
    {
        _depth++;
        Visit(node.Expression);
        _depth--;
        _depth++;
        Visit(node.Index);
        _depth--;
    }

    public virtual void VisitPostfixUnary(PostfixUnaryExpressionSyntax node)
    {
        _depth++;
        Visit(node.Operand);
        _depth--;
    }

    public virtual void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        _depth++;
        Visit(node.Condition);
        _depth--;
        _depth++;
        Visit(node.WhenTrue);
        _depth--;
        _depth++;
        Visit(node.WhenFalse);
        _depth--;
    }

    public virtual void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        _depth++;
        Visit(node.GoverningExpression);
        _depth--;
        for (int i = 0; i < node.Arms.Length; i++)
        {
            _depth++;
            node.Arms[i].AcceptWalker(this);
            _depth--;
        }
    }

    public virtual void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        if (node.Pattern != null)
        {
            _depth++;
            Visit(node.Pattern);
            _depth--;
        }
        _depth++;
        Visit(node.Expression);
        _depth--;
    }
}

/// <summary>
/// Concrete walker that prints an indented tree of node types.
/// </summary>
public class TreePrinter : SyntaxWalker
{
    private string _output;

    public TreePrinter()
    {
        _output = "";
    }

    public string GetOutput()
    {
        return _output;
    }

    private void PrintNode(string text)
    {
        for (int i = 0; i < _depth; i++)
            _output = _output + "  ";
        _output = _output + text + "\n";
    }

    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        PrintNode("CompilationUnit");
        base.VisitCompilationUnit(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        PrintNode("ClassDeclaration: " + node.Name);
        base.VisitClassDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        PrintNode("StructDeclaration: " + node.Name);
        base.VisitStructDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        PrintNode("EnumDeclaration: " + node.Name + " (" + node.Members.Length + " members)");
        base.VisitEnumDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        string mods = "";
        if (node.IsStatic) mods = "static ";
        PrintNode("MethodDeclaration: " + mods + node.ReturnType + " " + node.Name);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        PrintNode("ConstructorDeclaration: " + node.Name);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        string mods = "";
        if (node.IsStatic) mods = "static ";
        PrintNode("FieldDeclaration: " + mods + node.TypeName + " " + node.Name);
        base.VisitFieldDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        string accessors = "";
        if (node.HasGetter) accessors = accessors + "get";
        if (node.HasSetter)
        {
            if (accessors.Length > 0) accessors = accessors + ",";
            accessors = accessors + "set";
        }
        PrintNode("PropertyDeclaration: " + node.TypeName + " " + node.Name + " {" + accessors + "}");
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitBlock(BlockSyntax node)
    {
        PrintNode("Block (" + node.Statements.Length + " statements)");
        base.VisitBlock(node);
    }

    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
        PrintNode("ReturnStatement");
        base.VisitReturnStatement(node);
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        string hasElse = "";
        if (node.ElseBody != null) hasElse = " +else";
        PrintNode("IfStatement" + hasElse);
        base.VisitIfStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        PrintNode("WhileStatement");
        base.VisitWhileStatement(node);
    }

    public override void VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        PrintNode("ExpressionStatement");
        base.VisitExpressionStatement(node);
    }

    public override void VisitLocalDeclaration(LocalDeclarationStatementSyntax node)
    {
        PrintNode("LocalDeclaration: " + node.TypeName + " " + node.VariableName);
        base.VisitLocalDeclaration(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        PrintNode("ForStatement");
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        PrintNode("ForEachStatement: " + node.TypeName + " " + node.Identifier);
        base.VisitForEachStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        PrintNode("DoStatement");
        base.VisitDoStatement(node);
    }

    public override void VisitBreakStatement(BreakStatementSyntax node)
    {
        PrintNode("BreakStatement");
        base.VisitBreakStatement(node);
    }

    public override void VisitContinueStatement(ContinueStatementSyntax node)
    {
        PrintNode("ContinueStatement");
        base.VisitContinueStatement(node);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        PrintNode("SwitchStatement");
        base.VisitSwitchStatement(node);
    }

    public override void VisitSwitchSection(SwitchSectionSyntax node)
    {
        int caseCount = 0;
        bool hasDefault = false;
        for (int i = 0; i < node.Labels.Length; i++)
        {
            if (node.Labels[i] == null) hasDefault = true;
            else caseCount++;
        }
        string desc = caseCount + " case(s)";
        if (hasDefault) desc = desc + " +default";
        PrintNode("SwitchSection: " + desc);
        base.VisitSwitchSection(node);
    }

    public override void VisitTryStatement(TryStatementSyntax node)
    {
        string desc = "TryStatement";
        if (node.Catches.Length > 0) desc = desc + " (" + node.Catches.Length + " catch)";
        if (node.FinallyBlock != null) desc = desc + " +finally";
        PrintNode(desc);
        base.VisitTryStatement(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        string desc = "CatchClause";
        if (node.ExceptionTypeName != null)
        {
            desc = desc + ": " + node.ExceptionTypeName;
            if (node.Identifier != null) desc = desc + " " + node.Identifier;
        }
        PrintNode(desc);
        base.VisitCatchClause(node);
    }

    public override void VisitThrowStatement(ThrowStatementSyntax node)
    {
        string rethrow = node.Expression == null ? " (re-throw)" : "";
        PrintNode("ThrowStatement" + rethrow);
        base.VisitThrowStatement(node);
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        PrintNode("Literal: " + node.Token.Text);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        PrintNode("Identifier: " + node.Identifier.Text);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        PrintNode("BinaryExpression: " + node.OperatorToken.Text);
        base.VisitBinaryExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        PrintNode("PrefixUnary: " + node.OperatorToken.Text);
        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        PrintNode("ParenthesizedExpression");
        base.VisitParenthesizedExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        PrintNode("InvocationExpression");
        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        PrintNode("MemberAccess: ." + node.Name.Text);
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        PrintNode("Assignment: " + node.OperatorToken.Text);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        PrintNode("ObjectCreation: " + node.TypeName);
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        PrintNode("ArrayCreation: " + node.TypeName);
        base.VisitArrayCreationExpression(node);
    }

    public override void VisitLambdaExpression(LambdaExpressionSyntax node)
    {
        PrintNode("Lambda (" + node.ParameterNames.Length + " params)");
        base.VisitLambdaExpression(node);
    }

    public override void VisitCastExpression(CastExpressionSyntax node)
    {
        PrintNode("Cast: " + node.TypeName);
        base.VisitCastExpression(node);
    }

    public override void VisitElementAccess(ElementAccessExpressionSyntax node)
    {
        PrintNode("ElementAccess");
        base.VisitElementAccess(node);
    }

    public override void VisitPostfixUnary(PostfixUnaryExpressionSyntax node)
    {
        string opText = node.OperatorKind == Microsoft.CodeAnalysis.CSharp.SyntaxKind.PlusPlusToken ? "++" : "--";
        PrintNode("PostfixUnary: " + opText);
        base.VisitPostfixUnary(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        PrintNode("ConditionalExpression");
        base.VisitConditionalExpression(node);
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        PrintNode("SwitchExpression (" + node.Arms.Length + " arms)");
        base.VisitSwitchExpression(node);
    }

    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        string label = node.Pattern != null ? "case" : "default";
        PrintNode("SwitchArm: " + label);
        base.VisitSwitchExpressionArm(node);
    }
}
