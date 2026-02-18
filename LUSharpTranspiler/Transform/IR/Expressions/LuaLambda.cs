namespace LUSharpTranspiler.Transform.IR.Expressions;

public class LuaLambda : ILuaExpression
{
    public List<string> Parameters { get; init; } = new();
    public List<ILuaStatement> Body { get; init; } = new();
}
