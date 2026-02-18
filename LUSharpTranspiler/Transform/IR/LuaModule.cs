namespace LUSharpTranspiler.Transform.IR;

public enum ScriptType { LocalScript, ModuleScript, Script }

public class LuaModule
{
    public string SourceFile { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public ScriptType ScriptType { get; init; }
    public List<LuaRequire> Requires { get; init; } = new();
    public List<LuaClassDef> Classes { get; init; } = new();
    public List<ILuaStatement> EntryBody { get; init; } = new(); // Main class only
}
