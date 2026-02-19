using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTests;

public class ExprEmitterTests
{
    private static string Emit(ILuaExpression expr)
    {
        var w = new LuaWriter();
        ExprEmitter.Emit(expr, w);
        return w.ToString().Trim();
    }

    [Fact] public void Literal()    => Assert.Equal("42",       Emit(new LuaLiteral("42")));
    [Fact] public void Ident()      => Assert.Equal("self",     Emit(new LuaIdent("self")));
    [Fact] public void Member()     => Assert.Equal("self.Health", Emit(new LuaMember(new LuaIdent("self"), "Health")));
    [Fact] public void Binary()     => Assert.Equal("a + b",    Emit(new LuaBinary(new LuaIdent("a"), "+", new LuaIdent("b"))));
    [Fact] public void Concat()     => Assert.Equal("a .. b",   Emit(new LuaConcat(new() { new LuaIdent("a"), new LuaIdent("b") })));
    [Fact] public void NilLiteral() => Assert.Equal("nil",      Emit(LuaLiteral.Nil));
    [Fact] public void New()        => Assert.Equal("Player.new(x)", Emit(new LuaNew("Player", new() { new LuaIdent("x") })));
    [Fact] public void MethodCall() => Assert.Equal("obj:Foo(x)", Emit(new LuaMethodCall(new LuaIdent("obj"), "Foo", new() { new LuaIdent("x") })));
    [Fact] public void Ternary()    => Assert.Equal("a and b or c", Emit(new LuaTernary(new LuaIdent("a"), new LuaIdent("b"), new LuaIdent("c"))));
    [Fact] public void NullSafe()   => Assert.Equal("x and x.y", Emit(new LuaNullSafe(new LuaIdent("x"), "y")));
    [Fact] public void Index()      => Assert.Equal("t[i]",     Emit(new LuaIndex(new LuaIdent("t"), new LuaIdent("i"))));
}
