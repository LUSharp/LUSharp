using System;
using System.Collections.Generic;

namespace LUSharpTests;

// Generic class: Box<T>
public class GenBox<T>
{
    public T Value { get; set; }

    public GenBox(T value)
    {
        Value = value;
    }

    public string Describe()
    {
        return "Box(" + Value + ")";
    }
}

// Generic class with two type parameters
public class GenPair<T1, T2>
{
    public T1 First { get; set; }
    public T2 Second { get; set; }

    public GenPair(T1 first, T2 second)
    {
        First = first;
        Second = second;
    }

    public string Describe()
    {
        return "(" + First + ", " + Second + ")";
    }
}

// Generic stack built on List<T>
public class GenStack<T>
{
    private List<T> _items;

    public GenStack()
    {
        _items = new List<T>();
    }

    public void Push(T item)
    {
        _items.Add(item);
    }

    public T Pop()
    {
        int last = _items.Count - 1;
        T item = _items[last];
        _items.RemoveAt(last);
        return item;
    }

    public int Count()
    {
        return _items.Count;
    }

    public bool IsEmpty()
    {
        return _items.Count == 0;
    }
}

public static class T17_Generics
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

    // Generic identity method
    private static T Identity<T>(T val)
    {
        return val;
    }

    // Generic swap method returns new pair
    private static GenPair<T2, T1> Swap<T1, T2>(T1 a, T2 b)
    {
        return new GenPair<T2, T1>(b, a);
    }

    // Generic method with class constraint
    private static string DescribeClass<T>(T obj) where T : class
    {
        if (obj == null)
            return "null";
        return obj.ToString();
    }

    // Generic default value
    private static T GetDefault<T>()
    {
        return default(T);
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T17_Generics ===");

        // ── GenBox<int> ──
        GenBox<int> intBox = new GenBox<int>(42);
        Assert(intBox.Value == 42, "GenBox<int>: value stored");
        Assert(intBox.Describe() == "Box(42)", "GenBox<int>: Describe");

        // ── GenBox<string> ──
        GenBox<string> strBox = new GenBox<string>("hello");
        Assert(strBox.Value == "hello", "GenBox<string>: value stored");
        Assert(strBox.Describe() == "Box(hello)", "GenBox<string>: Describe");

        // ── GenBox<bool> ──
        GenBox<bool> boolBox = new GenBox<bool>(true);
        Assert(boolBox.Value == true, "GenBox<bool>: value true");

        // ── mutation ──
        intBox.Value = 99;
        Assert(intBox.Value == 99, "GenBox<int>: value mutation");

        // ── Identity<T> ──
        Assert(Identity<int>(5) == 5, "Identity<int>: same int");
        Assert(Identity<string>("abc") == "abc", "Identity<string>: same string");
        Assert(Identity<bool>(false) == false, "Identity<bool>: false");

        // ── GenPair<T1, T2> ──
        GenPair<int, string> p = new GenPair<int, string>(1, "one");
        Assert(p.First == 1, "GenPair<int,string>: First");
        Assert(p.Second == "one", "GenPair<int,string>: Second");
        Assert(p.Describe() == "(1, one)", "GenPair<int,string>: Describe");

        GenPair<string, bool> pb = new GenPair<string, bool>("flag", true);
        Assert(pb.First == "flag", "GenPair<string,bool>: First");
        Assert(pb.Second == true, "GenPair<string,bool>: Second");

        // ── Swap<T1, T2> ──
        GenPair<string, int> swapped = Swap<int, string>(10, "ten");
        Assert(swapped.First == "ten", "Swap: First is original second");
        Assert(swapped.Second == 10, "Swap: Second is original first");

        // ── class constraint ──
        string desc = DescribeClass<string>("test");
        Assert(desc == "test", "DescribeClass<string>: non-null value");

        // ── GenStack<int> ──
        GenStack<int> stack = new GenStack<int>();
        Assert(stack.IsEmpty() == true, "GenStack<int>: starts empty");
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        Assert(stack.Count() == 3, "GenStack<int>: count 3 after pushes");
        Assert(stack.Pop() == 3, "GenStack<int>: Pop returns 3");
        Assert(stack.Pop() == 2, "GenStack<int>: Pop returns 2");
        Assert(stack.Count() == 1, "GenStack<int>: count 1 after 2 pops");

        // ── GenStack<string> ──
        GenStack<string> strStack = new GenStack<string>();
        strStack.Push("a");
        strStack.Push("b");
        Assert(strStack.Pop() == "b", "GenStack<string>: Pop returns b");
        Assert(strStack.Pop() == "a", "GenStack<string>: Pop returns a");
        Assert(strStack.IsEmpty() == true, "GenStack<string>: empty after all pops");

        // ── generic collection: List<string> vs List<int> ──
        List<string> strList = new List<string>();
        strList.Add("x");
        strList.Add("y");
        Assert(strList.Count == 2, "List<string>: count 2");
        Assert(strList[0] == "x", "List<string>: index 0");

        List<int> intList = new List<int>();
        intList.Add(10);
        intList.Add(20);
        Assert(intList.Count == 2, "List<int>: count 2");
        Assert(intList[1] == 20, "List<int>: index 1");

        Console.WriteLine("T17_Generics: " + _pass + " passed, " + _fail + " failed");
    }
}
