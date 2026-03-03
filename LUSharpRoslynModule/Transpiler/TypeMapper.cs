namespace LUSharpRoslynModule.Transpiler;

public static class TypeMapper
{
    private static readonly Dictionary<string, string> PrimitiveMap = new()
    {
        ["int"] = "number", ["uint"] = "number", ["long"] = "number",
        ["ulong"] = "number", ["short"] = "number", ["ushort"] = "number",
        ["byte"] = "number", ["sbyte"] = "number", ["float"] = "number",
        ["double"] = "number", ["decimal"] = "number",
        ["bool"] = "boolean", ["string"] = "string", ["char"] = "number",
        ["object"] = "any", ["void"] = "()",
        // .NET type names
        ["Int32"] = "number", ["UInt32"] = "number", ["Int64"] = "number",
        ["UInt64"] = "number", ["Int16"] = "number", ["UInt16"] = "number",
        ["Byte"] = "number", ["SByte"] = "number", ["Single"] = "number",
        ["Double"] = "number", ["Decimal"] = "number",
        ["Boolean"] = "boolean", ["String"] = "string", ["Char"] = "number",
        ["Object"] = "any", ["Void"] = "()",
        // Roslyn/Roblox types that map to number
        ["SyntaxKind"] = "number", ["Accessibility"] = "number",
    };

    public static string? MapType(string csharpType)
    {
        if (PrimitiveMap.TryGetValue(csharpType, out var luauType))
            return luauType;
        return null;
    }

    public static string MapEnumBaseType(string? baseType)
    {
        return baseType switch
        {
            "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" => "number",
            _ => "number"
        };
    }
}
