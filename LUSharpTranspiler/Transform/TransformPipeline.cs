using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;

namespace LUSharpTranspiler.Transform;

public class TransformPipeline
{
    private readonly SymbolTable _symbols = new();

    public List<LuaModule> Run(List<ParsedFile> files)
    {
        // Pass 0: collect all symbols first
        var collector = new SymbolCollector(_symbols);
        foreach (var f in files)
            collector.Collect(f.FilePath, f.Tree);

        // Remaining passes wired in as they're implemented
        var modules = new List<LuaModule>();
        foreach (var f in files)
            modules.Add(new LuaModule
            {
                SourceFile = f.FilePath,
                OutputPath = DeriveOutputPath(f.FilePath),
                ScriptType = _symbols.LookupClass("Main")?.ScriptType ?? ScriptType.ModuleScript
            });

        return modules;
    }

    private static string DeriveOutputPath(string filePath)
    {
        // Client/Foo.cs → out/client/Foo.lua
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (filePath.Contains("Client")) return $"out/client/{name}.lua";
        if (filePath.Contains("Server")) return $"out/server/{name}.lua";
        return $"out/shared/{name}.lua";
    }
}
