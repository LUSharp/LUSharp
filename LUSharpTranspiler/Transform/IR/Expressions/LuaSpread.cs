namespace LUSharpTranspiler.Transform.IR.Expressions;

// table.unpack(t)
public record LuaSpread(ILuaExpression Table) : ILuaExpression;
