namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: local _ok, _err = pcall(function() ... end)
// then optional catch block, then finally block inline
public class LuaPCall : ILuaStatement
{
    public List<ILuaStatement> TryBody { get; init; } = new();
    public string? ErrorVar { get; init; }
    public List<ILuaStatement> CatchBody { get; init; } = new();
    public List<ILuaStatement> FinallyBody { get; init; } = new();
}
