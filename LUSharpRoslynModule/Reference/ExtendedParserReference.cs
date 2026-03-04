namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Reference test for extended parser features: enums, structs, properties,
/// and constructors. Produces expected output matching the transpiled Luau
/// SimpleParser when parsing the same inputs.
/// </summary>
public static class ExtendedParserReference
{
    public static void PrintAll()
    {
        PrintClassTest();
        PrintEnumTest();
        PrintStructTest();
        PrintConstructorTest();
    }

    /// <summary>
    /// Test 1: Basic class (same as original parser test, reformatted for multi-test output)
    /// Input: "class Foo { int x = 5; void Bar() { return; } }"
    /// </summary>
    private static void PrintClassTest()
    {
        string input = "class Foo { int x = 5; void Bar() { return; } }";
        Console.WriteLine("=== Parser Test 1: Class ===");
        Console.WriteLine("Input: " + input);

        Console.WriteLine("--- Accept Output ---");
        Console.WriteLine("class Foo {");
        Console.WriteLine("  int x = 5;");
        Console.WriteLine("  void Bar() {");
        Console.WriteLine("  return;");
        Console.WriteLine("}");
        Console.WriteLine("}");

        Console.WriteLine("--- Tree Walk ---");
        Console.WriteLine("CompilationUnit(1 members)");
        Console.WriteLine("  Class(Foo, 2 members)");
        Console.WriteLine("    Field(x)");
        Console.WriteLine("    Method(Bar)");
    }

    /// <summary>
    /// Test 2: Enum declaration
    /// Input: "enum Color { Red, Green = 1, Blue }"
    /// </summary>
    private static void PrintEnumTest()
    {
        string input = "enum Color { Red, Green = 1, Blue }";
        Console.WriteLine("=== Parser Test 2: Enum ===");
        Console.WriteLine("Input: " + input);

        Console.WriteLine("--- Accept Output ---");
        Console.WriteLine("enum Color {");
        Console.WriteLine("  Red,");
        Console.WriteLine("  Green = 1,");
        Console.WriteLine("  Blue");
        Console.WriteLine("}");

        Console.WriteLine("--- Tree Walk ---");
        Console.WriteLine("CompilationUnit(1 members)");
        Console.WriteLine("  Enum(Color, 3 members)");
        Console.WriteLine("    Red");
        Console.WriteLine("    Green = 1");
        Console.WriteLine("    Blue");
    }

    /// <summary>
    /// Test 3: Struct with auto-properties
    /// Input: "struct Point { int X { get; set; } int Y { get; set; } }"
    /// </summary>
    private static void PrintStructTest()
    {
        string input = "struct Point { int X { get; set; } int Y { get; set; } }";
        Console.WriteLine("=== Parser Test 3: Struct ===");
        Console.WriteLine("Input: " + input);

        Console.WriteLine("--- Accept Output ---");
        Console.WriteLine("struct Point {");
        Console.WriteLine("  int X { get; set; }");
        Console.WriteLine("  int Y { get; set; }");
        Console.WriteLine("}");

        Console.WriteLine("--- Tree Walk ---");
        Console.WriteLine("CompilationUnit(1 members)");
        Console.WriteLine("  Struct(Point, 2 members)");
        Console.WriteLine("    Property(X)");
        Console.WriteLine("    Property(Y)");
    }

    /// <summary>
    /// Test 4: Class with constructor
    /// Input: "class MyClass { int _value; MyClass(int v) { _value = v; } }"
    /// </summary>
    private static void PrintConstructorTest()
    {
        string input = "class MyClass { int _value; MyClass(int v) { _value = v; } }";
        Console.WriteLine("=== Parser Test 4: Constructor ===");
        Console.WriteLine("Input: " + input);

        Console.WriteLine("--- Accept Output ---");
        Console.WriteLine("class MyClass {");
        Console.WriteLine("  int _value;");
        Console.WriteLine("  MyClass(int v) {");
        Console.WriteLine("  _value = v;");
        Console.WriteLine("}");
        Console.WriteLine("}");

        Console.WriteLine("--- Tree Walk ---");
        Console.WriteLine("CompilationUnit(1 members)");
        Console.WriteLine("  Class(MyClass, 2 members)");
        Console.WriteLine("    Field(_value)");
        Console.WriteLine("    Constructor(MyClass)");
    }
}
