namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaLocal(string Name, ILuaExpression? Value) : ILuaStatement;
