namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class LuaMappingAttribute(string luaExpr) : Attribute
{
    public string LuaExpr { get; } = luaExpr;
}
