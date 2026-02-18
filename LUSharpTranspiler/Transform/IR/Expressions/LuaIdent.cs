namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaIdent(string Name) : ILuaExpression
{
    public static LuaIdent Self => new("self");
}
