using LUSharpTranspiler.Transform.IR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class SymbolCollector
{
    private readonly SymbolTable _table;

    public SymbolCollector(SymbolTable table) => _table = table;

    public void Collect(string filePath, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var name = cls.Identifier.Text;
            var baseClass = cls.BaseList?.Types.FirstOrDefault()?.ToString() ?? "";
            var scriptType = DetermineScriptType(filePath, baseClass);
            _table.Register(new ClassSymbol(name, filePath, scriptType, baseClass));
        }
    }

    private static ScriptType DetermineScriptType(string filePath, string baseClass) =>
        baseClass switch
        {
            "ModuleScript" => ScriptType.ModuleScript,
            "LocalScript"  => ScriptType.LocalScript,
            "Script"       => ScriptType.Script,
            _ when filePath.Contains("Client") => ScriptType.LocalScript,
            _ when filePath.Contains("Server") => ScriptType.Script,
            _                                  => ScriptType.ModuleScript
        };
}
