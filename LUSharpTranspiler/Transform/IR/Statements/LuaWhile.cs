namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaWhile : ILuaStatement
{
    public ILuaExpression Condition { get; init; } = null!;
    public List<ILuaStatement> Body { get; init; } = new();
}
