namespace LUSharpTranspiler.Transform.IR;

public class LuaMethodDef
{
    public string Name { get; init; } = "";
    public bool IsStatic { get; init; }
    public List<string> Parameters { get; init; } = new();
    public List<ILuaStatement> Body { get; set; } = new();
}
