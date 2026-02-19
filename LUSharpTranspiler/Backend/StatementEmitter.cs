using LUSharpTranspiler.AST.SourceConstructor.Builders;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTranspiler.Backend;

public static class StatementEmitter
{
    public static void Emit(ILuaStatement stmt, LuaWriter w)
    {
        switch (stmt)
        {
            case LuaLocal loc:
                w.WriteInline($"local {loc.Name}");
                if (loc.Value != null) { w.WriteInline(" = "); ExprEmitter.Emit(loc.Value, w); }
                w.WriteLine();
                break;

            case LuaAssign asgn:
                ExprEmitter.Emit(asgn.Target, w);
                w.WriteInline(" = ");
                ExprEmitter.Emit(asgn.Value, w);
                w.WriteLine();
                break;

            case LuaReturn ret:
                w.WriteInline("return");
                if (ret.Value != null) { w.WriteInline(" "); ExprEmitter.Emit(ret.Value, w); }
                w.WriteLine();
                break;

            case LuaBreak:    w.WriteLine("break");    break;
            case LuaContinue: w.WriteLine("continue"); break;

            case LuaError err:
                w.WriteInline("error("); ExprEmitter.Emit(err.Message, w); w.WriteLine(")");
                break;

            case LuaExprStatement es:
                ExprEmitter.Emit(es.Expression, w);
                w.WriteLine();
                break;

            case LuaIf ifS:     EmitIf(ifS, w); break;
            case LuaWhile wh:   EmitWhile(wh, w); break;
            case LuaRepeat rep: EmitRepeat(rep, w); break;
            case LuaForNum fn:  EmitForNum(fn, w); break;
            case LuaForIn fi:   EmitForIn(fi, w); break;
            case LuaPCall pc:   EmitPCall(pc, w); break;
            case LuaTaskSpawn ts: EmitTaskSpawn(ts, w); break;
            case LuaConnect cn: EmitConnect(cn, w); break;

            case LuaMultiAssign ma:
                w.WriteInline(string.Join(", ", ma.Targets));
                w.WriteInline(" = ");
                for (int i = 0; i < ma.Values.Count; i++)
                {
                    if (i > 0) w.WriteInline(", ");
                    ExprEmitter.Emit(ma.Values[i], w);
                }
                w.WriteLine();
                break;

            default:
                w.WriteLine($"--[[unknown stmt: {stmt.GetType().Name}]]");
                break;
        }
    }

    private static void EmitBlock(List<ILuaStatement> body, LuaWriter w)
    {
        w.IndentMore();
        foreach (var s in body) Emit(s, w);
        w.IndentLess();
    }

    private static void EmitIf(LuaIf ifS, LuaWriter w)
    {
        w.WriteInline("if "); ExprEmitter.Emit(ifS.Condition, w); w.WriteLine(" then");
        EmitBlock(ifS.Then, w);
        foreach (var ei in ifS.ElseIfs)
        {
            w.WriteInline("elseif "); ExprEmitter.Emit(ei.Condition, w); w.WriteLine(" then");
            EmitBlock(ei.Body, w);
        }
        if (ifS.Else?.Count > 0) { w.WriteLine("else"); EmitBlock(ifS.Else, w); }
        w.WriteLine("end");
    }

    private static void EmitWhile(LuaWhile wh, LuaWriter w)
    {
        w.WriteInline("while "); ExprEmitter.Emit(wh.Condition, w); w.WriteLine(" do");
        EmitBlock(wh.Body, w);
        w.WriteLine("end");
    }

    private static void EmitRepeat(LuaRepeat rep, LuaWriter w)
    {
        w.WriteLine("repeat");
        EmitBlock(rep.Body, w);
        w.WriteInline("until "); ExprEmitter.Emit(rep.Condition, w); w.WriteLine();
    }

    private static void EmitForNum(LuaForNum fn, LuaWriter w)
    {
        w.WriteInline($"for {fn.Variable} = ");
        ExprEmitter.Emit(fn.Start, w); w.WriteInline(", ");
        ExprEmitter.Emit(fn.Limit, w);
        if (fn.Step != null) { w.WriteInline(", "); ExprEmitter.Emit(fn.Step, w); }
        w.WriteLine(" do");
        EmitBlock(fn.Body, w);
        w.WriteLine("end");
    }

    private static void EmitForIn(LuaForIn fi, LuaWriter w)
    {
        w.WriteInline($"for {string.Join(", ", fi.Variables)} in ");
        ExprEmitter.Emit(fi.Iterator, w); w.WriteLine(" do");
        EmitBlock(fi.Body, w);
        w.WriteLine("end");
    }

    private static void EmitPCall(LuaPCall pc, LuaWriter w)
    {
        w.WriteLine("local _ok, _err = pcall(function()");
        EmitBlock(pc.TryBody, w);
        w.WriteLine("end)");
        if (pc.CatchBody.Count > 0)
        {
            w.WriteLine("if not _ok then");
            EmitBlock(pc.CatchBody, w);
            w.WriteLine("end");
        }
        foreach (var s in pc.FinallyBody) Emit(s, w); // finally always runs
    }

    private static void EmitTaskSpawn(LuaTaskSpawn ts, LuaWriter w)
    {
        w.WriteLine("task.spawn(function()");
        EmitBlock(ts.Body, w);
        w.WriteLine("end)");
    }

    private static void EmitConnect(LuaConnect cn, LuaWriter w)
    {
        ExprEmitter.Emit(cn.Event, w);
        w.WriteInline(":Connect(");
        ExprEmitter.Emit(cn.Handler, w);
        w.WriteLine(")");
    }
}
