using LUSharpTranspiler.Transform.Passes;

namespace LUSharpTests;

public class TypeResolverTests
{
    private readonly TypeResolver _resolver = new(new());

    [Theory]
    [InlineData("string",  "string")]
    [InlineData("int",     "number")]
    [InlineData("float",   "number")]
    [InlineData("double",  "number")]
    [InlineData("bool",    "boolean")]
    [InlineData("void",    "nil")]
    [InlineData("object",  "any")]
    public void ResolvePrimitive(string csharp, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(csharp));
    }

    [Fact]
    public void ResolveList_EmitsTableType()
    {
        Assert.Equal("{number}", _resolver.Resolve("List<int>"));
    }

    [Fact]
    public void ResolveDictionary_EmitsTableType()
    {
        Assert.Equal("{[string]: number}", _resolver.Resolve("Dictionary<string, int>"));
    }

    [Fact]
    public void ResolveUnknownType_DefaultsToAny()
    {
        Assert.Equal("any", _resolver.Resolve("SomeRandomType"));
    }

    [Fact]
    public void ResolveUserClass_RegisteredInSymbolTable()
    {
        var table = new LUSharpTranspiler.Transform.SymbolTable();
        table.Register(new("Player", "Client/Player.cs",
            LUSharpTranspiler.Transform.IR.ScriptType.ModuleScript, "ModuleScript"));
        var resolver = new TypeResolver(table);
        Assert.Equal("Player", resolver.Resolve("Player"));
    }
}
