namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaMethodCall(ILuaExpression Object, string Method, List<ILuaExpression> Args) : ILuaExpression;
