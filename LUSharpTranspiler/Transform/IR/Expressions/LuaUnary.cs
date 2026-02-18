namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaUnary(string Op, ILuaExpression Operand) : ILuaExpression;
