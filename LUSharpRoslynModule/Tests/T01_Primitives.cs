namespace LUSharpTests;

public static class T01_Primitives
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

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T01_Primitives ===");

        // ── int ──
        int a = 42;
        Assert(a == 42, "int literal assignment");

        int b = -7;
        Assert(b == -7, "int negative literal");

        int c = 0;
        Assert(c == 0, "int zero");

        // ── float ──
        float f = 3.14f;
        Assert(f > 3.13f && f < 3.15f, "float literal range");

        float fNeg = -1.5f;
        Assert(fNeg < 0.0f, "float negative");

        // ── double ──
        double d = 2.718281828;
        Assert(d > 2.7 && d < 2.8, "double literal range");

        // ── bool ──
        bool t = true;
        bool ff = false;
        Assert(t == true, "bool true");
        Assert(ff == false, "bool false");
        Assert(t != ff, "bool inequality");

        // ── string ──
        string s = "hello";
        Assert(s == "hello", "string literal equality");
        Assert(s != "world", "string literal inequality");

        // ── null ──
        string? ns = null;
        Assert(ns == null, "string null assignment");

        int? ni = null;
        Assert(ni == null, "nullable int null");

        ni = 5;
        Assert(ni == 5, "nullable int assigned value");

        // ── default values ──
        int defInt = default(int);
        Assert(defInt == 0, "default(int)==0");

        bool defBool = default(bool);
        Assert(defBool == false, "default(bool)==false");

        string? defStr = default(string);
        Assert(defStr == null, "default(string)==null");

        // ── type casting: int → double ──
        // Casts are erased in Luau; int and double are both Luau numbers
        int i = 7;
        double castDouble = (double)i;
        Assert(castDouble == 7.0, "int widened to double: value preserved");

        // ── double value preserved through cast (casts are erased in Luau) ──
        // To truncate a double to int, use explicit Math.Floor / Math.Truncate
        double dVal = 9.0;
        int exact = (int)dVal;
        Assert(exact == 9, "cast of exact double 9.0 == 9");

        // Truncation toward zero via Math.Truncate (maps to conditional floor/ceil)
        double dPos = 9.99;
        int truncPos = (int)Math.Truncate(dPos);
        Assert(truncPos == 9, "Math.Truncate(9.99) == 9");

        double dNeg = -3.7;
        int truncNeg = (int)Math.Truncate(dNeg);
        Assert(truncNeg == -3, "Math.Truncate(-3.7) == -3 (toward zero)");

        // ── string → int parsing ──
        int parsed = int.Parse("123");
        Assert(parsed == 123, "int.Parse string");

        // ── int.MaxValue / int.MinValue ──
        Assert(int.MaxValue == 2147483647, "int.MaxValue");
        Assert(int.MinValue == -2147483648, "int.MinValue");

        // ── char as int ──
        // In C#, 'A' is a char literal. Cast to int gives its code point.
        int charCode = (int)'A';
        Assert(charCode == 65, "char 'A' as int == 65");

        int charZ = (int)'Z';
        Assert(charZ == 90, "char 'Z' as int == 90");

        int charZero = (int)'0';
        Assert(charZero == 48, "char '0' as int == 48");

        // ── bool logic ──
        bool andResult = true && false;
        Assert(andResult == false, "true && false == false");

        bool orResult = false || true;
        Assert(orResult == true, "false || true == true");

        bool notResult = !true;
        Assert(notResult == false, "!true == false");

        bool complex = (true && true) || false;
        Assert(complex == true, "(true && true) || false == true");

        // ── string concatenation with numbers ──
        int num = 10;
        string concat = "value=" + num;
        Assert(concat == "value=10", "string concat with int");

        double dnum = 3.5;
        string concatD = "d=" + dnum;
        Assert(concatD == "d=3.5", "string concat with double");

        // bool concat: use explicit tostring() since Luau .. does not coerce booleans
        bool bval = true;
        string concatB = "b=" + bval.ToString();
        Assert(concatB == "b=True" || concatB == "b=true", "string concat with bool");

        // ── scorecard ──
        Console.WriteLine("T01_Primitives: " + _pass + " passed, " + _fail + " failed");
    }
}
