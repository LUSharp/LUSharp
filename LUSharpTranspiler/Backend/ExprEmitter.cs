using LUSharpTranspiler.AST.SourceConstructor.Builders;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTranspiler.Backend;

public static class ExprEmitter
{
    public static void Emit(ILuaExpression expr, LuaWriter w)
    {
        switch (expr)
        {
            case LuaLiteral lit:     w.WriteInline(lit.Value); break;
            case LuaIdent id:        w.WriteInline(id.Name); break;
            case LuaInterp interp:   w.WriteInline(interp.Template); break;

            case LuaMember mem:
                Emit(mem.Object, w);
                w.WriteInline(mem.IsColon ? ":" : ".");
                w.WriteInline(mem.Member);
                break;

            case LuaIndex idx:
                Emit(idx.Object, w);
                w.WriteInline("[");
                Emit(idx.Key, w);
                w.WriteInline("]");
                break;

            case LuaBinary bin:
                EmitParen(bin.Left, bin.Op, w, true);
                w.WriteInline($" {bin.Op} ");
                EmitParen(bin.Right, bin.Op, w, false);
                break;

            case LuaUnary un:
                w.WriteInline(un.Op == "not" ? "not " : un.Op);
                Emit(un.Operand, w);
                break;

            case LuaConcat cat:
                for (int i = 0; i < cat.Parts.Count; i++)
                {
                    if (i > 0) w.WriteInline(" .. ");
                    Emit(cat.Parts[i], w);
                }
                break;

            case LuaCall call:
                Emit(call.Function, w);
                w.WriteInline("(");
                EmitArgList(call.Args, w);
                w.WriteInline(")");
                break;

            case LuaMethodCall mc:
                Emit(mc.Object, w);
                w.WriteInline($":{mc.Method}(");
                EmitArgList(mc.Args, w);
                w.WriteInline(")");
                break;

            case LuaNew n:
                w.WriteInline($"{n.ClassName}.new(");
                EmitArgList(n.Args, w);
                w.WriteInline(")");
                break;

            case LuaTernary t:
                Emit(t.Condition, w); w.WriteInline(" and ");
                Emit(t.Then, w);     w.WriteInline(" or ");
                Emit(t.Else, w);
                break;

            case LuaNullSafe ns:
                Emit(ns.Object, w);
                w.WriteInline(" and ");
                Emit(ns.Object, w);
                w.WriteInline($".{ns.Member}");
                break;

            case LuaCoalesce co:
                Emit(co.Left, w); w.WriteInline(" ~= nil and ");
                Emit(co.Left, w); w.WriteInline(" or ");
                Emit(co.Right, w);
                break;

            case LuaTable tbl:
                EmitTable(tbl, w);
                break;

            case LuaLambda lam:
                EmitLambda(lam, w);
                break;

            case LuaParen par:
                w.WriteInline("(");
                Emit(par.Inner, w);
                w.WriteInline(")");
                break;

            case LuaSpread sp:
                w.WriteInline("table.unpack(");
                Emit(sp.Table, w);
                w.WriteInline(")");
                break;

            default:
                w.WriteInline($"--[[unknown expr: {expr.GetType().Name}]]");
                break;
        }
    }

    private static void EmitParen(ILuaExpression expr, string parentOp, LuaWriter w, bool isLeft)
    {
        if (expr is LuaBinary bin && NeedsParens(bin.Op, parentOp))
        {
            w.WriteInline("("); Emit(expr, w); w.WriteInline(")");
        }
        else Emit(expr, w);
    }

    private static bool NeedsParens(string inner, string outer) =>
        (inner == "or" && outer != "or") ||
        (inner == "and" && outer is "+" or "-" or "*" or "/");

    private static void EmitArgList(List<ILuaExpression> args, LuaWriter w)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0) w.WriteInline(", ");
            Emit(args[i], w);
        }
    }

    private static void EmitTable(LuaTable tbl, LuaWriter w)
    {
        if (tbl.Entries.Count == 0) { w.WriteInline("{}"); return; }
        w.WriteInline("{");
        for (int i = 0; i < tbl.Entries.Count; i++)
        {
            if (i > 0) w.WriteInline(", ");
            var e = tbl.Entries[i];
            if (e.Key != null) w.WriteInline($"{e.Key} = ");
            Emit(e.Value, w);
        }
        w.WriteInline("}");
    }

    private static void EmitLambda(LuaLambda lam, LuaWriter w)
    {
        w.WriteInline($"function({string.Join(", ", lam.Parameters)})");
        if (lam.Body.Count == 1 && lam.Body[0] is LUSharpTranspiler.Transform.IR.Statements.LuaReturn ret && ret.Value != null)
        {
            w.WriteInline(" return "); Emit(ret.Value, w); w.WriteInline(" end");
        }
        else
        {
            w.WriteInline("\n");
            w.IndentMore();
            foreach (var s in lam.Body)
                StatementEmitter.Emit(s, w);
            w.IndentLess();
            w.WriteInline("end");
        }
    }
}
