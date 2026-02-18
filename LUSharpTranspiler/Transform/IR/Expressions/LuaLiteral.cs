namespace LUSharpTranspiler.Transform.IR.Expressions;

// Value is already the Lua literal string: "42", "\"hello\"", "true", "nil"
public record LuaLiteral(string Value) : ILuaExpression
{
    public static LuaLiteral Nil => new("nil");
    public static LuaLiteral True => new("true");
    public static LuaLiteral False => new("false");
    public static LuaLiteral FromString(string s) => new($"\"{s.Replace("\"", "\\\"")}\"");
    public static LuaLiteral FromNumber(object n) => new(n.ToString()!);
}
