using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class SymbolCollectorTests
{
    [Fact]
    public void Collect_RegistersModuleScriptClass()
    {
        var code = @"
            public class Player : ModuleScript {
                public string Name { get; set; }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var collector = new SymbolCollector(table);

        collector.Collect("Client/Player.cs", tree);

        var symbol = table.LookupClass("Player");
        Assert.NotNull(symbol);
        Assert.Equal(ScriptType.ModuleScript, symbol!.ScriptType);
    }

    [Fact]
    public void Collect_RegistersLocalScriptFromClientFolder()
    {
        var code = "public class Main : RobloxScript { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var collector = new SymbolCollector(table);

        collector.Collect("Client/Main.cs", tree);

        var symbol = table.LookupClass("Main");
        Assert.Equal(ScriptType.LocalScript, symbol!.ScriptType);
    }
}
