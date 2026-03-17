namespace LUSharpTests;

public static class TestRunner
{
    public static void RunAll()
    {
        Console.WriteLine("=== LUSharp Transpiler Test Suite ===");
        Console.WriteLine();

        T01_Primitives.Run();
        T02_Arithmetic.Run();
        T03_Strings.Run();
        T04_ControlFlow.Run();
        T05_Classes.Run();
        T06_Inheritance.Run();
        T07_Interfaces.Run();
        T08_Structs.Run();
        T09_Enums.Run();
        T10_Collections.Run();
        T11_Arrays.Run();
        T12_Linq.Run();
        T13_Exceptions.Run();
        T14_Operators.Run();
        T15_Lambdas.Run();
        T16_Patterns.Run();
        T17_Generics.Run();
        T18_Nullable.Run();
        T19_Math.Run();
        T20_Advanced.Run();

        Console.WriteLine("=== All test categories complete ===");
    }
}
