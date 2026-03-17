namespace LUSharpTests;

public static class T02_Arithmetic
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
        Console.WriteLine("=== T02_Arithmetic ===");

        // ── Addition ──
        Assert(3 + 4 == 7, "3 + 4 == 7");
        Assert(0 + 0 == 0, "0 + 0 == 0");
        Assert(-5 + 5 == 0, "-5 + 5 == 0");

        // ── Subtraction ──
        Assert(10 - 3 == 7, "10 - 3 == 7");
        Assert(0 - 1 == -1, "0 - 1 == -1");

        // ── Multiplication ──
        Assert(6 * 7 == 42, "6 * 7 == 42");
        Assert(-3 * 4 == -12, "-3 * 4 == -12");
        Assert(0 * 999 == 0, "0 * 999 == 0");

        // ── Integer division (int/int → math.floor in Luau) ──
        Assert(7 / 2 == 3, "7 / 2 == 3 (integer division)");
        Assert(10 / 3 == 3, "10 / 3 == 3 (integer division)");
        Assert(6 / 2 == 3, "6 / 2 == 3 (exact)");
        Assert(1 / 4 == 0, "1 / 4 == 0 (integer division rounds down)");
        Assert(9 / 3 == 3, "9 / 3 == 3 (exact)");
        // Note: -7/2 in Luau via math.floor gives -4, not -3 (C# truncates toward zero)
        // Test Luau's actual behavior:
        Assert(-7 / 2 == -4, "-7 / 2 == -4 via math.floor (Luau semantics)");

        // ── Float division ──
        double q = 7.0 / 2;
        Assert(q == 3.5, "7.0 / 2 == 3.5 (float division)");

        double q2 = 1.0 / 3.0;
        Assert(q2 > 0.333 && q2 < 0.334, "1.0/3.0 in range (0.333, 0.334)");

        // ── Modulo ──
        Assert(7 % 3 == 1, "7 % 3 == 1");
        Assert(10 % 5 == 0, "10 % 5 == 0");
        Assert(9 % 2 == 1, "9 % 2 == 1");
        Assert(17 % 4 == 1, "17 % 4 == 1");
        // Note: -7 % 3 in Luau gives 2 (math modulo), while C# gives -1 (truncation-based)
        // Test Luau's actual behavior:
        Assert(-7 % 3 == 2, "-7 % 3 == 2 via Luau math modulo");

        // ── Unary minus ──
        int pos = 5;
        int neg = -pos;
        Assert(neg == -5, "unary minus variable");

        int x = -(-3);
        Assert(x == 3, "double unary minus");

        // ── Pre-increment ──
        int pre = 5;
        pre++;
        Assert(pre == 6, "pre-increment (statement form)");

        int preB = 0;
        ++preB;
        Assert(preB == 1, "++x statement form");

        // ── Post-increment ──
        int post = 10;
        post++;
        Assert(post == 11, "post-increment (statement form)");

        // ── Pre-decrement ──
        int preDec = 8;
        preDec--;
        Assert(preDec == 7, "pre-decrement (statement form)");

        int preDecB = 3;
        --preDecB;
        Assert(preDecB == 2, "--x statement form");

        // ── Post-decrement ──
        int postDec = 20;
        postDec--;
        Assert(postDec == 19, "post-decrement (statement form)");

        // ── Compound assignment ──
        int ca = 10;
        ca += 5;
        Assert(ca == 15, "+= compound assignment");

        int cs = 10;
        cs -= 3;
        Assert(cs == 7, "-= compound assignment");

        int cm = 4;
        cm *= 3;
        Assert(cm == 12, "*= compound assignment");

        int cd = 15;
        cd /= 3;
        Assert(cd == 5, "/= compound assignment (exact)");

        int cmod = 17;
        cmod %= 5;
        Assert(cmod == 2, "%= compound assignment");

        // ── Operator precedence ──
        int prec = 2 + 3 * 4;
        Assert(prec == 14, "2 + 3 * 4 == 14 (precedence)");

        int prec2 = 10 - 2 * 3 + 1;
        Assert(prec2 == 5, "10 - 2*3 + 1 == 5");

        // ── Parentheses override ──
        int paren = (2 + 3) * 4;
        Assert(paren == 20, "(2 + 3) * 4 == 20");

        int paren2 = 10 / (2 + 3);
        Assert(paren2 == 2, "10 / (2 + 3) == 2");

        // ── Math.Floor on division ──
        double floored = Math.Floor(7.9);
        Assert(floored == 7.0, "Math.Floor(7.9) == 7.0");

        double floorNeg = Math.Floor(-1.1);
        Assert(floorNeg == -2.0, "Math.Floor(-1.1) == -2.0");

        // ── Math helpers ──
        Assert(Math.Abs(-42) == 42, "Math.Abs(-42) == 42");
        Assert(Math.Max(3, 7) == 7, "Math.Max(3, 7) == 7");
        Assert(Math.Min(3, 7) == 3, "Math.Min(3, 7) == 3");

        double sq = Math.Sqrt(9.0);
        Assert(sq == 3.0, "Math.Sqrt(9) == 3");

        // ── Large numbers ──
        // Use values that fit in int32 to avoid C# overflow
        int big = 10000;
        int bigSq = big * big;
        Assert(bigSq == 100000000, "10000 * 10000 == 100000000");

        // ── scorecard ──
        Console.WriteLine("T02_Arithmetic: " + _pass + " passed, " + _fail + " failed");
    }
}
