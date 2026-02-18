namespace LUSharpTranspiler.Transform.IR;

public record LuaFieldDef(string Name, ILuaExpression? Value, bool IsStatic = false);
