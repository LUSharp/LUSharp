namespace LUSharpTranspiler.Transform.IR.Expressions;

// x ~= nil and x or y — from C# x ?? y
public record LuaCoalesce(ILuaExpression Left, ILuaExpression Right) : ILuaExpression;
