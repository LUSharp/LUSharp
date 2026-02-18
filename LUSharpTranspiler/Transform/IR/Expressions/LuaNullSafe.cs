namespace LUSharpTranspiler.Transform.IR.Expressions;

// x and x.y — from C# x?.y
public record LuaNullSafe(ILuaExpression Object, string Member) : ILuaExpression;
