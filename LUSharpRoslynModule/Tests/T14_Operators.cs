using System;

namespace LUSharpTests;

public static class T14_Operators
{
    private static int _pass = 0;
    private static int _fail = 0;

    // Short-circuit side-effect tracking
    private static int SideEffectCounter = 0;

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

    private static bool SideEffect(bool val)
    {
        SideEffectCounter = SideEffectCounter + 1;
        return val;
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T14_Operators ===");

        // --- Comparison: == != < > <= >= on numbers ---
        Assert(3 == 3, "== on equal numbers");
        Assert(!(3 == 4), "== on unequal numbers is false");
        Assert(3 != 4, "!= on different numbers");
        Assert(!(3 != 3), "!= on equal numbers is false");
        Assert(2 < 5, "< less than");
        Assert(!(5 < 2), "< not less than");
        Assert(5 > 2, "> greater than");
        Assert(!(2 > 5), "> not greater than");
        Assert(3 <= 3, "<= equal case");
        Assert(2 <= 3, "<= less case");
        Assert(!(4 <= 3), "<= fails when greater");
        Assert(3 >= 3, ">= equal case");
        Assert(4 >= 3, ">= greater case");
        Assert(!(2 >= 3), ">= fails when less");

        // --- Comparison: == != on strings ---
        Assert("hello" == "hello", "== on equal strings");
        Assert(!("hello" == "world"), "== on unequal strings is false");
        Assert("hello" != "world", "!= on different strings");
        Assert(!("abc" != "abc"), "!= on equal strings is false");

        // --- Comparison: == on null ---
        string nullStr = null;
        Assert(nullStr == null, "== null: null variable equals null");
        Assert(!(nullStr != null), "!= null: null variable does not not-equal null");
        string nonNull = "value";
        Assert(nonNull != null, "!= null: non-null value is not null");
        Assert(!(nonNull == null), "== null: non-null value is not null");

        // --- Logical && ---
        Assert(true && true, "&& true/true");
        Assert(!(true && false), "&& true/false is false");
        Assert(!(false && true), "&& false/true is false");
        Assert(!(false && false), "&& false/false is false");

        // --- Logical || ---
        Assert(true || false, "|| true/false");
        Assert(false || true, "|| false/true");
        Assert(true || true, "|| true/true");
        Assert(!(false || false), "|| false/false is false");

        // --- Logical ! ---
        Assert(!false, "! false is true");
        Assert(!(true == false), "! expression");
        bool flag = true;
        Assert(!flag == false, "! variable");

        // --- Bitwise & | ^ << >> ---
        int ba = 10;   // 0b1010
        int bb = 12;   // 0b1100
        Assert((ba & bb) == 8,  "bitwise & (1010 & 1100 = 1000 = 8)");
        Assert((ba | bb) == 14, "bitwise | (1010 | 1100 = 1110 = 14)");
        Assert((ba ^ bb) == 6,  "bitwise ^ (1010 ^ 1100 = 0110 = 6)");
        Assert((1 << 3) == 8,   "left shift << 3 (1 << 3 = 8)");
        Assert((16 >> 2) == 4,  "right shift >> 2 (16 >> 2 = 4)");
        Assert((0xFF & 0x0F) == 0x0F, "bitwise & mask (0xFF & 0x0F = 0x0F)");
        Assert((0xF0 | 0x0F) == 0xFF, "bitwise | combine (0xF0 | 0x0F = 0xFF)");
        Assert((0xFF ^ 0x0F) == 0xF0, "bitwise ^ flip lower nibble");

        // --- Null-coalescing ?? with null left operand ---
        string maybeNull = null;
        string coalesced = maybeNull ?? "default";
        Assert(coalesced == "default", "?? null left uses right side");

        // --- Null-coalescing ?? with non-null left operand ---
        string notNull = "actual";
        string coalesced2 = notNull ?? "default";
        Assert(coalesced2 == "actual", "?? non-null left returns left side");

        // --- Null-coalescing ?? chained ---
        string a1 = null;
        string a2 = null;
        string a3 = "found";
        string chainCoalesce = a1 ?? a2 ?? a3;
        Assert(chainCoalesce == "found", "?? chained: finds first non-null");

        // --- Conditional ?: (ternary) ---
        int x = 10;
        int ternaryResult = x > 5 ? 1 : 0;
        Assert(ternaryResult == 1, "ternary: condition true returns first branch");

        int ternaryFalse = x < 5 ? 1 : 0;
        Assert(ternaryFalse == 0, "ternary: condition false returns second branch");

        string ternaryStr = x == 10 ? "ten" : "other";
        Assert(ternaryStr == "ten", "ternary: string result");

        int ternaryNested = x > 5 ? (x > 8 ? 2 : 1) : 0;
        Assert(ternaryNested == 2, "ternary: nested ternary");

        // --- Compound assignment +=, -=, *=, /=, %= ---
        int c = 10;
        c += 5;
        Assert(c == 15, "compound +=");
        c -= 3;
        Assert(c == 12, "compound -=");
        c *= 2;
        Assert(c == 24, "compound *=");
        c /= 4;
        Assert(c == 6, "compound /=");
        c %= 4;
        Assert(c == 2, "compound %=");

        // --- Compound assignment on strings (+=) ---
        string str = "hello";
        str += " world";
        Assert(str == "hello world", "string += appends");

        // --- Operator precedence: arithmetic vs comparison ---
        Assert(2 + 3 * 4 == 14, "precedence: * before +");
        Assert((2 + 3) * 4 == 20, "precedence: parentheses override");
        Assert(10 - 3 - 2 == 5, "precedence: subtraction is left-associative");

        // --- Operator precedence: arithmetic vs logical ---
        Assert(1 + 1 == 2 && 3 - 1 == 2, "precedence: arithmetic before comparison before logical");
        Assert(5 > 3 || 2 > 10, "precedence: comparison before ||");
        Assert(!false && 2 == 2, "precedence: ! before && before comparison");

        // --- Short-circuit: && with false first (right side NOT evaluated) ---
        SideEffectCounter = 0;
        bool scResult1 = false && SideEffect(true);
        Assert(scResult1 == false, "short-circuit &&: result is false");
        Assert(SideEffectCounter == 0, "short-circuit &&: right side not evaluated when left is false");

        // --- Short-circuit: || with true first (right side NOT evaluated) ---
        SideEffectCounter = 0;
        bool scResult2 = true || SideEffect(false);
        Assert(scResult2 == true, "short-circuit ||: result is true");
        Assert(SideEffectCounter == 0, "short-circuit ||: right side not evaluated when left is true");

        // --- Short-circuit: && with true first (right side IS evaluated) ---
        SideEffectCounter = 0;
        bool scResult3 = true && SideEffect(true);
        Assert(scResult3 == true, "short-circuit &&: both sides when left is true");
        Assert(SideEffectCounter == 1, "short-circuit &&: right side evaluated when left is true");

        // --- Short-circuit: || with false first (right side IS evaluated) ---
        SideEffectCounter = 0;
        bool scResult4 = false || SideEffect(true);
        Assert(scResult4 == true, "short-circuit ||: evaluates right when left is false");
        Assert(SideEffectCounter == 1, "short-circuit ||: right side evaluated when left is false");

        // --- is operator ---
        object obj1 = "hello";
        Assert(obj1 is string, "is operator: string is string");
        Assert(!(obj1 is int), "is operator: string is not int");

        object obj2 = 42;
        Assert(obj2 is int, "is operator: boxed int is int");

        // --- as operator (cast) ---
        object asObj = "test string";
        string asStr = asObj as string;
        Assert(asStr != null, "as operator: string as string is not null");
        Assert(asStr == "test string", "as operator: cast preserves value");

        // --- as operator returns null for incompatible type ---
        object notAString = 123;
        string failedAs = notAString as string;
        Assert(failedAs == null, "as operator: returns null for incompatible type");

        // --- typeof-like comparison via is ---
        object typedObj = 3.14;
        Assert(typedObj is double, "is double: double value");
        Assert(!(typedObj is string), "is double: not a string");
        Assert(!(typedObj is int), "is double: not an int (different from double)");

        Console.WriteLine("T14_Operators: " + _pass + " passed, " + _fail + " failed");
    }
}
