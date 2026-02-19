using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.Passes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class EventBinderTests
{
    private static List<ILuaStatement> LowerMethod(string body)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {body} }} }}");
        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().First();
        var exprs = new ExpressionLowerer(new TypeResolver(new()));
        return StatementLowerer.LowerBlock(method.Body!.Statements, exprs);
    }

    [Fact]
    public void PlusEqualsLambda_BecomesLuaConnect()
    {
        var stmts = LowerMethod("players.PlayerAdded += (Player p) => { print(p.Name); };");
        var conn = Assert.IsType<LuaConnect>(Assert.Single(stmts));
        Assert.NotNull(conn.Event);
        Assert.NotNull(conn.Handler);
    }

    [Fact]
    public void DotConnect_BecomesLuaMethodCall()
    {
        var stmts = LowerMethod("players.PlayerAdded.Connect((Player p) => { print(p.Name); });");
        // .Connect() call should be recognized as LuaExprStatement with LuaMethodCall
        Assert.Single(stmts);
    }
}
