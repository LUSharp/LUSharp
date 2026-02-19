namespace LUSharpTranspiler.Transform.Passes;

public class TypeResolver
{
    private readonly SymbolTable _symbols;

    private static readonly Dictionary<string, string> Primitives = new()
    {
        ["string"] = "string",
        ["int"]    = "number",
        ["float"]  = "number",
        ["double"] = "number",
        ["long"]   = "number",
        ["short"]  = "number",
        ["uint"]   = "number",
        ["bool"]   = "boolean",
        ["void"]   = "nil",
        ["object"] = "any",
        ["var"]    = "any",
        ["dynamic"]= "any",
    };

    public TypeResolver(SymbolTable symbols) => _symbols = symbols;

    public string Resolve(string csType)
    {
        if (Primitives.TryGetValue(csType, out var lua)) return lua;

        if (csType.StartsWith("List<") && csType.EndsWith(">"))
        {
            var inner = Resolve(csType[5..^1].Trim());
            return $"{{{inner}}}";
        }

        if (csType.StartsWith("Dictionary<") && csType.EndsWith(">"))
        {
            var inner = csType[11..^1];
            var comma = FindTopLevelComma(inner);
            if (comma >= 0)
            {
                var k = Resolve(inner[..comma].Trim());
                var v = Resolve(inner[(comma + 1)..].Trim());
                return $"{{[{k}]: {v}}}";
            }
        }

        if (_symbols.LookupClass(csType) != null) return csType;

        return "any";
    }

    private static int FindTopLevelComma(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0) return i;
        }
        return -1;
    }
}
