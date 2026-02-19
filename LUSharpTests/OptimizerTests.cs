using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTests;

public class OptimizerTests
{
    [Fact]
    public void FoldsConstantAddition()
    {
        var expr = new LuaBinary(new LuaLiteral("1"), "+", new LuaLiteral("2"));
        var result = Optimizer.FoldExpr(expr);
        Assert.Equal("3", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void FoldsConstantStringConcat()
    {
        var expr = new LuaConcat(new() { new LuaLiteral("\"hello\""), new LuaLiteral("\" world\"") });
        var result = Optimizer.FoldExpr(expr);
        Assert.Equal("\"hello world\"", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void EliminatesDeadIfTrue()
    {
        var stmt = new LuaIf
        {
            Condition = LuaLiteral.True,
            Then = new() { new LuaReturn(new LuaLiteral("1")) }
        };
        var result = Optimizer.OptimizeStatement(stmt);
        // Should return just the then-body directly
        Assert.IsType<LuaReturn>(Assert.Single(result));
    }

    [Fact]
    public void EliminatesDeadIfFalse()
    {
        var stmt = new LuaIf
        {
            Condition = LuaLiteral.False,
            Then = new() { new LuaReturn(new LuaLiteral("1")) },
            Else = new() { new LuaReturn(new LuaLiteral("2")) }
        };
        var result = Optimizer.OptimizeStatement(stmt);
        Assert.IsType<LuaReturn>(Assert.Single(result));
        Assert.Equal("2", Assert.IsType<LuaLiteral>(((LuaReturn)result[0]).Value!).Value);
    }
}
