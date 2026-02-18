namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaBinary(ILuaExpression Left, string Op, ILuaExpression Right) : ILuaExpression;
