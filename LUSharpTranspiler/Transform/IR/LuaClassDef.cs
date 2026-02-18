namespace LUSharpTranspiler.Transform.IR;

public class LuaClassDef
{
    public string Name { get; init; } = "";
    public LuaMethodDef? Constructor { get; set; }
    public List<LuaFieldDef> StaticFields { get; init; } = new();
    public List<LuaFieldDef> InstanceFields { get; init; } = new();
    public List<LuaMethodDef> Methods { get; init; } = new();
    public List<LuaEventDef> Events { get; init; } = new();
}
