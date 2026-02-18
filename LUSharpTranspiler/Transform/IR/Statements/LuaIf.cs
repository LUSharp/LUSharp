namespace LUSharpTranspiler.Transform.IR.Statements;

public record LuaElseIf(ILuaExpression Condition, List<ILuaStatement> Body);

public class LuaIf : ILuaStatement
{
    public ILuaExpression Condition { get; init; } = null!;
    public List<ILuaStatement> Then { get; init; } = new();
    public List<LuaElseIf> ElseIfs { get; init; } = new();
    public List<ILuaStatement>? Else { get; set; }
}
