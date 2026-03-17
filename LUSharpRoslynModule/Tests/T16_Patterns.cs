namespace LUSharpTests;

// Helper class hierarchy for pattern tests
public class PatternAnimal
{
    public string Name { get; set; }
    public PatternAnimal(string name) { Name = name; }
    public virtual string Speak() { return "..."; }
}

public class PatternDog : PatternAnimal
{
    public string Breed { get; set; }
    public PatternDog(string name, string breed) : base(name) { Breed = breed; }
    public override string Speak() { return "Woof"; }
}

public class PatternCat : PatternAnimal
{
    public PatternCat(string name) : base(name) { }
    public override string Speak() { return "Meow"; }
}

public static class T16_Patterns
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

    // is Type check (no variable binding)
    private static bool IsString(object obj)
    {
        return obj is string;
    }

    // is Type with variable binding
    private static string GetStringValue(object obj)
    {
        if (obj is string s)
        {
            return s;
        }
        return "";
    }

    // is not null
    private static bool IsNotNull(object obj)
    {
        return obj is not null;
    }

    // Dispatch based on sequential type checks
    private static string Describe(object obj)
    {
        if (obj is string str)
            return "string:" + str;
        if (obj is int num)
            return "int:" + num;
        if (obj is bool b)
            return "bool:" + b;
        return "other";
    }

    // Derived is Base check
    private static bool IsAnimal(PatternAnimal a)
    {
        return a is PatternAnimal;
    }

    // instanceof check on derived type
    private static bool IsDog(PatternAnimal a)
    {
        return a is PatternDog;
    }

    // Negated pattern (is not Type)
    private static bool IsNotDog(PatternAnimal a)
    {
        return a is not PatternDog;
    }

    // Null check via == null
    private static bool CheckIsNull(object obj)
    {
        return obj == null;
    }

    // Boxed int type check
    private static bool BoxedIntIsInt(object obj)
    {
        return obj is int;
    }

    // Pattern in if condition with variable
    private static int TryGetLength(object obj)
    {
        if (obj is string s)
            return s.Length;
        return -1;
    }

    // Null string guard
    private static string NullOrValue(string s)
    {
        if (s == null) return "null";
        return "value";
    }

    // typeof() usage — emits as the type name string literal
    private static string GetTypeLiteral()
    {
        return typeof(string);
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T16_Patterns ===");

        // ── is Type ──
        Assert(IsString("hello") == true, "is string: string literal");
        Assert(IsString(42) == false, "is string: int is not string");
        Assert(IsString("") == true, "is string: empty string");

        // ── is Type with variable binding ──
        Assert(GetStringValue("world") == "world", "is string s: extracts value");
        Assert(GetStringValue(99) == "", "is string s: non-string returns empty");

        // ── is not null ──
        Assert(IsNotNull("test") == true, "is not null: string");
        Assert(IsNotNull(42) == true, "is not null: int");

        // ── == null ──
        Assert(CheckIsNull(null) == true, "== null: null is null");
        Assert(CheckIsNull("x") == false, "== null: non-null is not null");

        // ── sequential type dispatch ──
        Assert(Describe("abc") == "string:abc", "Describe: string case");
        Assert(Describe(7) == "int:7", "Describe: int case");
        Assert(Describe(3.14) == "other", "Describe: double falls to other");

        // ── inheritance is checks ──
        PatternDog dog = new PatternDog("Rex", "Lab");
        PatternCat cat = new PatternCat("Whiskers");
        Assert(IsAnimal(dog) == true, "Dog is PatternAnimal: true");
        Assert(IsAnimal(cat) == true, "Cat is PatternAnimal: true");
        Assert(IsDog(dog) == true, "IsDog: Dog returns true");
        Assert(IsDog(cat) == false, "IsDog: Cat returns false");

        // ── negated pattern ──
        Assert(IsNotDog(cat) == true, "is not Dog: Cat is not Dog");
        Assert(IsNotDog(dog) == false, "is not Dog: Dog is Dog");

        // ── boxed value type check ──
        object boxed = 5;
        Assert(BoxedIntIsInt(boxed) == true, "boxed int is int: true");
        Assert(BoxedIntIsInt("not an int") == false, "string is not int: false");

        // ── pattern with variable — TryGetLength ──
        Assert(TryGetLength("hello") == 5, "TryGetLength: string length 5");
        Assert(TryGetLength("") == 0, "TryGetLength: empty string length 0");
        Assert(TryGetLength(42) == -1, "TryGetLength: non-string returns -1");

        // ── null guard ──
        Assert(NullOrValue(null) == "null", "null guard: null");
        Assert(NullOrValue("hi") == "value", "null guard: non-null");

        // ── typeof() ──
        string typeLiteral = GetTypeLiteral();
        Assert(typeLiteral == "string", "typeof(string): emits as \"string\"");

        Console.WriteLine("T16_Patterns: " + _pass + " passed, " + _fail + " failed");
    }
}
