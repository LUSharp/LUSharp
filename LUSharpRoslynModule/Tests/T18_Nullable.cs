namespace LUSharpTests;

public static class T18_Nullable
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

    // int? with or without value
    private static int? GetNullableInt(bool hasValue)
    {
        if (hasValue)
            return 42;
        return null;
    }

    // ?? fallback on nullable int
    private static int WithFallback(int? x, int fallback)
    {
        return x ?? fallback;
    }

    // ?? fallback on string
    private static string WithStringFallback(string s, string fallback)
    {
        return s ?? fallback;
    }

    // chained ?? fallback (a ?? b ?? c)
    private static string ChainedFallback(string a, string b, string c)
    {
        return a ?? b ?? c;
    }

    // ?. on object — returns null if null, otherwise .Length
    private static int? SafeLength(string s)
    {
        return s?.Length;
    }

    // Nullable in ternary
    private static string NullableTernary(int? n)
    {
        return n != null ? "has:" + n : "none";
    }

    // Nullable assignment and reassignment
    private static int TestNullableAssignment()
    {
        int? x = null;
        int result = x ?? 0;
        x = 10;
        result = result + (x ?? 0);
        return result;
    }

    // string null checks
    private static bool IsNonNull(string s)
    {
        return s != null;
    }

    private static bool IsNullStr(string s)
    {
        return s == null;
    }

    // Safe first character via null guard
    private static string SafeFirst(string s)
    {
        if (s == null) return null;
        if (s.Length == 0) return null;
        return s.Substring(0, 1);
    }

    // Chained ?. — access nested property if not null
    private static int ChainedSafeLen(string s)
    {
        int? len = s?.Length;
        return len ?? -1;
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T18_Nullable ===");

        // ── int? declaration ──
        int? withVal = GetNullableInt(true);
        int? withoutVal = GetNullableInt(false);
        Assert(withVal != null, "int? with value: not null");
        Assert(withoutVal == null, "int? without value: is null");

        // ── HasValue pattern (check != null) ──
        bool hasValue = withVal != null;
        Assert(hasValue == true, "HasValue: non-null has value");
        bool noValue = withoutVal == null;
        Assert(noValue == true, "no value: null == null");

        // ── Value access via ?? ──
        int intVal = withVal ?? 0;
        Assert(intVal == 42, "int? Value: retrieved via ??");

        // ── ?? on int? ──
        Assert(WithFallback(10, 99) == 10, "?? int: non-null returns value");
        Assert(WithFallback(null, 99) == 99, "?? int: null returns fallback");
        Assert(WithFallback(0, 99) == 0, "?? int: zero is non-null");

        // ── ?? on string ──
        Assert(WithStringFallback("hi", "default") == "hi", "?? string: non-null");
        Assert(WithStringFallback(null, "default") == "default", "?? string: null fallback");
        Assert(WithStringFallback("", "default") == "", "?? string: empty is non-null");

        // ── chained ?? ──
        Assert(ChainedFallback("a", "b", "c") == "a", "?? chain: first non-null");
        Assert(ChainedFallback(null, "b", "c") == "b", "?? chain: second non-null");
        Assert(ChainedFallback(null, null, "c") == "c", "?? chain: third fallback");

        // ── ?. on string ──
        int? len = SafeLength("hello");
        Assert(len != null, "?. non-null: has value");
        Assert((len ?? -1) == 5, "?. non-null: correct length 5");
        int? nullLen = SafeLength(null);
        Assert(nullLen == null, "?. null string: returns null");

        // ── nullable ternary ──
        Assert(NullableTernary(7) == "has:7", "nullable ternary: has value");
        Assert(NullableTernary(null) == "none", "nullable ternary: no value");

        // ── nullable assignment flow ──
        Assert(TestNullableAssignment() == 10, "nullable assignment: 0 + 10 = 10");

        // ── string null checks ──
        Assert(IsNonNull("test") == true, "string != null: true");
        Assert(IsNonNull(null) == false, "string != null: null is false");
        Assert(IsNullStr(null) == true, "string == null: null is true");
        Assert(IsNullStr("x") == false, "string == null: non-null is false");

        // ── nullable int increment ──
        int? counter = 0;
        counter = counter + 1;
        Assert(counter != null, "nullable int increment: not null");
        Assert((counter ?? -1) == 1, "nullable int increment: value is 1");

        // ── chained ?. via safe first char ──
        string first = SafeFirst("abc");
        Assert(first != null, "SafeFirst: non-null for non-empty string");
        Assert(first == "a", "SafeFirst: correct first char");
        string emptyFirst = SafeFirst("");
        Assert(emptyFirst == null, "SafeFirst: null for empty string");
        string nullFirst = SafeFirst(null);
        Assert(nullFirst == null, "SafeFirst: null for null input");

        // ── ?. len chain ──
        Assert(ChainedSafeLen("hello") == 5, "ChainedSafeLen: hello has length 5");
        Assert(ChainedSafeLen(null) == -1, "ChainedSafeLen: null returns -1 fallback");

        Console.WriteLine("T18_Nullable: " + _pass + " passed, " + _fail + " failed");
    }
}
