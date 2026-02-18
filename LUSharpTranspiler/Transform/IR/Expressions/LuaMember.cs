namespace LUSharpTranspiler.Transform.IR.Expressions;

// obj.field  (Dot) or obj:method (Colon — for method calls only)
public record LuaMember(ILuaExpression Object, string Member, bool IsColon = false) : ILuaExpression;
