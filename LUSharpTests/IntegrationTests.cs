using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class IntegrationTests
{
    private static string Transpile(string csharp)
    {
        var tree = CSharpSyntaxTree.ParseText(csharp);
        var table = new SymbolTable();
        new SymbolCollector(table).Collect("Client/Test.cs", tree);

        var lowerer = new MethodBodyLowerer(table);
        var classes = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Select(c => lowerer.Lower(c))
            .ToList();

        var module = new LUSharpTranspiler.Transform.IR.LuaModule
        {
            ScriptType = LUSharpTranspiler.Transform.IR.ScriptType.ModuleScript,
            Classes = classes
        };

        return ModuleEmitter.Emit(module);
    }

    [Fact]
    public void SimpleClass_TranspilesToLua()
    {
        var result = Transpile(@"
            public class Player {
                public string Name { get; set; } = ""Default"";
                public Player(string name) { Name = name; }
                public void Greet() { print(""Hello "" + Name); }
            }");

        Assert.Contains("local Player = {}", result);
        Assert.Contains("function Player.new(name)", result);
        Assert.Contains("function Player:Greet()", result);
        Assert.Contains("return Player", result);
    }

    [Fact]
    public void IfStatement_TranspilesToLua()
    {
        var result = Transpile(@"
            public class Logic {
                public void Check(int x) {
                    if (x > 0) { print(""pos""); }
                    else { print(""neg""); }
                }
            }");

        Assert.Contains("if x > 0 then", result);
        Assert.Contains("else", result);
        Assert.Contains("end", result);
    }

    [Fact]
    public void ForEach_TranspilesToPairsLoop()
    {
        var result = Transpile(@"
            public class Looper {
                public void Run() {
                    foreach (var item in items) { print(item); }
                }
            }");

        Assert.Contains("for _, item in pairs(items) do", result);
    }
}
