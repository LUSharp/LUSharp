namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaExprStatement(ILuaExpression Expression) : ILuaStatement;
