namespace LUSharpTranspiler.Transform.IR.Expressions;

// ClassName.new(args) — from C# new Foo(...)
public record LuaNew(string ClassName, List<ILuaExpression> Args) : ILuaExpression;
