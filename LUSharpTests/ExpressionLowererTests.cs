using LUSharpTranspiler.Transform.Passes;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTests;

public class ExpressionLowererTests
{
    private ExpressionLowerer MakeLowerer() => new(new TypeResolver(new()));

    private ExpressionSyntax Parse(string expr)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ var x = {expr}; }} }}");
        return tree.GetRoot()
            .DescendantNodes()
            .OfType<EqualsValueClauseSyntax>()
            .First()
            .Value;
    }

    [Fact]
    public void LowerIntLiteral()
    {
        var result = MakeLowerer().Lower(Parse("42"));
        var lit = Assert.IsType<LuaLiteral>(result);
        Assert.Equal("42", lit.Value);
    }

    [Fact]
    public void LowerStringLiteral()
    {
        var result = MakeLowerer().Lower(Parse("\"hello\""));
        var lit = Assert.IsType<LuaLiteral>(result);
        Assert.Equal("\"hello\"", lit.Value);
    }

    [Fact]
    public void LowerBoolTrue()
    {
        var result = MakeLowerer().Lower(Parse("true"));
        Assert.Equal("true", Assert.IsType<LuaLiteral>(result).Value);
    }

    [Fact]
    public void LowerIdentifier()
    {
        var result = MakeLowerer().Lower(Parse("myVar"));
        Assert.Equal("myVar", Assert.IsType<LuaIdent>(result).Name);
    }

    [Fact]
    public void LowerAddExpression_StaysBinary()
    {
        var result = MakeLowerer().Lower(Parse("\"hello\" + name"));
        var bin = Assert.IsType<LuaBinary>(result);
        Assert.Equal("+", bin.Op);
    }

    [Fact]
    public void LowerInterpolatedString()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { var x = $\"Hello {name}\"; } }");
        var interp = tree.GetRoot().DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>().First();
        var result = MakeLowerer().Lower(interp);
        Assert.IsType<LuaInterp>(result);
    }
}
