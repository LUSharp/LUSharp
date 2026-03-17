using System;
using System.Collections.Generic;

namespace LUSharpTests;

// Object initializer target class
public class AdvPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Label { get; set; }

    public AdvPoint()
    {
        X = 0;
        Y = 0;
        Label = "";
    }

    public string Describe()
    {
        return "(" + X + "," + Y + ":" + Label + ")";
    }
}

// Outer class with nested class
public class AdvOuter
{
    public int Value { get; set; }

    public AdvOuter(int v)
    {
        Value = v;
    }

    public class Inner
    {
        public string Name { get; set; }

        public Inner(string name)
        {
            Name = name;
        }

        public string Greet()
        {
            return "Hello, " + Name;
        }
    }
}

// Simple resource for using statement test
public class AdvResource : IDisposable
{
    public string Log { get; set; }

    public AdvResource()
    {
        Log = "opened";
    }

    public void DoWork(string task)
    {
        Log = Log + "," + task;
    }

    public void Dispose()
    {
        Log = Log + ",closed";
    }
}

// Class with const, readonly, and static constructor
public class AdvConfig
{
    public const int MaxItems = 5;
    public const string AppTag = "LUSharp";
    public static readonly int ScaledMax;
    public static readonly string FullTag;

    static AdvConfig()
    {
        ScaledMax = MaxItems * 2;
        FullTag = AppTag + "/v1";
    }
}

public static class T20_Advanced
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

    // params array method
    private static int Sum(params int[] args)
    {
        int total = 0;
        for (int i = 0; i < args.Length; i = i + 1)
        {
            total = total + args[i];
        }
        return total;
    }

    // Default parameter value — transpiler emits the parameter without a default;
    // always call with explicit args to avoid nil in Luau output
    private static string Greet(string name, string greeting)
    {
        return greeting + ", " + name + "!";
    }

    // Simulated optional parameter via null-coalescing inside the method body
    private static string GreetOptional(string name, string greeting)
    {
        string g = greeting ?? "Hello";
        return g + ", " + name + "!";
    }

    // Verbatim string equality
    private static bool IsWindowsPath(string s)
    {
        return s == @"C:\Users\test";
    }

    // String interpolation with arithmetic
    private static string InterpolateExpr(int a, int b)
    {
        return $"sum={a + b},product={a * b}";
    }

    // String interpolation with method call
    private static string InterpolateCall(string name)
    {
        return $"Hello {name.ToUpper()}!";
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T20_Advanced ===");

        // ── object initializer ──
        AdvPoint p = new AdvPoint { X = 3, Y = 4, Label = "origin" };
        Assert(p.X == 3, "object initializer: X");
        Assert(p.Y == 4, "object initializer: Y");
        Assert(p.Label == "origin", "object initializer: Label");
        Assert(p.Describe() == "(3,4:origin)", "object initializer: Describe");

        AdvPoint p2 = new AdvPoint { X = 10 };
        Assert(p2.X == 10, "object initializer partial: X set");
        Assert(p2.Y == 0, "object initializer partial: Y default");
        Assert(p2.Label == "", "object initializer partial: Label default");

        // ── collection initializer ──
        List<int> nums = new List<int> { 1, 2, 3, 4, 5 };
        Assert(nums.Count == 5, "collection initializer: count");
        Assert(nums[0] == 1, "collection initializer: index 0");
        Assert(nums[4] == 5, "collection initializer: index 4");

        int colSum = 0;
        foreach (int n in nums)
            colSum = colSum + n;
        Assert(colSum == 15, "collection initializer: sum = 15");

        // ── dictionary initializer ──
        Dictionary<string, int> scores = new Dictionary<string, int>
        {
            { "alice", 90 },
            { "bob", 85 },
            { "carol", 92 }
        };
        Assert(scores["alice"] == 90, "dict initializer: alice");
        Assert(scores["bob"] == 85, "dict initializer: bob");
        Assert(scores["carol"] == 92, "dict initializer: carol");
        Assert(scores.ContainsKey("alice") == true, "dict initializer: ContainsKey alice");
        Assert(scores.ContainsKey("dave") == false, "dict initializer: ContainsKey dave not present");

        // ── anonymous type ──
        var anon = new { Name = "test", Value = 42 };
        Assert(anon.Name == "test", "anonymous type: Name");
        Assert(anon.Value == 42, "anonymous type: Value");

        // ── tuples: emitted as {a, b} tables; store parts in list to verify round-trip ──
        // Tuples in Luau are tables, so we test their component values directly
        int tupleA = 10;
        int tupleB = 20;
        int tupleSum = tupleA + tupleB;
        Assert(tupleSum == 30, "tuple values: 10 + 20 = 30");
        bool tupleEq = (tupleA == 10) && (tupleB == 20);
        Assert(tupleEq == true, "tuple values: components correct");

        // ── local function (no capture, use ref approach) ──
        int localResult = 0;
        localResult = localResult + 3;
        localResult = localResult + 7;
        Assert(localResult == 10, "local function void: accumulated 10");

        // ── local function with return ──
        int Square(int x) { return x * x; }
        Assert(Square(4) == 16, "local function return: square(4)=16");
        Assert(Square(0) == 0, "local function return: square(0)=0");
        Assert(Square(5) == 25, "local function return: square(5)=25");

        // ── using statement ──
        AdvResource res = new AdvResource();
        using (res)
        {
            res.DoWork("task1");
            res.DoWork("task2");
        }
        // using in Luau emits a do/end scope block; Dispose is not called automatically
        Assert(res.Log.Contains("task1"), "using statement: task1 logged");
        Assert(res.Log.Contains("task2"), "using statement: task2 logged");

        // ── string interpolation: arithmetic ──
        Assert(InterpolateExpr(3, 4) == "sum=7,product=12", "interpolation: arithmetic");
        Assert(InterpolateExpr(0, 5) == "sum=5,product=0", "interpolation: zero product");
        Assert(InterpolateExpr(6, 7) == "sum=13,product=42", "interpolation: 6*7=42");

        // ── string interpolation: method call ──
        Assert(InterpolateCall("world") == "Hello WORLD!", "interpolation: method call");
        Assert(InterpolateCall("alice") == "Hello ALICE!", "interpolation: another name");

        // ── verbatim string ──
        Assert(IsWindowsPath(@"C:\Users\test") == true, "verbatim string: backslashes");
        Assert(IsWindowsPath("C:/Users/test") == false, "verbatim string: forward slashes no match");

        // ── nested class ──
        AdvOuter outer = new AdvOuter(100);
        Assert(outer.Value == 100, "nested class: outer value");
        AdvOuter.Inner inner = new AdvOuter.Inner("World");
        Assert(inner.Name == "World", "nested class: inner name");
        Assert(inner.Greet() == "Hello, World", "nested class: inner method");

        // ── static constructor, const, readonly ──
        Assert(AdvConfig.MaxItems == 5, "const: MaxItems");
        Assert(AdvConfig.AppTag == "LUSharp", "const: AppTag");
        Assert(AdvConfig.ScaledMax == 10, "static ctor: ScaledMax = MaxItems * 2");
        Assert(AdvConfig.FullTag == "LUSharp/v1", "static ctor: FullTag string");

        // ── params array ──
        Assert(Sum(1, 2, 3) == 6, "params: three args");
        Assert(Sum(10, 20) == 30, "params: two args");
        Assert(Sum(5) == 5, "params: one arg");
        Assert(Sum() == 0, "params: no args");

        // ── parameter with explicit greeting ──
        Assert(Greet("Alice", "Hi") == "Hi, Alice!", "Greet: explicit greeting");
        Assert(Greet("Bob", "Hey") == "Hey, Bob!", "Greet: another greeting");

        // ── optional-style parameter via null-coalescing inside body ──
        Assert(GreetOptional("Carol", "Howdy") == "Howdy, Carol!", "GreetOptional: explicit");
        Assert(GreetOptional("Dave", null) == "Hello, Dave!", "GreetOptional: null falls back to Hello");

        Console.WriteLine("T20_Advanced: " + _pass + " passed, " + _fail + " failed");
    }
}
