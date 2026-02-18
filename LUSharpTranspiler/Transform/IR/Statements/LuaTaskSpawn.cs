namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: task.spawn(function() ... end)
public class LuaTaskSpawn : ILuaStatement
{
    public List<ILuaStatement> Body { get; init; } = new();
}
