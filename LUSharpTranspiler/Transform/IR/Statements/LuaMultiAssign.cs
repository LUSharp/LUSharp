namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: a, b = expr1, expr2
public record LuaMultiAssign(List<string> Targets, List<ILuaExpression> Values) : ILuaStatement;
