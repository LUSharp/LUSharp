namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaForIn : ILuaStatement
{
    public List<string> Variables { get; init; } = new();
    public ILuaExpression Iterator { get; init; } = null!; // pairs(t) or ipairs(t)
    public List<ILuaStatement> Body { get; init; } = new();
}
