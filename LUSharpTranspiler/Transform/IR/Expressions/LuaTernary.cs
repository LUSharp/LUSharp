namespace LUSharpTranspiler.Transform.IR.Expressions;

// cond and a or b — from C# a ? b : c
public record LuaTernary(ILuaExpression Condition, ILuaExpression Then, ILuaExpression Else) : ILuaExpression;
