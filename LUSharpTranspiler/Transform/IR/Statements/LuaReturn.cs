namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaReturn(ILuaExpression? Value) : ILuaStatement;
