using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

public static class SyntaxFactsReference
{
    // Some SyntaxFacts methods are internal in Roslyn — access via reflection
    private static readonly Type s_type = typeof(SyntaxFacts);

    private static T CallInternal<T>(string name, params object[] args)
    {
        var method = s_type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
            throw new InvalidOperationException($"SyntaxFacts.{name} not found");
        return (T)method.Invoke(null, args)!;
    }

    private static bool IsHexDigit(char c) => CallInternal<bool>("IsHexDigit", c);
    private static bool IsDecDigit(char c) => CallInternal<bool>("IsDecDigit", c);
    private static bool IsBinaryDigit(char c) => CallInternal<bool>("IsBinaryDigit", c);
    private static int HexValue(char c) => CallInternal<int>("HexValue", c);
    private static int DecValue(char c) => CallInternal<int>("DecValue", c);
    private static bool IsWhitespace(char c) => CallInternal<bool>("IsWhitespace", c);
    private static bool IsNewLine(char c) => CallInternal<bool>("IsNewLine", c);
    private static bool IsLiteral(SyntaxKind kind) => CallInternal<bool>("IsLiteral", kind);

    public static void PrintCharClassification()
    {
        // Test character classification methods
        var testChars = new[] { '0', '9', 'a', 'f', 'g', 'A', 'F', 'G', ' ', '\t', '\n', '\r', 'z' };
        foreach (var c in testChars)
        {
            Console.WriteLine($"char={(int)c}|hex={IsHexDigit(c)}|dec={IsDecDigit(c)}|bin={IsBinaryDigit(c)}|ws={IsWhitespace(c)}|nl={IsNewLine(c)}");
        }
    }

    public static void PrintHexValues()
    {
        // Test hex/dec/bin value conversion
        var hexChars = new[] { '0', '5', '9', 'a', 'c', 'f', 'A', 'C', 'F' };
        foreach (var c in hexChars)
        {
            Console.WriteLine($"char={(int)c}|hexval={HexValue(c)}|decval={DecValue(c)}");
        }
    }

    public static void PrintKeywordClassification()
    {
        // Test SyntaxKind classification for a selection of kinds
        var kinds = new[]
        {
            SyntaxKind.None,
            SyntaxKind.ClassKeyword,
            SyntaxKind.IntKeyword,
            SyntaxKind.IfKeyword,
            SyntaxKind.SemicolonToken,
            SyntaxKind.PlusToken,
            SyntaxKind.IdentifierToken,
            SyntaxKind.NumericLiteralToken,
            SyntaxKind.TrueKeyword,
            SyntaxKind.FalseKeyword,
            SyntaxKind.PublicKeyword,
            SyntaxKind.AsyncKeyword,
        };

        foreach (var kind in kinds)
        {
            Console.WriteLine($"kind={(int)kind}|kw={SyntaxFacts.IsKeywordKind(kind)}|reserved={SyntaxFacts.IsReservedKeyword(kind)}|punct={SyntaxFacts.IsPunctuation(kind)}|literal={IsLiteral(kind)}|token={SyntaxFacts.IsAnyToken(kind)}");
        }
    }

    public static void PrintGetText()
    {
        // Test GetText for key tokens/keywords
        var kinds = new[]
        {
            SyntaxKind.PlusToken,
            SyntaxKind.MinusToken,
            SyntaxKind.AsteriskToken,
            SyntaxKind.SlashToken,
            SyntaxKind.EqualsToken,
            SyntaxKind.SemicolonToken,
            SyntaxKind.OpenBraceToken,
            SyntaxKind.CloseBraceToken,
            SyntaxKind.ClassKeyword,
            SyntaxKind.IntKeyword,
            SyntaxKind.StringKeyword,
            SyntaxKind.BoolKeyword,
            SyntaxKind.VoidKeyword,
            SyntaxKind.ReturnKeyword,
            SyntaxKind.IfKeyword,
            SyntaxKind.ElseKeyword,
            SyntaxKind.WhileKeyword,
            SyntaxKind.ForKeyword,
            SyntaxKind.TrueKeyword,
            SyntaxKind.FalseKeyword,
            SyntaxKind.NullKeyword,
            SyntaxKind.PublicKeyword,
            SyntaxKind.PrivateKeyword,
            SyntaxKind.StaticKeyword,
        };

        foreach (var kind in kinds)
        {
            var text = SyntaxFacts.GetText(kind);
            Console.WriteLine($"kind={(int)kind}|text={text}");
        }
    }

    public static void PrintGetKeywordKind()
    {
        // Test reverse lookup: text -> SyntaxKind
        var keywords = new[] { "class", "int", "string", "bool", "void", "return", "if", "else", "while", "for", "true", "false", "null", "public", "private", "static", "new", "this" };
        foreach (var kw in keywords)
        {
            var kind = SyntaxFacts.GetKeywordKind(kw);
            Console.WriteLine($"text={kw}|kind={(int)kind}");
        }
    }

    public static void PrintAll()
    {
        Console.WriteLine("=== Character Classification ===");
        PrintCharClassification();
        Console.WriteLine("=== Hex Values ===");
        PrintHexValues();
        Console.WriteLine("=== Keyword Classification ===");
        PrintKeywordClassification();
        Console.WriteLine("=== GetText ===");
        PrintGetText();
        Console.WriteLine("=== GetKeywordKind ===");
        PrintGetKeywordKind();
    }
}
