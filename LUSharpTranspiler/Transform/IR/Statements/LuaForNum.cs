namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaForNum : ILuaStatement
{
    public string Variable { get; init; } = "i";
    public ILuaExpression Start { get; init; } = null!;
    public ILuaExpression Limit { get; init; } = null!;
    public ILuaExpression? Step { get; init; }
    public List<ILuaStatement> Body { get; init; } = new();
}
