using Microsoft.CodeAnalysis.CSharp;

namespace LUSharpRoslynModule.Reference;

public static class SyntaxKindReference
{
    public static void PrintAll()
    {
        foreach (var name in Enum.GetNames<SyntaxKind>())
        {
            var value = (ushort)(SyntaxKind)Enum.Parse<SyntaxKind>(name);
            Console.WriteLine($"{name}={value}");
        }
    }

    public static void PrintCount()
    {
        Console.WriteLine($"SyntaxKind member count: {Enum.GetNames<SyntaxKind>().Length}");
    }

    public static void PrintSpotCheck()
    {
        var checks = new[]
        {
            SyntaxKind.None,
            SyntaxKind.ClassKeyword,
            SyntaxKind.IntKeyword,
            SyntaxKind.StringKeyword,
            SyntaxKind.IfKeyword,
            SyntaxKind.SemicolonToken,
            SyntaxKind.OpenBraceToken,
            SyntaxKind.CloseBraceToken,
            SyntaxKind.IdentifierToken,
            SyntaxKind.NumericLiteralToken,
            SyntaxKind.StringLiteralToken,
        };

        foreach (var kind in checks)
        {
            Console.WriteLine($"{kind}={(ushort)kind}");
        }
    }
}
