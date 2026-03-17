using System;

namespace LUSharpTests;

public static class T19_Math
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

    // Epsilon comparison for floating-point
    private static bool Near(double a, double b)
    {
        double diff = a - b;
        if (diff < 0) diff = 0 - diff;
        return diff < 0.0001;
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T19_Math ===");

        // ── Math.Max ──
        Assert(Math.Max(3, 7) == 7, "Math.Max: larger wins");
        Assert(Math.Max(-5, -2) == -2, "Math.Max: negative values");
        Assert(Math.Max(4, 4) == 4, "Math.Max: equal values");

        // ── Math.Min ──
        Assert(Math.Min(3, 7) == 3, "Math.Min: smaller wins");
        Assert(Math.Min(-5, -2) == -5, "Math.Min: negative values");
        Assert(Math.Min(4, 4) == 4, "Math.Min: equal values");

        // ── Math.Abs ──
        Assert(Math.Abs(-7) == 7, "Math.Abs: negative");
        Assert(Math.Abs(7) == 7, "Math.Abs: positive");
        Assert(Math.Abs(0) == 0, "Math.Abs: zero");

        // ── Math.Floor ──
        Assert(Math.Floor(3.7) == 3, "Math.Floor: positive");
        Assert(Math.Floor(-3.2) == -4, "Math.Floor: negative rounds down");
        Assert(Math.Floor(4.0) == 4, "Math.Floor: whole number");

        // ── Math.Ceiling ──
        Assert(Math.Ceiling(3.2) == 4, "Math.Ceiling: positive");
        Assert(Math.Ceiling(-3.7) == -3, "Math.Ceiling: negative rounds up");
        Assert(Math.Ceiling(4.0) == 4, "Math.Ceiling: whole number");

        // ── Math.Round ──
        Assert(Math.Round(3.5) == 4, "Math.Round: 3.5 rounds up");
        Assert(Math.Round(2.4) == 2, "Math.Round: 2.4 rounds down");
        Assert(Math.Round(2.0) == 2, "Math.Round: whole number");

        // ── Math.Sqrt ──
        Assert(Near(Math.Sqrt(9), 3), "Math.Sqrt: sqrt(9) = 3");
        Assert(Near(Math.Sqrt(2), 1.41421), "Math.Sqrt: sqrt(2) approx");
        Assert(Near(Math.Sqrt(0), 0), "Math.Sqrt: sqrt(0) = 0");

        // ── Math.Pow ──
        Assert(Near(Math.Pow(2, 10), 1024), "Math.Pow: 2^10 = 1024");
        Assert(Near(Math.Pow(3, 3), 27), "Math.Pow: 3^3 = 27");
        Assert(Near(Math.Pow(5, 0), 1), "Math.Pow: x^0 = 1");
        Assert(Near(Math.Pow(4, 0.5), 2), "Math.Pow: 4^0.5 = 2");

        // ── Math.Log (natural) ──
        Assert(Near(Math.Log(1), 0), "Math.Log: log(1) = 0");
        Assert(Near(Math.Log(Math.E), 1), "Math.Log: log(e) = 1");

        // ── Math.Log (with base) ──
        Assert(Near(Math.Log(8, 2), 3), "Math.Log(8,2) = 3");
        Assert(Near(Math.Log(1000, 10), 3), "Math.Log(1000,10) = 3");

        // ── Math.Log10 ──
        Assert(Near(Math.Log10(100), 2), "Math.Log10: 100 = 2");
        Assert(Near(Math.Log10(1), 0), "Math.Log10: 1 = 0");
        Assert(Near(Math.Log10(1000), 3), "Math.Log10: 1000 = 3");

        // ── Math.Sin ──
        Assert(Near(Math.Sin(0), 0), "Math.Sin: sin(0) = 0");
        Assert(Near(Math.Sin(Math.PI / 2), 1), "Math.Sin: sin(PI/2) = 1");

        // ── Math.Cos ──
        Assert(Near(Math.Cos(0), 1), "Math.Cos: cos(0) = 1");
        Assert(Near(Math.Cos(Math.PI), -1), "Math.Cos: cos(PI) = -1");

        // ── Math.PI and Math.E ──
        Assert(Near(Math.PI, 3.14159), "Math.PI: approximately 3.14159");
        Assert(Near(Math.E, 2.71828), "Math.E: approximately 2.71828");

        // ── Math.Sign ──
        Assert(Math.Sign(5) == 1, "Math.Sign: positive");
        Assert(Math.Sign(-3) == -1, "Math.Sign: negative");
        Assert(Math.Sign(0) == 0, "Math.Sign: zero");

        // ── Math.Clamp ──
        Assert(Math.Clamp(5, 0, 10) == 5, "Math.Clamp: within range");
        Assert(Math.Clamp(-5, 0, 10) == 0, "Math.Clamp: below min");
        Assert(Math.Clamp(15, 0, 10) == 10, "Math.Clamp: above max");
        Assert(Math.Clamp(0, 0, 10) == 0, "Math.Clamp: at min boundary");
        Assert(Math.Clamp(10, 0, 10) == 10, "Math.Clamp: at max boundary");

        // ── Math.Truncate ──
        Assert(Math.Truncate(3.9) == 3, "Math.Truncate: positive toward zero");
        Assert(Math.Truncate(-3.9) == -3, "Math.Truncate: negative toward zero");
        Assert(Math.Truncate(0.5) == 0, "Math.Truncate: 0.5 becomes 0");

        // ── Convert.ToInt32 ──
        Assert(Convert.ToInt32(3.7) == 3, "Convert.ToInt32: truncates double");
        Assert(Convert.ToInt32("42") == 42, "Convert.ToInt32: from string");
        Assert(Convert.ToInt32(-2.9) == -2, "Convert.ToInt32: negative truncates toward zero");

        // ── Convert.ToString ──
        Assert(Convert.ToString(123) == "123", "Convert.ToString: int");
        Assert(Convert.ToString(true) == "true", "Convert.ToString: bool");

        // ── Convert.ToDouble ──
        double d1 = Convert.ToDouble("3.14");
        Assert(Near(d1, 3.14), "Convert.ToDouble: from string");
        double d2 = Convert.ToDouble(5);
        Assert(Near(d2, 5.0), "Convert.ToDouble: from int");

        // ── Convert.ToBoolean ──
        Assert(Convert.ToBoolean(1) == true, "Convert.ToBoolean: 1 = true");
        Assert(Convert.ToBoolean(0) == false, "Convert.ToBoolean: 0 = false");

        // ── int.Parse ──
        int p1 = int.Parse("99");
        Assert(p1 == 99, "int.Parse: valid string");
        int p2 = int.Parse("-7");
        Assert(p2 == -7, "int.Parse: negative string");

        // ── int.TryParse success ──
        int outVal = 0;
        bool ok = int.TryParse("123", out outVal);
        Assert(ok == true, "int.TryParse: success returns true");
        Assert(outVal == 123, "int.TryParse: success out value");

        // ── int.TryParse failure ──
        int badVal = 0;
        bool notOk = int.TryParse("abc", out badVal);
        Assert(notOk == false, "int.TryParse: failure returns false");

        // ── double.Parse ──
        double dp = double.Parse("2.5");
        Assert(Near(dp, 2.5), "double.Parse: valid string");

        // ── double.TryParse success ──
        double dOut = 0;
        bool dOk = double.TryParse("1.5", out dOut);
        Assert(dOk == true, "double.TryParse: success returns true");
        Assert(Near(dOut, 1.5), "double.TryParse: success out value");

        // ── double.TryParse failure ──
        double dBad = 0;
        bool dNotOk = double.TryParse("nope", out dBad);
        Assert(dNotOk == false, "double.TryParse: failure returns false");

        // ── double.IsNaN ──
        double nan = double.NaN;
        Assert(double.IsNaN(nan) == true, "double.IsNaN: NaN is NaN");
        Assert(double.IsNaN(1.0) == false, "double.IsNaN: 1.0 is not NaN");
        // self-check: NaN != NaN
        Assert((nan == nan) == false, "NaN != NaN: true");

        // ── double.IsInfinity ──
        double posInf = double.PositiveInfinity;
        double negInf = double.NegativeInfinity;
        Assert(double.IsInfinity(posInf) == true, "double.IsInfinity: +Inf");
        Assert(double.IsInfinity(negInf) == true, "double.IsInfinity: -Inf");
        Assert(double.IsInfinity(1.0) == false, "double.IsInfinity: 1.0 is not infinite");

        // ── int.MaxValue and int.MinValue ──
        Assert(int.MaxValue > 2000000000, "int.MaxValue: > 2 billion");
        Assert(int.MinValue < -2000000000, "int.MinValue: < -2 billion");
        Assert(int.MaxValue > int.MinValue, "int.MaxValue > int.MinValue");

        Console.WriteLine("T19_Math: " + _pass + " passed, " + _fail + " failed");
    }
}
