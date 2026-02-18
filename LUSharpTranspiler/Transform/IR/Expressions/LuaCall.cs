namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaCall(ILuaExpression Function, List<ILuaExpression> Args) : ILuaExpression;
