namespace LUSharpTests;

public static class T05_Classes
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

    // ── Simple class with fields and constructor ──
    private class Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    // ── Class with multiple constructors ──
    private class Box
    {
        public int Width;
        public int Height;
        public string Label;

        public Box(int width, int height)
        {
            Width = width;
            Height = height;
            Label = "untitled";
        }

        public Box(int width, int height, string label)
        {
            Width = width;
            Height = height;
            Label = label;
        }

        public int Area()
        {
            return Width * Height;
        }

        public string Describe()
        {
            return Label + ": " + Width + "x" + Height;
        }
    }

    // ── Class with static fields and methods ──
    private class Counter
    {
        public static int TotalCreated;
        private int _value;

        public Counter(int start)
        {
            _value = start;
            Counter.TotalCreated = Counter.TotalCreated + 1;
        }

        public void Increment()
        {
            _value = _value + 1;
        }

        public int GetValue()
        {
            return _value;
        }

        public static int GetTotalCreated()
        {
            return Counter.TotalCreated;
        }
    }

    // ── Class with auto properties and read-only property ──
    private class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }

        private string _id;
        public string Id { get { return _id; } }

        public Person(string name, int age, string id)
        {
            Name = name;
            Age = age;
            _id = id;
        }

        public string Greeting()
        {
            return "Hi, I am " + Name;
        }
    }

    // ── Expression-bodied property and method ──
    private class Circle
    {
        public double Radius;

        public Circle(double radius)
        {
            Radius = radius;
        }

        public double Diameter => Radius * 2.0;

        public double Area() => 3.14159265 * Radius * Radius;
    }

    // ── Class with const field ──
    private class MathConst
    {
        public const int MaxItems = 100;
        public const string DefaultName = "unnamed";

        public bool IsValidCount(int n)
        {
            return n >= 0 && n <= MathConst.MaxItems;
        }
    }

    // ── Class with field initializers ──
    private class Defaults
    {
        public int Count = 0;
        public string Status = "ready";
        public bool Active = true;

        public Defaults()
        {
        }

        public void Reset()
        {
            Count = 0;
            Status = "ready";
            Active = true;
        }
    }

    // ── Class with ToString override ──
    private class Tag
    {
        public string Key;
        public string Value;

        public Tag(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string ToString2()
        {
            return Key + "=" + Value;
        }
    }

    // ── Class with method using this keyword ──
    private class Builder
    {
        private string _buffer;

        public Builder()
        {
            _buffer = "";
        }

        public Builder Append(string text)
        {
            _buffer = _buffer + text;
            return this;
        }

        public string Build()
        {
            return _buffer;
        }
    }

    // ── Class with multiple parameters and return ──
    private class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public int Multiply(int a, int b, int c)
        {
            return a * b * c;
        }

        public static int Square(int n)
        {
            return n * n;
        }
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T05_Classes ===");

        // ── Class instantiation and field access ──
        Point p = new Point(3, 4);
        Assert(p.X == 3, "Point.X == 3");
        Assert(p.Y == 4, "Point.Y == 4");

        // ── Multiple constructors (overloads) ──
        Box b1 = new Box(10, 5);
        Assert(b1.Width == 10, "Box 2-arg ctor: Width == 10");
        Assert(b1.Height == 5, "Box 2-arg ctor: Height == 5");
        Assert(b1.Label == "untitled", "Box 2-arg ctor: default Label");

        Box b2 = new Box(8, 3, "mybox");
        Assert(b2.Width == 8, "Box 3-arg ctor: Width == 8");
        Assert(b2.Label == "mybox", "Box 3-arg ctor: Label == 'mybox'");

        // ── Instance methods ──
        Assert(b1.Area() == 50, "Box.Area() == 10*5 == 50");
        Assert(b2.Area() == 24, "Box.Area() == 8*3 == 24");

        string desc = b2.Describe();
        Assert(desc == "mybox: 8x3", "Box.Describe() format");

        // ── Static fields and methods ──
        Counter.TotalCreated = 0;
        Counter c1 = new Counter(0);
        Counter c2 = new Counter(10);
        Assert(Counter.TotalCreated == 2, "static field TotalCreated == 2 after 2 instances");
        Assert(Counter.GetTotalCreated() == 2, "static method returns TotalCreated == 2");

        c1.Increment();
        c1.Increment();
        c1.Increment();
        Assert(c1.GetValue() == 3, "Counter after 3 increments == 3");
        Assert(c2.GetValue() == 10, "Second Counter still == 10");

        // ── Auto-properties (get/set) ──
        Person per = new Person("Alice", 30, "A001");
        Assert(per.Name == "Alice", "Person.Name == 'Alice'");
        Assert(per.Age == 30, "Person.Age == 30");

        per.Name = "Bob";
        Assert(per.Name == "Bob", "Person.Name updated to 'Bob'");

        per.Age = 25;
        Assert(per.Age == 25, "Person.Age updated to 25");

        // ── Read-only property (get only) ──
        Assert(per.Id == "A001", "Person.Id read-only == 'A001'");

        // ── Instance method using field ──
        Assert(per.Greeting() == "Hi, I am Bob", "Person.Greeting() uses updated Name");

        // ── Expression-bodied property ──
        Circle circ = new Circle(5.0);
        Assert(circ.Diameter == 10.0, "Circle.Diameter expression-bodied == 10.0");
        Assert(circ.Radius == 5.0, "Circle.Radius field == 5.0");

        // ── Expression-bodied method ──
        double area = circ.Area();
        Assert(area > 78.5 && area < 78.6, "Circle.Area() expression-bodied in range");

        // ── Const fields ──
        MathConst mc = new MathConst();
        Assert(MathConst.MaxItems == 100, "const MaxItems == 100");
        Assert(MathConst.DefaultName == "unnamed", "const DefaultName == 'unnamed'");
        Assert(mc.IsValidCount(50), "IsValidCount(50) == true");
        Assert(mc.IsValidCount(100), "IsValidCount(100) == true (boundary)");
        Assert(!mc.IsValidCount(101), "IsValidCount(101) == false (over boundary)");
        Assert(!mc.IsValidCount(-1), "IsValidCount(-1) == false (negative)");

        // ── Field initializers ──
        Defaults def = new Defaults();
        Assert(def.Count == 0, "field initializer Count == 0");
        Assert(def.Status == "ready", "field initializer Status == 'ready'");
        Assert(def.Active == true, "field initializer Active == true");

        def.Count = 5;
        def.Status = "busy";
        def.Active = false;
        def.Reset();
        Assert(def.Count == 0, "Reset restores Count to 0");
        Assert(def.Status == "ready", "Reset restores Status to 'ready'");
        Assert(def.Active == true, "Reset restores Active to true");

        // ── this keyword ──
        Builder bldr = new Builder();
        string built = bldr.Append("hello").Append(" ").Append("world").Build();
        Assert(built == "hello world", "Builder chained via this keyword");

        // ── Method with multiple params ──
        Calculator calc = new Calculator();
        Assert(calc.Add(3, 7) == 10, "Calculator.Add(3,7) == 10");
        Assert(calc.Multiply(2, 3, 4) == 24, "Calculator.Multiply(2,3,4) == 24");

        // ── Static method ──
        Assert(Calculator.Square(6) == 36, "Calculator.Square(6) == 36");
        Assert(Calculator.Square(0) == 0, "Calculator.Square(0) == 0");

        // ── Multiple instances are independent ──
        Point p1 = new Point(1, 2);
        Point p2 = new Point(10, 20);
        Assert(p1.X == 1, "p1.X independent == 1");
        Assert(p2.X == 10, "p2.X independent == 10");

        p1.X = 99;
        Assert(p1.X == 99, "p1.X mutated to 99");
        Assert(p2.X == 10, "p2.X unchanged after p1 mutation");

        // ── Descriptive method (ToString-like) ──
        Tag tag = new Tag("version", "1.0");
        Assert(tag.ToString2() == "version=1.0", "Tag.ToString2() format");

        Tag tag2 = new Tag("env", "prod");
        Assert(tag2.ToString2() == "env=prod", "Tag2.ToString2() format");

        // ── scorecard ──
        Console.WriteLine("T05_Classes: " + _pass + " passed, " + _fail + " failed");
    }
}
