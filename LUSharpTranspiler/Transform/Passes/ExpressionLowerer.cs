using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class ExpressionLowerer
{
    private readonly TypeResolver _types;

    public ExpressionLowerer(TypeResolver types) => _types = types;

    public ILuaExpression Lower(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit                               => LowerLiteral(lit),
        IdentifierNameSyntax id                                   => new LuaIdent(id.Identifier.Text),
        InterpolatedStringExpressionSyntax i                      => LowerInterpolated(i),
        BinaryExpressionSyntax bin                                => LowerBinary(bin),
        PrefixUnaryExpressionSyntax pre                           => LowerPrefixUnary(pre),
        PostfixUnaryExpressionSyntax post                         => LowerPostfixUnary(post),
        MemberAccessExpressionSyntax mem                          => LowerMemberAccess(mem),
        InvocationExpressionSyntax inv                            => LowerInvocation(inv),
        ObjectCreationExpressionSyntax oc                         => LowerObjectCreation(oc),
        ImplicitObjectCreationExpressionSyntax ic                 => LowerImplicitCreation(ic),
        ConditionalExpressionSyntax cond                          => LowerTernary(cond),
        ConditionalAccessExpressionSyntax ca                      => LowerNullSafe(ca),
        ElementAccessExpressionSyntax el                          => LowerIndex(el),
        AssignmentExpressionSyntax assign                         => LowerAssignmentExpr(assign),
        ParenthesizedLambdaExpressionSyntax lam                   => LowerLambda(lam.ParameterList, lam.Body),
        SimpleLambdaExpressionSyntax lam                          => LowerSimpleLambda(lam),
        ParenthesizedExpressionSyntax par                         => Lower(par.Expression),
        CastExpressionSyntax cast                                 => Lower(cast.Expression), // strip casts
        _                                                         => new LuaLiteral($"--[[unsupported: {expr.Kind()}]]")
    };

    private static LuaLiteral LowerLiteral(LiteralExpressionSyntax lit) => lit.Kind() switch
    {
        SyntaxKind.StringLiteralExpression  => new LuaLiteral($"\"{EscapeString(lit.Token.ValueText)}\""),
        SyntaxKind.NumericLiteralExpression => new LuaLiteral(lit.Token.Text),
        SyntaxKind.TrueLiteralExpression    => LuaLiteral.True,
        SyntaxKind.FalseLiteralExpression   => LuaLiteral.False,
        SyntaxKind.NullLiteralExpression    => LuaLiteral.Nil,
        _                                   => new LuaLiteral(lit.Token.Text)
    };

    private static LuaInterp LowerInterpolated(InterpolatedStringExpressionSyntax interp)
    {
        var sb = new System.Text.StringBuilder("`");
        foreach (var content in interp.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
                sb.Append(text.TextToken.ValueText);
            else if (content is InterpolationSyntax hole)
                sb.Append($"{{{hole.Expression}}}");
        }
        sb.Append('`');
        return new LuaInterp(sb.ToString());
    }

    private ILuaExpression LowerBinary(BinaryExpressionSyntax bin)
    {
        // String concatenation via + → ..
        if (bin.IsKind(SyntaxKind.AddExpression))
        {
            // Flatten concat chains
            var parts = FlattenConcat(bin);
            if (parts.Count > 1) return new LuaConcat(parts);
        }

        var left = Lower(bin.Left);
        var right = Lower(bin.Right);

        var op = bin.Kind() switch
        {
            SyntaxKind.AddExpression                  => "+",
            SyntaxKind.SubtractExpression             => "-",
            SyntaxKind.MultiplyExpression             => "*",
            SyntaxKind.DivideExpression               => "/",
            SyntaxKind.ModuloExpression               => "%",
            SyntaxKind.EqualsExpression               => "==",
            SyntaxKind.NotEqualsExpression            => "~=",
            SyntaxKind.LessThanExpression             => "<",
            SyntaxKind.LessThanOrEqualExpression      => "<=",
            SyntaxKind.GreaterThanExpression          => ">",
            SyntaxKind.GreaterThanOrEqualExpression   => ">=",
            SyntaxKind.LogicalAndExpression           => "and",
            SyntaxKind.LogicalOrExpression            => "or",
            SyntaxKind.CoalesceExpression             => null, // handled below
            _                                         => bin.OperatorToken.Text
        };

        if (op == null) return LowerCoalesce(bin);
        return new LuaBinary(left, op, right);
    }

    private List<ILuaExpression> FlattenConcat(BinaryExpressionSyntax bin)
    {
        var parts = new List<ILuaExpression>();
        if (bin.Left is BinaryExpressionSyntax lb && lb.IsKind(SyntaxKind.AddExpression))
            parts.AddRange(FlattenConcat(lb));
        else
            parts.Add(Lower(bin.Left));
        parts.Add(Lower(bin.Right));
        return parts;
    }

    private ILuaExpression LowerPrefixUnary(PrefixUnaryExpressionSyntax pre)
    {
        var op = pre.Kind() switch
        {
            SyntaxKind.LogicalNotExpression  => "not",
            SyntaxKind.UnaryMinusExpression  => "-",
            SyntaxKind.PreIncrementExpression => null, // x++ → handled as assign
            _                                => pre.OperatorToken.Text
        };
        if (op == null) return Lower(pre.Operand); // fallback
        return new LuaUnary(op, Lower(pre.Operand));
    }

    private ILuaExpression LowerPostfixUnary(PostfixUnaryExpressionSyntax post) =>
        Lower(post.Operand); // i++ treated as expression value (statement handles increment)

    private ILuaExpression LowerMemberAccess(MemberAccessExpressionSyntax mem) =>
        new LuaMember(Lower(mem.Expression), mem.Name.Identifier.Text);

    private ILuaExpression LowerInvocation(InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList.Arguments
            .Select(a => Lower(a.Expression)).ToList();

        if (inv.Expression is MemberAccessExpressionSyntax mem)
        {
            // Map Console.WriteLine → print
            var obj = mem.Expression.ToString();
            var method = mem.Name.Identifier.Text;
            if (obj == "Console" && method == "WriteLine")
                return new LuaCall(new LuaIdent("print"), args);

            return new LuaMethodCall(Lower(mem.Expression), method, args);
        }

        return new LuaCall(Lower(inv.Expression), args);
    }

    private ILuaExpression LowerObjectCreation(ObjectCreationExpressionSyntax oc)
    {
        var className = oc.Type.ToString();
        var args = oc.ArgumentList?.Arguments
            .Select(a => Lower(a.Expression)).ToList() ?? new();
        return new LuaNew(className, args);
    }

    private ILuaExpression LowerImplicitCreation(ImplicitObjectCreationExpressionSyntax ic)
    {
        // new() { ... } — typically a collection initializer
        if (ic.Initializer != null)
            return LowerInitializer(ic.Initializer);
        return LuaTable.Empty;
    }

    private LuaTable LowerInitializer(InitializerExpressionSyntax init)
    {
        var entries = new List<LuaTableEntry>();
        foreach (var expr in init.Expressions)
        {
            if (expr is InitializerExpressionSyntax kv && kv.Expressions.Count == 2)
            {
                var k = kv.Expressions[0].ToString().Trim('"');
                var v = Lower(kv.Expressions[1]);
                entries.Add(new LuaTableEntry(k, v));
            }
            else
            {
                entries.Add(new LuaTableEntry(null, Lower(expr)));
            }
        }
        return new LuaTable(entries);
    }

    private ILuaExpression LowerTernary(ConditionalExpressionSyntax c) =>
        new LuaTernary(Lower(c.Condition), Lower(c.WhenTrue), Lower(c.WhenFalse));

    private ILuaExpression LowerNullSafe(ConditionalAccessExpressionSyntax ca)
    {
        var member = ca.WhenNotNull is MemberBindingExpressionSyntax mb
            ? mb.Name.Identifier.Text : ca.WhenNotNull.ToString();
        return new LuaNullSafe(Lower(ca.Expression), member);
    }

    private ILuaExpression LowerCoalesce(BinaryExpressionSyntax bin) =>
        new LuaCoalesce(Lower(bin.Left), Lower(bin.Right));

    private ILuaExpression LowerIndex(ElementAccessExpressionSyntax el)
    {
        var key = Lower(el.ArgumentList.Arguments.First().Expression);
        return new LuaIndex(Lower(el.Expression), key);
    }

    private ILuaExpression LowerAssignmentExpr(AssignmentExpressionSyntax assign) =>
        Lower(assign.Right); // assignment-as-expression; statement lowerer handles the assign

    private ILuaExpression LowerLambda(ParameterListSyntax paramList, CSharpSyntaxNode body)
    {
        var parms = paramList.Parameters.Select(p => p.Identifier.Text).ToList();
        var stmts = body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LuaReturn(Lower((ExpressionSyntax)body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private ILuaExpression LowerSimpleLambda(SimpleLambdaExpressionSyntax lam)
    {
        var parms = new List<string> { lam.Parameter.Identifier.Text };
        var stmts = lam.Body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LuaReturn(Lower((ExpressionSyntax)lam.Body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
