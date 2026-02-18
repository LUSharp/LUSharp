namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaAssign(ILuaExpression Target, ILuaExpression Value) : ILuaStatement;
