namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaParen(ILuaExpression Inner) : ILuaExpression;
