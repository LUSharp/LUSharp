using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTranspiler.Transform.Passes;

/// <summary>
/// Recursively rewrites IR nodes:
/// 1. Replaces LuaIdent("this") with LuaIdent("self")
/// 2. When instanceMembers is provided, replaces bare LuaIdent(memberName)
///    with LuaMember(self, memberName) for implicit this access
/// </summary>
public static class IrRewriter
{
    public static ILuaExpression RewriteThisToSelf(ILuaExpression expr, HashSet<string>? instanceMembers = null) => expr switch
    {
        LuaIdent { Name: "this" } => LuaIdent.Self,
        // Bare identifier matching an instance member → self.MemberName
        LuaIdent id when instanceMembers != null && instanceMembers.Contains(id.Name)
            => new LuaMember(LuaIdent.Self, id.Name),
        LuaMember m   => m with { Object = RewriteThisToSelf(m.Object, instanceMembers) },
        LuaIndex ix   => ix with { Object = RewriteThisToSelf(ix.Object, instanceMembers), Key = RewriteThisToSelf(ix.Key, instanceMembers) },
        LuaBinary b   => b with { Left = RewriteThisToSelf(b.Left, instanceMembers), Right = RewriteThisToSelf(b.Right, instanceMembers) },
        LuaUnary u    => u with { Operand = RewriteThisToSelf(u.Operand, instanceMembers) },
        LuaConcat c   => c with { Parts = c.Parts.Select(p => RewriteThisToSelf(p, instanceMembers)).ToList() },
        LuaCall call  => call with { Function = RewriteThisToSelf(call.Function, instanceMembers), Args = call.Args.Select(a => RewriteThisToSelf(a, instanceMembers)).ToList() },
        LuaMethodCall mc => mc with { Object = RewriteThisToSelf(mc.Object, instanceMembers), Args = mc.Args.Select(a => RewriteThisToSelf(a, instanceMembers)).ToList() },
        LuaNew n      => n with { Args = n.Args.Select(a => RewriteThisToSelf(a, instanceMembers)).ToList() },
        LuaTernary t  => t with { Condition = RewriteThisToSelf(t.Condition, instanceMembers), Then = RewriteThisToSelf(t.Then, instanceMembers), Else = RewriteThisToSelf(t.Else, instanceMembers) },
        LuaNullSafe ns => ns with { Object = RewriteThisToSelf(ns.Object, instanceMembers) },
        LuaCoalesce co => co with { Left = RewriteThisToSelf(co.Left, instanceMembers), Right = RewriteThisToSelf(co.Right, instanceMembers) },
        LuaTable tbl  => tbl with { Entries = tbl.Entries.Select(e => new LuaTableEntry(e.Key, RewriteThisToSelf(e.Value, instanceMembers))).ToList() },
        LuaLambda lam => RewriteLambda(lam, instanceMembers),
        LuaSpread sp  => sp with { Table = RewriteThisToSelf(sp.Table, instanceMembers) },
        _ => expr
    };

    private static LuaLambda RewriteLambda(LuaLambda lam, HashSet<string>? instanceMembers) => new()
    {
        Parameters = lam.Parameters,
        Body = RewriteBlock(lam.Body, instanceMembers)
    };

    public static ILuaStatement RewriteThisToSelf(ILuaStatement stmt, HashSet<string>? instanceMembers = null) => stmt switch
    {
        LuaLocal loc      => loc with { Value = loc.Value != null ? RewriteThisToSelf(loc.Value, instanceMembers) : null },
        LuaAssign a       => a with { Target = RewriteThisToSelf(a.Target, instanceMembers), Value = RewriteThisToSelf(a.Value, instanceMembers) },
        LuaReturn ret     => ret with { Value = ret.Value != null ? RewriteThisToSelf(ret.Value, instanceMembers) : null },
        LuaExprStatement e => e with { Expression = RewriteThisToSelf(e.Expression, instanceMembers) },
        LuaConnect c      => c with { Event = RewriteThisToSelf(c.Event, instanceMembers), Handler = RewriteThisToSelf(c.Handler, instanceMembers) },
        LuaError err      => err with { Message = RewriteThisToSelf(err.Message, instanceMembers) },
        LuaIf ifS         => RewriteIf(ifS, instanceMembers),
        LuaWhile wh       => RewriteWhile(wh, instanceMembers),
        LuaRepeat rep     => RewriteRepeat(rep, instanceMembers),
        LuaForNum fn      => RewriteForNum(fn, instanceMembers),
        LuaForIn fi       => RewriteForIn(fi, instanceMembers),
        LuaPCall pc       => RewritePCall(pc, instanceMembers),
        LuaTaskSpawn ts   => RewriteTaskSpawn(ts, instanceMembers),
        LuaMultiAssign ma => ma with { Values = ma.Values.Select(v => RewriteThisToSelf(v, instanceMembers)).ToList() },
        _ => stmt
    };

    public static List<ILuaStatement> RewriteBlock(List<ILuaStatement> block, HashSet<string>? instanceMembers = null) =>
        block.Select(s => RewriteThisToSelf(s, instanceMembers)).ToList();

    private static LuaIf RewriteIf(LuaIf ifS, HashSet<string>? im) => new()
    {
        Condition = RewriteThisToSelf(ifS.Condition, im),
        Then = RewriteBlock(ifS.Then, im),
        ElseIfs = ifS.ElseIfs.Select(ei => new LuaElseIf(RewriteThisToSelf(ei.Condition, im), RewriteBlock(ei.Body, im))).ToList(),
        Else = ifS.Else != null ? RewriteBlock(ifS.Else, im) : null
    };

    private static LuaWhile RewriteWhile(LuaWhile wh, HashSet<string>? im) => new()
    {
        Condition = RewriteThisToSelf(wh.Condition, im),
        Body = RewriteBlock(wh.Body, im)
    };

    private static LuaRepeat RewriteRepeat(LuaRepeat rep, HashSet<string>? im) => new()
    {
        Body = RewriteBlock(rep.Body, im),
        Condition = RewriteThisToSelf(rep.Condition, im)
    };

    private static LuaForNum RewriteForNum(LuaForNum fn, HashSet<string>? im) => new()
    {
        Variable = fn.Variable,
        Start = RewriteThisToSelf(fn.Start, im),
        Limit = RewriteThisToSelf(fn.Limit, im),
        Step = fn.Step != null ? RewriteThisToSelf(fn.Step, im) : null,
        Body = RewriteBlock(fn.Body, im)
    };

    private static LuaForIn RewriteForIn(LuaForIn fi, HashSet<string>? im) => new()
    {
        Variables = fi.Variables,
        Iterator = RewriteThisToSelf(fi.Iterator, im),
        Body = RewriteBlock(fi.Body, im)
    };

    private static LuaPCall RewritePCall(LuaPCall pc, HashSet<string>? im) => new()
    {
        TryBody = RewriteBlock(pc.TryBody, im),
        ErrorVar = pc.ErrorVar,
        CatchBody = RewriteBlock(pc.CatchBody, im),
        FinallyBody = RewriteBlock(pc.FinallyBody, im)
    };

    private static LuaTaskSpawn RewriteTaskSpawn(LuaTaskSpawn ts, HashSet<string>? im) => new()
    {
        Body = RewriteBlock(ts.Body, im)
    };
}
