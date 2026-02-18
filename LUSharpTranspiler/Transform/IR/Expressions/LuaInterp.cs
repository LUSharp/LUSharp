namespace LUSharpTranspiler.Transform.IR.Expressions;

// From C# $"..." → Luau `...` template string
public record LuaInterp(string Template) : ILuaExpression;
