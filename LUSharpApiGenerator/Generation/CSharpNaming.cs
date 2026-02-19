namespace LUSharpApiGenerator.Generation;

public class CSharpNaming
{
    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
        "lock", "long", "namespace", "new", "null", "object", "operator",
        "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        // Contextual keywords that may cause issues
        "value", "var"
    };

    public string EscapeIdentifier(string name)
    {
        // Remove spaces (e.g. "Active Color" → "ActiveColor")
        if (name.Contains(' '))
            name = name.Replace(" ", "");

        // Remove invalid characters
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        // Can't start with a digit
        if (name.Length > 0 && char.IsDigit(name[0]))
            name = "_" + name;

        if (CSharpKeywords.Contains(name))
            return "@" + name;

        return name;
    }

    public string EscapeParameterName(string name)
    {
        return EscapeIdentifier(name);
    }
}
