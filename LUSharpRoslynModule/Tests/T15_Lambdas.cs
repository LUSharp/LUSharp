using System;
using System.Collections.Generic;
using System.Linq;

namespace LUSharpTests;

public static class T15_Lambdas
{
    private static int _pass = 0;
    private static int _fail = 0;

    private static void Assert(bool condition, string name)
    {
        if (condition)
        {
            _pass = _pass + 1;
            Console.WriteLine("  PASS: " + name);
        }
        else
        {
            _fail = _fail + 1;
            Console.WriteLine("  FAIL: " + name);
        }
    }

    // Helper: accepts a Func<int,int> and applies it to a value
    private static int Apply(Func<int, int> fn, int value)
    {
        return fn(value);
    }

    // Helper: accepts an Action and calls it
    private static void Invoke(Action action)
    {
        action();
    }

    // Helper: accepts a Func<int,int,int> and applies it
    private static int ApplyBinary(Func<int, int, int> fn, int a, int b)
    {
        return fn(a, b);
    }

    // Helper: accepts a Func<int,bool> predicate
    private static bool Check(Func<int, bool> pred, int value)
    {
        return pred(value);
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T15_Lambdas ===");

        // --- Simple lambda: x => x * 2 ---
        Func<int, int> doubler = x => x * 2;
        Assert(doubler(5) == 10, "simple lambda: x => x * 2");
        Assert(doubler(0) == 0, "simple lambda: 0 * 2 = 0");

        // --- Multi-param lambda: (a, b) => a + b ---
        Func<int, int, int> adder = (a, b) => a + b;
        Assert(adder(3, 4) == 7, "multi-param lambda: (a,b) => a + b");
        Assert(adder(-1, 1) == 0, "multi-param lambda: sum to zero");

        // --- Lambda with block body: x => { return x; } ---
        Func<int, int> identity = x => { return x; };
        Assert(identity(42) == 42, "block body lambda: returns argument");
        Assert(identity(-7) == -7, "block body lambda: returns negative");

        // --- Lambda assigned to variable ---
        Func<int, bool> isEven = n => n % 2 == 0;
        Assert(isEven(4) == true, "lambda variable: 4 is even");
        Assert(isEven(7) == false, "lambda variable: 7 is not even");

        // --- Lambda as method parameter ---
        int applyResult = Apply(x => x * x, 6);
        Assert(applyResult == 36, "lambda as parameter: square function");

        int binaryResult = ApplyBinary((a, b) => a * b, 4, 5);
        Assert(binaryResult == 20, "multi-param lambda as parameter: multiply");

        bool checkResult = Check(n => n > 10, 15);
        Assert(checkResult == true, "predicate lambda as parameter: 15 > 10");

        // --- Action (void lambda) ---
        int sideEffect = 0;
        Action setToFive = () => { sideEffect = 5; };
        setToFive();
        Assert(sideEffect == 5, "Action lambda: void side effect");

        Action incrementTwice = () => { sideEffect = sideEffect + 1; sideEffect = sideEffect + 1; };
        incrementTwice();
        Assert(sideEffect == 7, "Action lambda: multi-statement void body");

        Invoke(() => { sideEffect = 99; });
        Assert(sideEffect == 99, "Action lambda passed to method");

        // --- Func<int,int> returning lambda ---
        Func<int, int> triple = n => n * 3;
        Assert(triple(7) == 21, "Func<int,int>: returning lambda");

        // --- Lambda capturing outer variable (closure) ---
        int multiplier = 4;
        Func<int, int> capturedMul = x => x * multiplier;
        Assert(capturedMul(3) == 12, "closure: captures outer variable value");
        Assert(capturedMul(5) == 20, "closure: re-evaluates with same captured value");

        // --- Closure captures and modifies outer variable ---
        int counter = 0;
        Action increment = () => { counter = counter + 1; };
        increment();
        increment();
        increment();
        Assert(counter == 3, "closure: captures and modifies outer variable");

        // --- Lambda in List.Find ---
        List<string> names = new List<string> { "Alice", "Bob", "Charlie", "David" };
        string found = names.Find(n => n.StartsWith("C"));
        Assert(found == "Charlie", "lambda in Find: finds Charlie");

        string notFound = names.Find(n => n.StartsWith("Z"));
        Assert(notFound == null, "lambda in Find: returns null when not found");

        // --- Lambda in Where/Select chain ---
        List<int> nums = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        List<int> evenSquares = nums.Where(x => x % 2 == 0).Select(x => x * x).ToList();
        Assert(evenSquares.Count == 5, "Where/Select chain: count");
        Assert(evenSquares[0] == 4, "Where/Select chain: first is 4");
        Assert(evenSquares[4] == 100, "Where/Select chain: last is 100");

        // --- Nested lambdas (currying) ---
        Func<int, Func<int, int>> add = a => b => a + b;
        Func<int, int> add5 = add(5);
        Assert(add5(3) == 8, "nested lambda: curried add 5+3=8");
        Assert(add5(10) == 15, "nested lambda: curried add 5+10=15");

        Func<int, int> add10 = add(10);
        Assert(add10(7) == 17, "nested lambda: curried add 10+7=17");

        // --- Multiple lambdas sharing a captured variable ---
        int shared = 0;
        Action addOne = () => { shared = shared + 1; };
        Action addTwo = () => { shared = shared + 2; };
        addOne();
        addTwo();
        addOne();
        Assert(shared == 4, "multiple lambdas sharing captured variable");

        // --- Immediately invoked lambda (IIFE) ---
        // TODO: Emitter doesn't wrap IIFE in parens yet
        Func<int, int> iifeFunc = (x => x + 100);
        int immediateResult = iifeFunc(42);
        Assert(immediateResult == 142, "immediately invoked lambda via variable");

        // --- Lambda with complex block body ---
        Func<int, string> classify = n => {
            if (n < 0)
            {
                return "negative";
            }
            if (n == 0)
            {
                return "zero";
            }
            return "positive";
        };
        Assert(classify(-5) == "negative", "complex lambda body: negative");
        Assert(classify(0) == "zero", "complex lambda body: zero");
        Assert(classify(7) == "positive", "complex lambda body: positive");

        // --- Loop closure: each iteration captures its own copy ---
        List<Func<int>> funcs = new List<Func<int>>();
        int captureBase = 10;
        for (int i = 0; i < 3; i++)
        {
            int captured = captureBase + i;
            funcs.Add(() => captured);
        }
        Assert(funcs[0]() == 10, "loop closure: first lambda captures 10");
        Assert(funcs[1]() == 11, "loop closure: second lambda captures 11");
        Assert(funcs[2]() == 12, "loop closure: third lambda captures 12");

        // --- Lambda returning lambda ---
        Func<int, Func<int, bool>> greaterThan = threshold => value => value > threshold;
        Func<int, bool> gt5 = greaterThan(5);
        Assert(gt5(6) == true, "lambda returning lambda: 6 > 5");
        Assert(gt5(3) == false, "lambda returning lambda: 3 not > 5");
        Func<int, bool> gt0 = greaterThan(0);
        Assert(gt0(1) == true, "lambda returning lambda: 1 > 0");
        Assert(gt0(-1) == false, "lambda returning lambda: -1 not > 0");

        // --- Lambda modifying local variable via block body ---
        int accumulator = 0;
        Action addToAccumulator = () => {
            accumulator = accumulator + 10;
            accumulator = accumulator + 5;
        };
        addToAccumulator();
        Assert(accumulator == 15, "lambda block body: modifies captured via multiple statements");

        Console.WriteLine("T15_Lambdas: " + _pass + " passed, " + _fail + " failed");
    }
}
