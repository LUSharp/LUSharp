using Microsoft.CodeAnalysis;

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

    /// <summary>
    /// Map a Roslyn ITypeSymbol to a Luau type annotation string.
    /// </summary>
    public static string MapTypeSymbol(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
            or SpecialType.System_Byte or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal or SpecialType.System_Char
            or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_UInt16
            or SpecialType.System_SByte => "number",

            SpecialType.System_Boolean => "boolean",
            SpecialType.System_String => "string",
            SpecialType.System_Object => "any",
            SpecialType.System_Void => "()",
            _ => MapComplexTypeSymbol(type),
        };
    }

    private static string MapComplexTypeSymbol(ITypeSymbol type)
    {
        // Nullable<T> → T?
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && type is INamedTypeSymbol { TypeArguments.Length: 1 } nullable)
            return MapTypeSymbol(nullable.TypeArguments[0]) + "?";

        // Arrays → { T }
        if (type is IArrayTypeSymbol arrayType)
            return $"{{ {MapTypeSymbol(arrayType.ElementType)} }}";

        // Generic collections
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            var typeArgs = namedType.TypeArguments;

            return name switch
            {
                "List" or "IList" or "ICollection" or "IEnumerable" or "IReadOnlyList"
                    or "Queue" or "Stack" or "LinkedList"
                    when typeArgs.Length == 1
                    => $"{{ {MapTypeSymbol(typeArgs[0])} }}",

                "Dictionary" or "IDictionary" or "ConcurrentDictionary" or "SortedDictionary"
                    when typeArgs.Length == 2
                    => $"{{ [{MapTypeSymbol(typeArgs[0])}]: {MapTypeSymbol(typeArgs[1])} }}",

                "HashSet" or "ISet" when typeArgs.Length == 1
                    => $"{{ [{MapTypeSymbol(typeArgs[0])}]: boolean }}",

                "KeyValuePair" when typeArgs.Length == 2
                    => $"{{ Key: {MapTypeSymbol(typeArgs[0])}, Value: {MapTypeSymbol(typeArgs[1])} }}",

                "Tuple" or "ValueTuple" when typeArgs.Length >= 2
                    => $"{{ {string.Join(", ", typeArgs.Select(t => MapTypeSymbol(t)))} }}",

                "Task" when typeArgs.Length == 1
                    => MapTypeSymbol(typeArgs[0]),

                "Func" when typeArgs.Length >= 1
                    => $"({string.Join(", ", typeArgs.Take(typeArgs.Length - 1).Select(t => MapTypeSymbol(t)))}) -> {MapTypeSymbol(typeArgs.Last())}",

                "Action" when typeArgs.Length >= 1
                    => $"({string.Join(", ", typeArgs.Select(t => MapTypeSymbol(t)))}) -> ()",

                "Predicate" when typeArgs.Length == 1
                    => $"({MapTypeSymbol(typeArgs[0])}) -> boolean",

                _ => type.Name, // Keep as user-defined type name
            };
        }

        // Task without type argument → ()
        if (type.Name == "Task" && type is INamedTypeSymbol { TypeArguments.Length: 0 })
            return "()";

        // Enum → number
        if (type.TypeKind == TypeKind.Enum)
            return "number";

        // Interface → keep name
        if (type.TypeKind == TypeKind.Interface)
            return type.Name;

        // Fallback: use the type name
        return type.Name;
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
