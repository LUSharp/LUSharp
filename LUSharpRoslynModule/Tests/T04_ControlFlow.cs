namespace LUSharpTests;

public static class T04_ControlFlow
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

    // Helper for early-return test
    private static int EarlyReturn(int n)
    {
        if (n < 0)
        {
            return -1;
        }
        if (n == 0)
        {
            return 0;
        }
        return 1;
    }

    // Helper to test null-coalescing
    private static string NullCoalesce(string? s)
    {
        string result = s ?? "default";
        return result;
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T04_ControlFlow ===");

        // ── if / else ──
        int x = 10;
        int ifResult = 0;
        if (x > 5)
        {
            ifResult = 1;
        }
        else
        {
            ifResult = -1;
        }
        Assert(ifResult == 1, "if: x > 5 takes true branch");

        int ifElse = 0;
        if (x < 0)
        {
            ifElse = -1;
        }
        else
        {
            ifElse = 99;
        }
        Assert(ifElse == 99, "if/else: false branch taken");

        // ── if / else if / else chain ──
        int score = 75;
        string grade = "";
        if (score >= 90)
        {
            grade = "A";
        }
        else if (score >= 80)
        {
            grade = "B";
        }
        else if (score >= 70)
        {
            grade = "C";
        }
        else
        {
            grade = "F";
        }
        Assert(grade == "C", "if/elseif/else chain selects C");

        // ── nested if ──
        int a = 3;
        int b = 7;
        string nested = "";
        if (a > 0)
        {
            if (b > 5)
            {
                nested = "both";
            }
            else
            {
                nested = "only a";
            }
        }
        else
        {
            nested = "neither";
        }
        Assert(nested == "both", "nested if both conditions true");

        // ── switch on int ──
        int day = 3;
        string dayName = "";
        switch (day)
        {
            case 1:
                dayName = "Monday";
                break;
            case 2:
                dayName = "Tuesday";
                break;
            case 3:
                dayName = "Wednesday";
                break;
            case 4:
                dayName = "Thursday";
                break;
            default:
                dayName = "Other";
                break;
        }
        Assert(dayName == "Wednesday", "switch int case 3 -> Wednesday");

        // ── switch on string ──
        string color = "blue";
        int colorCode = 0;
        switch (color)
        {
            case "red":
                colorCode = 1;
                break;
            case "green":
                colorCode = 2;
                break;
            case "blue":
                colorCode = 3;
                break;
            default:
                colorCode = -1;
                break;
        }
        Assert(colorCode == 3, "switch string 'blue' -> 3");

        // ── switch with default ──
        int unknown = 999;
        string unknownResult = "";
        switch (unknown)
        {
            case 1:
                unknownResult = "one";
                break;
            case 2:
                unknownResult = "two";
                break;
            default:
                unknownResult = "many";
                break;
        }
        Assert(unknownResult == "many", "switch default taken for unknown value");

        // ── switch fall-through: multiple case labels on same body ──
        int season = 2;
        string seasonName = "";
        switch (season)
        {
            case 1:
            case 2:
            case 3:
                seasonName = "Q1";
                break;
            case 4:
            case 5:
            case 6:
                seasonName = "Q2";
                break;
            default:
                seasonName = "Other";
                break;
        }
        Assert(seasonName == "Q1", "switch fall-through: season 2 -> Q1");

        int season7 = 5;
        string season7Name = "";
        switch (season7)
        {
            case 1:
            case 2:
            case 3:
                season7Name = "Q1";
                break;
            case 4:
            case 5:
            case 6:
                season7Name = "Q2";
                break;
            default:
                season7Name = "Other";
                break;
        }
        Assert(season7Name == "Q2", "switch fall-through: season 5 -> Q2");

        // ── for loop (0 to 9) ──
        int forSum = 0;
        for (int i = 0; i < 10; i++)
        {
            forSum = forSum + i;
        }
        Assert(forSum == 45, "for loop 0..9 sum == 45");

        // ── for loop reverse ──
        int reverseSum = 0;
        for (int i = 10; i > 0; i--)
        {
            reverseSum = reverseSum + i;
        }
        Assert(reverseSum == 55, "for loop 10 down to 1 sum == 55");

        // ── foreach over array ──
        int[] arr = new int[] { 1, 2, 3, 4, 5 };
        int arrSum = 0;
        foreach (int v in arr)
        {
            arrSum = arrSum + v;
        }
        Assert(arrSum == 15, "foreach over int[] sum == 15");

        // ── foreach over List ──
        List<int> lst = new List<int>();
        lst.Add(10);
        lst.Add(20);
        lst.Add(30);
        int lstSum = 0;
        foreach (int v in lst)
        {
            lstSum = lstSum + v;
        }
        Assert(lstSum == 60, "foreach over List<int> sum == 60");

        // ── while loop ──
        int wCount = 0;
        int w = 0;
        while (w < 5)
        {
            wCount = wCount + 1;
            w = w + 1;
        }
        Assert(wCount == 5, "while loop runs 5 times");
        Assert(w == 5, "while loop final w == 5");

        // ── do-while loop ──
        int dw = 0;
        int dwCount = 0;
        do
        {
            dwCount = dwCount + 1;
            dw = dw + 1;
        } while (dw < 3);
        Assert(dwCount == 3, "do-while runs 3 times");

        // do-while runs at least once even if condition is false initially
        int dwOnce = 0;
        do
        {
            dwOnce = dwOnce + 1;
        } while (false);
        Assert(dwOnce == 1, "do-while runs at least once");

        // ── break in loop ──
        int breakAt = 0;
        for (int i = 0; i < 100; i++)
        {
            if (i == 5)
            {
                break;
            }
            breakAt = i;
        }
        Assert(breakAt == 4, "break exits loop: last value before break is 4");

        // ── continue in loop ──
        int skipOdd = 0;
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 != 0)
            {
                continue;
            }
            skipOdd = skipOdd + i;
        }
        // Even numbers 0+2+4+6+8 = 20
        Assert(skipOdd == 20, "continue skips odd numbers: sum of evens 0-8 == 20");

        // ── nested loops with break (inner only) ──
        int outerCount = 0;
        int innerCount = 0;
        for (int i = 0; i < 3; i++)
        {
            outerCount = outerCount + 1;
            for (int j = 0; j < 10; j++)
            {
                if (j == 2)
                {
                    break;
                }
                innerCount = innerCount + 1;
            }
        }
        Assert(outerCount == 3, "nested loop: outer runs 3 times");
        Assert(innerCount == 6, "nested loop: inner break at j==2, 2 per outer => 6 total");

        // ── nested loops with continue (inner only) ──
        int innerSkipped = 0;
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (j == 3)
                {
                    continue;
                }
                innerSkipped = innerSkipped + 1;
            }
        }
        // 2 outer * (5 - 1 skip) = 8
        Assert(innerSkipped == 8, "nested loop continue: 2 * 4 == 8");

        // ── early return ──
        Assert(EarlyReturn(-5) == -1, "early return for negative input");
        Assert(EarlyReturn(0) == 0, "early return for zero");
        Assert(EarlyReturn(7) == 1, "early return for positive");

        // ── ternary operator ──
        int tv = 10;
        string ternResult = tv > 5 ? "big" : "small";
        Assert(ternResult == "big", "ternary true branch");

        int tv2 = 2;
        string ternResult2 = tv2 > 5 ? "big" : "small";
        Assert(ternResult2 == "small", "ternary false branch");

        int ternNum = tv > 0 ? tv * 2 : 0;
        Assert(ternNum == 20, "ternary with expression result");

        // ── null-coalescing (??) ──
        Assert(NullCoalesce(null) == "default", "?? returns default when null");
        Assert(NullCoalesce("hello") == "hello", "?? returns value when not null");

        string? maybeNull = null;
        string coalesced = maybeNull ?? "fallback";
        Assert(coalesced == "fallback", "?? inline: null coalesces to fallback");

        string? notNull = "real";
        string coalesced2 = notNull ?? "fallback";
        Assert(coalesced2 == "real", "?? inline: non-null keeps original");

        int? maybeInt = null;
        int intCoalesced = maybeInt ?? -1;
        Assert(intCoalesced == -1, "?? with nullable int coalesces to -1");

        int? definiteInt = 42;
        int intCoalesced2 = definiteInt ?? -1;
        Assert(intCoalesced2 == 42, "?? with nullable int returns value");

        // ── scorecard ──
        Console.WriteLine("T04_ControlFlow: " + _pass + " passed, " + _fail + " failed");
    }
}
