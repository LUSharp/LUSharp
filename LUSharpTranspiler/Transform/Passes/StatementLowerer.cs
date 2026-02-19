using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public static class StatementLowerer
{
    public static List<ILuaStatement> LowerBlock(
        SyntaxList<StatementSyntax> stmts,
        ExpressionLowerer exprs)
    {
        var result = new List<ILuaStatement>();
        foreach (var s in stmts)
        {
            var lowered = Lower(s, exprs);
            if (lowered != null) result.Add(lowered);
        }
        return result;
    }

    private static ILuaStatement? Lower(StatementSyntax stmt, ExpressionLowerer exprs) => stmt switch
    {
        LocalDeclarationStatementSyntax loc   => LowerLocalDecl(loc, exprs),
        ExpressionStatementSyntax expr        => LowerExprStmt(expr, exprs),
        ReturnStatementSyntax ret             => new LuaReturn(ret.Expression != null ? exprs.Lower(ret.Expression) : null),
        IfStatementSyntax ifS                 => LowerIf(ifS, exprs),
        ForStatementSyntax forS               => LowerFor(forS, exprs),
        ForEachStatementSyntax forEach        => LowerForEach(forEach, exprs),
        WhileStatementSyntax wh               => LowerWhile(wh, exprs),
        DoStatementSyntax doS                 => LowerDo(doS, exprs),
        BreakStatementSyntax                  => new LuaBreak(),
        ContinueStatementSyntax               => new LuaContinue(),
        ThrowStatementSyntax th               => LowerThrow(th, exprs),
        TryStatementSyntax tryS               => LowerTry(tryS, exprs),
        SwitchStatementSyntax sw              => LowerSwitch(sw, exprs),
        BlockSyntax block                     => new LuaExprStatement(new LuaLiteral(
            string.Join("\n", LowerBlock(block.Statements, exprs)))), // nested block inline
        _                                     => null
    };

    private static ILuaStatement LowerLocalDecl(LocalDeclarationStatementSyntax loc, ExpressionLowerer exprs)
    {
        var v = loc.Declaration.Variables.First();
        var value = v.Initializer != null ? exprs.Lower(v.Initializer.Value) : null;
        return new LuaLocal(v.Identifier.Text, value);
    }

    private static ILuaStatement LowerExprStmt(ExpressionStatementSyntax exprStmt, ExpressionLowerer exprs)
    {
        var expr = exprStmt.Expression;

        // Event += lambda → LuaConnect (must check BEFORE compound assignment)
        if (expr is AssignmentExpressionSyntax ev &&
            ev.IsKind(SyntaxKind.AddAssignmentExpression) &&
            (ev.Right is LambdaExpressionSyntax || ev.Right is AnonymousMethodExpressionSyntax))
        {
            return new LuaConnect(exprs.Lower(ev.Left), exprs.Lower(ev.Right));
        }

        // x += 1 etc — compound assignments
        if (expr is AssignmentExpressionSyntax assign)
        {
            var target = exprs.Lower(assign.Left);
            var value = exprs.Lower(assign.Right);

            if (assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
                return new LuaAssign(target, value);

            // compound: x += v → x = x + v
            var op = assign.Kind() switch
            {
                SyntaxKind.AddAssignmentExpression      => "+",
                SyntaxKind.SubtractAssignmentExpression => "-",
                SyntaxKind.MultiplyAssignmentExpression => "*",
                SyntaxKind.DivideAssignmentExpression   => "/",
                _                                       => "+"
            };
            return new LuaAssign(target, new LuaBinary(target, op, value));
        }

        // i++ / i-- → i = i + 1
        if (expr is PostfixUnaryExpressionSyntax post)
        {
            var operand = exprs.Lower(post.Operand);
            var delta = new LuaLiteral("1");
            var op = post.IsKind(SyntaxKind.PostIncrementExpression) ? "+" : "-";
            return new LuaAssign(operand, new LuaBinary(operand, op, delta));
        }

        return new LuaExprStatement(exprs.Lower(expr));
    }

    private static LuaIf LowerIf(IfStatementSyntax ifS, ExpressionLowerer exprs)
    {
        var luaIf = new LuaIf
        {
            Condition = exprs.Lower(ifS.Condition),
            Then = LowerBody(ifS.Statement, exprs)
        };

        // Collect elseif chain
        var current = ifS.Else;
        while (current?.Statement is IfStatementSyntax elseIf)
        {
            luaIf.ElseIfs.Add(new LuaElseIf(
                exprs.Lower(elseIf.Condition),
                LowerBody(elseIf.Statement, exprs)));
            current = elseIf.Else;
        }

        if (current != null)
            luaIf.Else = LowerBody(current.Statement, exprs);

        return luaIf;
    }

    private static ILuaStatement LowerFor(ForStatementSyntax forS, ExpressionLowerer exprs)
    {
        // Detect simple numeric for: for (int i = start; i < limit; i++)
        if (forS.Declaration?.Variables.Count == 1 &&
            forS.Incrementors.Count == 1 &&
            forS.Condition != null)
        {
            var varName = forS.Declaration.Variables[0].Identifier.Text;
            var start = exprs.Lower(forS.Declaration.Variables[0].Initializer!.Value);
            var limit = exprs.Lower(forS.Condition is BinaryExpressionSyntax bin ? bin.Right : forS.Condition);
            var body = LowerBody(forS.Statement, exprs);
            return new LuaForNum { Variable = varName, Start = start, Limit = limit, Body = body };
        }

        // Fallback: emit as while
        var initStmts = new List<ILuaStatement>();
        if (forS.Declaration != null)
            initStmts.Add(LowerLocalDecl(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.LocalDeclarationStatement(forS.Declaration),
                exprs));

        var whileBody = LowerBody(forS.Statement, exprs);
        foreach (var inc in forS.Incrementors)
            whileBody.Add(new LuaExprStatement(exprs.Lower(inc)));

        return new LuaWhile
        {
            Condition = forS.Condition != null ? exprs.Lower(forS.Condition) : LuaLiteral.True,
            Body = whileBody
        };
    }

    private static LuaForIn LowerForEach(ForEachStatementSyntax fe, ExpressionLowerer exprs)
    {
        var iter = exprs.Lower(fe.Expression);
        // Use ipairs for array-like, pairs for general
        var iterCall = new LuaCall(new LuaIdent("pairs"), new List<ILuaExpression> { iter });
        return new LuaForIn
        {
            Variables = new() { "_", fe.Identifier.Text },
            Iterator = iterCall,
            Body = LowerBody(fe.Statement, exprs)
        };
    }

    private static LuaWhile LowerWhile(WhileStatementSyntax wh, ExpressionLowerer exprs) =>
        new() { Condition = exprs.Lower(wh.Condition), Body = LowerBody(wh.Statement, exprs) };

    private static LuaRepeat LowerDo(DoStatementSyntax doS, ExpressionLowerer exprs) =>
        new() { Body = LowerBody(doS.Statement, exprs), Condition = exprs.Lower(doS.Condition) };

    private static LuaError LowerThrow(ThrowStatementSyntax th, ExpressionLowerer exprs) =>
        new(th.Expression != null ? exprs.Lower(th.Expression) : new LuaLiteral("\"error\""));

    private static LuaPCall LowerTry(TryStatementSyntax tryS, ExpressionLowerer exprs)
    {
        var pCall = new LuaPCall
        {
            TryBody = LowerBody(tryS.Block, exprs)
        };

        if (tryS.Catches.Count > 0)
        {
            var catch1 = tryS.Catches[0];
            pCall.CatchBody.AddRange(LowerBody(catch1.Block, exprs));
        }

        if (tryS.Finally != null)
            pCall.FinallyBody.AddRange(LowerBody(tryS.Finally.Block, exprs));

        return pCall;
    }

    private static ILuaStatement LowerSwitch(SwitchStatementSyntax sw, ExpressionLowerer exprs)
    {
        // switch → if/elseif chain
        var subject = exprs.Lower(sw.Expression);
        LuaIf? root = null;
        LuaIf? current = null;

        foreach (var section in sw.Sections)
        {
            var labels = section.Labels.OfType<CaseSwitchLabelSyntax>().ToList();
            if (!labels.Any()) continue; // default handled below

            var cond = labels
                .Select(l => (ILuaExpression)new LuaBinary(subject, "==", exprs.Lower(l.Value)))
                .Aggregate((a, b) => new LuaBinary(a, "or", b));

            var body = section.Statements
                .Where(s => s is not BreakStatementSyntax)
                .Select(s => Lower(s, exprs))
                .Where(s => s != null)
                .Select(s => s!)
                .ToList();

            if (root == null)
            {
                root = new LuaIf { Condition = cond, Then = body };
                current = root;
            }
            else
            {
                current!.ElseIfs.Add(new LuaElseIf(cond, body));
            }
        }

        // default section
        var defaultSection = sw.Sections.FirstOrDefault(s =>
            s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
        if (defaultSection != null && root != null)
        {
            var defaultBody = defaultSection.Statements
                .Where(s => s is not BreakStatementSyntax)
                .Select(s => Lower(s, exprs))
                .Where(s => s != null).Select(s => s!).ToList();
            root.Else ??= new();
            root.Else.AddRange(defaultBody);
        }

        return (ILuaStatement?)root ?? new LuaExprStatement(new LuaLiteral("-- empty switch"));
    }

    private static List<ILuaStatement> LowerBody(StatementSyntax stmt, ExpressionLowerer exprs) =>
        stmt is BlockSyntax block
            ? LowerBlock(block.Statements, exprs)
            : Lower(stmt, exprs) is { } s ? new() { s } : new();

    private static List<ILuaStatement> LowerBody(BlockSyntax block, ExpressionLowerer exprs) =>
        LowerBlock(block.Statements, exprs);
}
