namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaError(ILuaExpression Message) : ILuaStatement;
