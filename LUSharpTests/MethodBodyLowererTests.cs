using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpTests;

public class MethodBodyLowererTests
{
    [Fact]
    public void LowersMethodBodyIntoClassDef()
    {
        var code = @"
            class Player {
                public int GetHealth() {
                    return 100;
                }
            }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var table = new SymbolTable();
        var lowerer = new MethodBodyLowerer(table);

        var classDef = lowerer.Lower(
            tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .First());

        Assert.Single(classDef.Methods);
        Assert.Equal("GetHealth", classDef.Methods[0].Name);
        Assert.IsType<LuaReturn>(Assert.Single(classDef.Methods[0].Body));
    }
}
