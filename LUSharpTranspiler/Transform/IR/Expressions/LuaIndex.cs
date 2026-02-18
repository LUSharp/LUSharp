namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaIndex(ILuaExpression Object, ILuaExpression Key) : ILuaExpression;
