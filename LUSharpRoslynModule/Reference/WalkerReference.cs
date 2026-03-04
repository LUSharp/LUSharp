namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Produces expected TreePrinter output for validation.
/// We manually construct the expected tree dump since we can't run the
/// RoslynSource files (they're excluded from compilation).
/// </summary>
public static class WalkerReference
{
    public static void PrintAll()
    {
        // Test input: a non-trivial C# class
        string input = @"class Calculator {
    int _result;

    Calculator(int initial) {
        _result = initial;
    }

    int Add(int a, int b) {
        return a + b;
    }

    void Reset() {
        _result = 0;
    }

    static int Max(int a, int b) {
        if (a > b) {
            return a;
        }
        return b;
    }
}

enum Operation { Add, Subtract = 1, Multiply }";

        Console.WriteLine("=== Walker Test ===");
        Console.WriteLine("Input:");
        Console.WriteLine(input);
        Console.WriteLine();

        // Expected tree output (what the parser + walker SHOULD produce)
        // This must match exactly what the Luau version produces
        Console.WriteLine("=== Tree Output ===");
        Console.WriteLine("CompilationUnit");
        Console.WriteLine("  ClassDeclaration: Calculator");
        Console.WriteLine("    FieldDeclaration: int _result");
        Console.WriteLine("    ConstructorDeclaration: Calculator");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: _result");
        Console.WriteLine("            Identifier: initial");
        Console.WriteLine("    MethodDeclaration: int Add");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          BinaryExpression: +");
        Console.WriteLine("            Identifier: a");
        Console.WriteLine("            Identifier: b");
        Console.WriteLine("    MethodDeclaration: void Reset");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: _result");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("    MethodDeclaration: static int Max");
        Console.WriteLine("      Block (2 statements)");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: >");
        Console.WriteLine("            Identifier: a");
        Console.WriteLine("            Identifier: b");
        Console.WriteLine("          Block (1 statements)");
        Console.WriteLine("            ReturnStatement");
        Console.WriteLine("              Identifier: a");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          Identifier: b");
        Console.WriteLine("  EnumDeclaration: Operation (3 members)");
    }
}
