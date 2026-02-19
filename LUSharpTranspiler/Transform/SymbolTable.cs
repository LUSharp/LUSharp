namespace LUSharpTranspiler.Transform;

public class SymbolTable
{
    private readonly Dictionary<string, ClassSymbol> _classes = new();

    public void Register(ClassSymbol symbol) =>
        _classes[symbol.Name] = symbol;

    public ClassSymbol? LookupClass(string name) =>
        _classes.GetValueOrDefault(name);

    public IReadOnlyDictionary<string, ClassSymbol> Classes => _classes;
}
