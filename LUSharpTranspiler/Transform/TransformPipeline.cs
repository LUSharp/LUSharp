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

            // Pass 2: optimize method bodies
            foreach (var cls in classes)
            {
                foreach (var method in cls.Methods)
                    method.Body = Optimizer.OptimizeBlock(method.Body);
                if (cls.Constructor != null)
                    cls.Constructor.Body = Optimizer.OptimizeBlock(cls.Constructor.Body);
            }

            // Pass 3: extract GameEntry() body from Main class as top-level entry code
            var entryBody = new List<ILuaStatement>();
            var mainClass = classes.FirstOrDefault(c => c.Name == "Main");
            if (mainClass != null)
            {
                var gameEntry = mainClass.Methods.FirstOrDefault(m => m.Name == "GameEntry");
                if (gameEntry != null)
                {
                    entryBody = gameEntry.Body;
                    mainClass.Methods.Remove(gameEntry);
                }

                // Main class is just a script container — remove it from class list
                classes.Remove(mainClass);
            }

            // Determine script type from the Main class in this file, or fallback
            var mainSymbol = classes.Count == 0 && mainClass != null
                ? _symbols.LookupClass("Main")
                : null;
            var scriptType = mainSymbol?.ScriptType
                ?? _symbols.LookupClass("Main")?.ScriptType
                ?? ScriptType.ModuleScript;

            modules.Add(new LuaModule
            {
                SourceFile = f.FilePath,
                OutputPath = DeriveOutputPath(f.FilePath),
                ScriptType = scriptType,
                Classes = classes,
                EntryBody = entryBody
            });
        }

        // Pass 4: resolve cross-file requires
        ImportResolver.Resolve(modules, _symbols);

        return modules;
    }

    private static string DeriveOutputPath(string filePath)
    {
        // Client/Foo.cs → client/Foo.lua (no out/ prefix — BuildCommand adds it)
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (filePath.Contains("Client")) return $"client/{name}.lua";
        if (filePath.Contains("Server")) return $"server/{name}.lua";
        return $"shared/{name}.lua";
    }
}
