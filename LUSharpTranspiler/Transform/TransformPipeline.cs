using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        // Pass 1: lower each file into a LuaModule
        var lowerer = new MethodBodyLowerer(_symbols);
        var modules = new List<LuaModule>();
        foreach (var f in files)
        {
            var classes = f.Tree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(c => lowerer.Lower(c))
                .ToList();

            modules.Add(new LuaModule
            {
                SourceFile = f.FilePath,
                OutputPath = DeriveOutputPath(f.FilePath),
                ScriptType = _symbols.LookupClass("Main")?.ScriptType ?? ScriptType.ModuleScript,
                Classes = classes
            });
        }

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
