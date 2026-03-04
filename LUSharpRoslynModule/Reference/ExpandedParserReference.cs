namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Reference test for expanded parser features: for/foreach/switch/try-catch/
/// break/continue/element-access/postfix/ternary/lambda/object-initializers.
/// Produces expected TreePrinter output matching the transpiled Luau SimpleParser.
/// </summary>
public static class ExpandedParserReference
{
    public static void PrintAll()
    {
        string input = @"class TestClass {
    void ForLoop() {
        for (int i = 0; i < 10; i++) {
            if (i == 5) break;
            continue;
        }
    }
    void ForEachLoop(int[] items) {
        foreach (var item in items) {
            int x = item;
        }
    }
    void SwitchTest(int val) {
        switch (val) {
            case 1:
                return;
            default:
                break;
        }
    }
    void TryCatchTest() {
        try {
            int x = 1;
        } catch (Exception ex) {
            throw;
        }
    }
    void ExpressionTests(int[] arr) {
        int x = arr[0];
        x++;
        int y = x > 0 ? 1 : 0;
        var fn = x => x + 1;
        var obj = new Foo() { X = 1 };
    }
}";

        Console.WriteLine("=== Expanded Parser Test ===");
        Console.WriteLine("Input:");
        Console.WriteLine(input);
        Console.WriteLine();

        Console.WriteLine("=== Tree Output ===");
        Console.WriteLine("CompilationUnit");
        Console.WriteLine("  ClassDeclaration: TestClass");

        // ForLoop
        Console.WriteLine("    MethodDeclaration: void ForLoop");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ForStatement");
        Console.WriteLine("          LocalDeclaration: int i");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("          BinaryExpression: <");
        Console.WriteLine("            Identifier: i");
        Console.WriteLine("            Literal: 10");
        Console.WriteLine("          PostfixUnary: ++");
        Console.WriteLine("            Identifier: i");
        Console.WriteLine("          Block (2 statements)");
        Console.WriteLine("            IfStatement");
        Console.WriteLine("              BinaryExpression: ==");
        Console.WriteLine("                Identifier: i");
        Console.WriteLine("                Literal: 5");
        Console.WriteLine("              BreakStatement");
        Console.WriteLine("            ContinueStatement");

        // ForEachLoop
        Console.WriteLine("    MethodDeclaration: void ForEachLoop");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ForEachStatement: var item");
        Console.WriteLine("          Identifier: items");
        Console.WriteLine("          Block (1 statements)");
        Console.WriteLine("            LocalDeclaration: int x");
        Console.WriteLine("              Identifier: item");

        // SwitchTest
        Console.WriteLine("    MethodDeclaration: void SwitchTest");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        SwitchStatement");
        Console.WriteLine("          Identifier: val");
        Console.WriteLine("          SwitchSection: 1 case(s)");
        Console.WriteLine("            Literal: 1");
        Console.WriteLine("            ReturnStatement");
        Console.WriteLine("          SwitchSection: 0 case(s) +default");
        Console.WriteLine("            BreakStatement");

        // TryCatchTest
        Console.WriteLine("    MethodDeclaration: void TryCatchTest");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        TryStatement (1 catch)");
        Console.WriteLine("          Block (1 statements)");
        Console.WriteLine("            LocalDeclaration: int x");
        Console.WriteLine("              Literal: 1");
        Console.WriteLine("          CatchClause: Exception ex");
        Console.WriteLine("            Block (1 statements)");
        Console.WriteLine("              ThrowStatement (re-throw)");

        // ExpressionTests
        Console.WriteLine("    MethodDeclaration: void ExpressionTests");
        Console.WriteLine("      Block (5 statements)");
        Console.WriteLine("        LocalDeclaration: int x");
        Console.WriteLine("          ElementAccess");
        Console.WriteLine("            Identifier: arr");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          PostfixUnary: ++");
        Console.WriteLine("            Identifier: x");
        Console.WriteLine("        LocalDeclaration: int y");
        Console.WriteLine("          ConditionalExpression");
        Console.WriteLine("            BinaryExpression: >");
        Console.WriteLine("              Identifier: x");
        Console.WriteLine("              Literal: 0");
        Console.WriteLine("            Literal: 1");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("        LocalDeclaration: var fn");
        Console.WriteLine("          Lambda (1 params)");
        Console.WriteLine("            BinaryExpression: +");
        Console.WriteLine("              Identifier: x");
        Console.WriteLine("              Literal: 1");
        Console.WriteLine("        LocalDeclaration: var obj");
        Console.WriteLine("          ObjectCreation: Foo");
    }
}
