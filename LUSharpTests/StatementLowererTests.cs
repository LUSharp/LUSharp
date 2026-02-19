using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class StatementLowererTests
{
    private static List<ILuaStatement> LowerMethod(string body)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {body} }} }}");
        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().First();
        var exprLowerer = new ExpressionLowerer(new TypeResolver(new()));
        return StatementLowerer.LowerBlock(method.Body!.Statements, exprLowerer);
    }

    [Fact]
    public void LocalVarDeclaration()
    {
        var stmts = LowerMethod("var x = 42;");
        var local = Assert.IsType<LuaLocal>(Assert.Single(stmts));
        Assert.Equal("x", local.Name);
        Assert.Equal("42", Assert.IsType<LuaLiteral>(local.Value!).Value);
    }

    [Fact]
    public void ReturnStatement()
    {
        var stmts = LowerMethod("return 99;");
        var ret = Assert.IsType<LuaReturn>(Assert.Single(stmts));
        Assert.Equal("99", Assert.IsType<LuaLiteral>(ret.Value!).Value);
    }

    [Fact]
    public void IfElseStatement()
    {
        var stmts = LowerMethod("if (x > 0) { y = 1; } else { y = 2; }");
        var ifStmt = Assert.IsType<LuaIf>(Assert.Single(stmts));
        Assert.NotNull(ifStmt.Condition);
        Assert.Single(ifStmt.Then);
        Assert.NotNull(ifStmt.Else);
    }

    [Fact]
    public void ForEachStatement()
    {
        var stmts = LowerMethod("foreach (var item in list) { print(item); }");
        var forIn = Assert.IsType<LuaForIn>(Assert.Single(stmts));
        Assert.Contains("item", forIn.Variables);
    }

    [Fact]
    public void WhileStatement()
    {
        var stmts = LowerMethod("while (running) { update(); }");
        var wh = Assert.IsType<LuaWhile>(Assert.Single(stmts));
        Assert.NotNull(wh.Condition);
        Assert.Single(wh.Body);
    }
}
