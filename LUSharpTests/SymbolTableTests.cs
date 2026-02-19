using LUSharpTranspiler.Transform;
using LUSharpTranspiler.Transform.IR;

namespace LUSharpTests;

public class SymbolTableTests
{
    [Fact]
    public void RegisterClass_CanLookupByName()
    {
        var table = new SymbolTable();
        var symbol = new ClassSymbol("Player", "Client/Player.cs", ScriptType.ModuleScript, "ModuleScript");
        table.Register(symbol);

        var found = table.LookupClass("Player");
        Assert.NotNull(found);
        Assert.Equal("Player", found!.Name);
    }

    [Fact]
    public void LookupClass_ReturnsNull_WhenNotFound()
    {
        var table = new SymbolTable();
        Assert.Null(table.LookupClass("NonExistent"));
    }
}
