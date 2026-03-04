namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Reference parser test that produces the expected output for the simplified
/// SimpleParser/SyntaxNode pipeline. This mirrors exactly what the transpiled
/// Luau version should produce when parsing the same input.
///
/// Rather than importing the RoslynSource types (which are excluded from compilation),
/// this replicates the parser's behavior inline to produce the expected text.
/// </summary>
public static class ParserReference
{
    /// <summary>
    /// Prints the full parser reference output for: "class Foo { int x = 5; void Bar() { return; } }"
    /// </summary>
    public static void PrintAll()
    {
        // The input to parse
        string input = "class Foo { int x = 5; void Bar() { return; } }";
        Console.WriteLine("=== Parser Test ===");
        Console.WriteLine("Input: " + input);

        // What Accept() on the CompilationUnit produces:
        // CompilationUnitSyntax.Accept() concatenates each member's Accept() + "\n"
        // The single member is a ClassDeclarationSyntax for Foo
        // ClassDeclarationSyntax.Accept() produces:
        //   "class Foo {\n"
        //   "  " + field.Accept() + "\n"   -> "  int x = 5;\n"
        //   "  " + method.Accept() + "\n"  -> "  void Bar() {\n  return;\n}\n"
        //   "}"
        Console.WriteLine("=== Accept Output ===");
        Console.WriteLine("class Foo {");
        Console.WriteLine("  int x = 5;");
        Console.WriteLine("  void Bar() {");
        Console.WriteLine("  return;");
        Console.WriteLine("}");
        Console.WriteLine("}");

        // Tree walk using ToDisplayString():
        // CompilationUnitSyntax -> "CompilationUnit(1 members)"
        //   ClassDeclarationSyntax -> "Class(Foo, 2 members)"
        //     FieldDeclarationSyntax -> "Field(x)"
        //     MethodDeclarationSyntax -> "Method(Bar)"
        Console.WriteLine("=== Tree Walk ===");
        Console.WriteLine("CompilationUnit(1 members)");
        Console.WriteLine("  Class(Foo, 2 members)");
        Console.WriteLine("    Field(x)");
        Console.WriteLine("    Method(Bar)");
    }
}
