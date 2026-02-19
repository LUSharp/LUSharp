using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTranspiler.Transform.Passes;

public static class Optimizer
{
    public static ILuaExpression FoldExpr(ILuaExpression expr) => expr switch
    {
        LuaBinary { Op: "+" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l + r).ToString()),
        LuaBinary { Op: "-" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l - r).ToString()),
        LuaBinary { Op: "*" } bin when IsNumericLiteral(bin.Left, out var l) && IsNumericLiteral(bin.Right, out var r)
            => new LuaLiteral((l * r).ToString()),
        LuaConcat cat when cat.Parts.All(IsStringLiteral)
            => new LuaLiteral("\"" + string.Concat(cat.Parts.Cast<LuaLiteral>()
                .Select(l => l.Value.Trim('"'))) + "\""),
        _ => expr
    };

    public static List<ILuaStatement> OptimizeStatement(ILuaStatement stmt) => stmt switch
    {
        LuaIf { Condition: LuaLiteral { Value: "true"  } } ifS => ifS.Then,
        LuaIf { Condition: LuaLiteral { Value: "false" } } ifS => ifS.Else ?? new(),
        _ => new() { stmt }
    };

    public static List<ILuaStatement> OptimizeBlock(List<ILuaStatement> stmts) =>
        stmts.SelectMany(OptimizeStatement).ToList();

    private static bool IsNumericLiteral(ILuaExpression e, out double v)
    {
        if (e is LuaLiteral lit && double.TryParse(lit.Value, out v)) return true;
        v = 0; return false;
    }

    private static bool IsStringLiteral(ILuaExpression e) =>
        e is LuaLiteral lit && lit.Value.StartsWith('"') && lit.Value.EndsWith('"');
}
