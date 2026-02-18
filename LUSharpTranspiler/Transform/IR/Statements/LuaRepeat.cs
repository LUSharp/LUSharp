namespace LUSharpTranspiler.Transform.IR.Statements;

public class LuaRepeat : ILuaStatement
{
    public List<ILuaStatement> Body { get; init; } = new();
    public ILuaExpression Condition { get; init; } = null!;
}
