namespace LUSharpTranspiler.Transform.IR.Expressions;

public record LuaTableEntry(string? Key, ILuaExpression Value); // Key null = array entry

public record LuaTable(List<LuaTableEntry> Entries) : ILuaExpression
{
    public static LuaTable Empty => new(new List<LuaTableEntry>());
}
