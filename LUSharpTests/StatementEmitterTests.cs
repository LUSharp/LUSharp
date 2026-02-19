using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTests;

public class StatementEmitterTests
{
    private static string Emit(ILuaStatement stmt)
    {
        var w = new LuaWriter();
        StatementEmitter.Emit(stmt, w);
        return w.ToString().Trim();
    }

    [Fact]
    public void LocalStatement()
    {
        var result = Emit(new LuaLocal("x", new LuaLiteral("42")));
        Assert.Equal("local x = 42", result);
    }

    [Fact]
    public void AssignStatement()
    {
        var result = Emit(new LuaAssign(new LuaIdent("x"), new LuaLiteral("10")));
        Assert.Equal("x = 10", result);
    }

    [Fact]
    public void ReturnStatement()
    {
        var result = Emit(new LuaReturn(new LuaLiteral("true")));
        Assert.Equal("return true", result);
    }

    [Fact]
    public void IfStatement()
    {
        var stmt = new LuaIf
        {
            Condition = new LuaBinary(new LuaIdent("x"), ">", new LuaLiteral("0")),
            Then = new() { new LuaReturn(new LuaLiteral("1")) }
        };
        var result = Emit(stmt);
        Assert.Contains("if x > 0 then", result);
        Assert.Contains("return 1", result);
        Assert.Contains("end", result);
    }

    [Fact]
    public void ForNumStatement()
    {
        var stmt = new LuaForNum
        {
            Variable = "i",
            Start = new LuaLiteral("1"),
            Limit = new LuaLiteral("10"),
            Body = new() { new LuaBreak() }
        };
        var result = Emit(stmt);
        Assert.Contains("for i = 1, 10 do", result);
        Assert.Contains("break", result);
    }

    [Fact]
    public void PCallStatement()
    {
        var stmt = new LuaPCall
        {
            TryBody = new() { new LuaExprStatement(new LuaCall(new LuaIdent("riskyOp"), new())) },
            CatchBody = new() { new LuaExprStatement(new LuaCall(new LuaIdent("print"), new() { new LuaIdent("_err") })) }
        };
        var result = Emit(stmt);
        Assert.Contains("pcall(function()", result);
        Assert.Contains("if not _ok then", result);
    }
}
