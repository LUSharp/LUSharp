namespace LUSharpTranspiler.Transform.IR.Expressions;

// Collapsed chain: a .. b .. c (avoids nesting)
public record LuaConcat(List<ILuaExpression> Parts) : ILuaExpression;
