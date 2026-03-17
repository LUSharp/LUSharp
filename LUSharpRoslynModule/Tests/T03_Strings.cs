namespace LUSharpTests;

public static class T03_Strings
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
        Console.WriteLine("=== T03_Strings ===");

        // ── Length ──
        string hello = "hello";
        Assert(hello.Length == 5, "Length of 'hello' == 5");

        string empty = "";
        Assert(empty.Length == 0, "Length of empty string == 0");

        // ── IndexOf ──
        string src = "abcabc";
        Assert(src.IndexOf("b") == 1, "IndexOf 'b' in 'abcabc' == 1");
        Assert(src.IndexOf("x") == -1, "IndexOf 'x' not found == -1");
        Assert(src.IndexOf("abc") == 0, "IndexOf 'abc' at start == 0");

        // ── LastIndexOf ──
        Assert(src.LastIndexOf("b") == 4, "LastIndexOf 'b' == 4");
        Assert(src.LastIndexOf("x") == -1, "LastIndexOf 'x' not found == -1");

        // ── Substring (1 arg) ──
        string sub1 = "hello world".Substring(6);
        Assert(sub1 == "world", "Substring(6) == 'world'");

        // ── Substring (2 args) ──
        string sub2 = "hello world".Substring(0, 5);
        Assert(sub2 == "hello", "Substring(0,5) == 'hello'");

        string sub3 = "abcdef".Substring(2, 3);
        Assert(sub3 == "cde", "Substring(2,3) == 'cde'");

        // ── Replace ──
        // string.gsub returns (string, count); assignment captures only the string
        string rep = "hello world".Replace("world", "Luau");
        Assert(rep == "hello Luau", "Replace 'world' with 'Luau'");

        string repNone = "hello".Replace("x", "y");
        Assert(repNone == "hello", "Replace with no match returns original");

        // ── Contains ──
        Assert("hello world".Contains("world"), "Contains 'world'");
        Assert(!"hello world".Contains("xyz"), "Not Contains 'xyz'");
        Assert("abc".Contains(""), "Contains empty string (vacuously true)");

        // ── StartsWith ──
        Assert("hello".StartsWith("hel"), "StartsWith 'hel'");
        Assert(!"hello".StartsWith("llo"), "Not StartsWith 'llo'");
        Assert("hello".StartsWith("hello"), "StartsWith full string");
        Assert("hello".StartsWith(""), "StartsWith empty string");

        // ── EndsWith ──
        Assert("hello".EndsWith("llo"), "EndsWith 'llo'");
        Assert(!"hello".EndsWith("hel"), "Not EndsWith 'hel'");
        Assert("hello".EndsWith("hello"), "EndsWith full string");

        // ── ToLower / ToUpper ──
        Assert("HELLO".ToLower() == "hello", "ToLower 'HELLO'");
        Assert("hello".ToUpper() == "HELLO", "ToUpper 'hello'");
        Assert("Hello World".ToLower() == "hello world", "ToLower mixed case");

        // ── Trim ──
        string trimmed = "  hello  ".Trim();
        Assert(trimmed == "hello", "Trim removes surrounding spaces");

        string noTrim = "hello".Trim();
        Assert(noTrim == "hello", "Trim on already-trimmed string");

        // ── TrimStart / TrimEnd ──
        string ts = "  hello  ".TrimStart();
        Assert(ts == "hello  ", "TrimStart removes leading spaces");

        string te = "  hello  ".TrimEnd();
        Assert(te == "  hello", "TrimEnd removes trailing spaces");

        // ── string.IsNullOrEmpty ──
        Assert(string.IsNullOrEmpty(""), "IsNullOrEmpty('') == true");
        Assert(string.IsNullOrEmpty(null), "IsNullOrEmpty(null) == true");
        Assert(!string.IsNullOrEmpty("x"), "IsNullOrEmpty('x') == false");

        // ── string.IsNullOrWhiteSpace ──
        Assert(string.IsNullOrWhiteSpace(""), "IsNullOrWhiteSpace('') == true");
        Assert(string.IsNullOrWhiteSpace("   "), "IsNullOrWhiteSpace('   ') == true");
        Assert(string.IsNullOrWhiteSpace(null), "IsNullOrWhiteSpace(null) == true");
        Assert(!string.IsNullOrWhiteSpace("a"), "IsNullOrWhiteSpace('a') == false");

        // ── string.Join ──
        List<string> parts = new List<string>();
        parts.Add("a");
        parts.Add("b");
        parts.Add("c");
        string joined = string.Join(",", parts);
        Assert(joined == "a,b,c", "Join with comma");

        string joinedEmpty = string.Join("-", new List<string>());
        Assert(joinedEmpty == "", "Join empty list");

        // ── string concatenation (+) ──
        string ca = "hello" + " " + "world";
        Assert(ca == "hello world", "string + concat");

        int num = 42;
        string withNum = "answer=" + num;
        Assert(withNum == "answer=42", "string + int concat");

        // ── string.Format ──
        string fmt = string.Format("{0} is {1}", "x", 10);
        Assert(fmt == "x is 10", "string.Format two args");

        string fmt1 = string.Format("{0}", "only");
        Assert(fmt1 == "only", "string.Format one arg");

        // ── string interpolation ──
        string name = "Luau";
        int ver = 5;
        string interp = $"Hello {name} v{ver}";
        Assert(interp == "Hello Luau v5", "string interpolation");

        string interpExpr = $"sum={1 + 2}";
        Assert(interpExpr == "sum=3", "string interpolation with expression");

        // ── char access via indexer → string.byte() returns numeric byte ──
        // s[i] → string.byte(s, i+1). Compare with known byte values.
        string alpha = "ABC";
        int charA = alpha[0];
        Assert(charA == 65, "s[0] == byte value of 'A' (65)");

        int charB = alpha[1];
        Assert(charB == 66, "s[1] == byte value of 'B' (66)");

        int charC = alpha[2];
        Assert(charC == 67, "s[2] == byte value of 'C' (67)");

        // ── PadLeft ──
        string padL = "42".PadLeft(5);
        Assert(padL == "   42", "PadLeft(5) pads with spaces");

        string padLChar = "7".PadLeft(4, '0');
        Assert(padLChar == "0007", "PadLeft(4, '0') pads with zeros");

        // ── PadRight ──
        string padR = "hi".PadRight(5);
        Assert(padR == "hi   ", "PadRight(5) pads with spaces");

        // ── Insert ──
        string ins = "helloworld".Insert(5, " ");
        Assert(ins == "hello world", "Insert space at index 5");

        // ── Remove ──
        string rem1 = "hello world".Remove(5);
        Assert(rem1 == "hello", "Remove from index 5 to end");

        string rem2 = "hello world".Remove(5, 6);
        Assert(rem2 == "hello", "Remove 6 chars starting at index 5");

        // ── string comparison (==) ──
        string sa = "abc";
        string sb = "abc";
        Assert(sa == sb, "equal strings compare equal");
        Assert(sa != "xyz", "different strings compare unequal");

        // ── string.Empty ──
        Assert(string.Empty == "", "string.Empty == ''");
        Assert(string.Empty.Length == 0, "string.Empty.Length == 0");

        // ── Split ──
        // string.split in Luau returns a table; Length check via list
        List<string> splitResult = new List<string>();
        string[] splitArr = "a,b,c".Split(",");
        for (int i = 0; i < splitArr.Length; i++)
        {
            splitResult.Add(splitArr[i]);
        }
        Assert(splitResult.Count == 3, "Split by comma gives 3 parts");
        Assert(splitResult[0] == "a", "Split[0] == 'a'");
        Assert(splitResult[1] == "b", "Split[1] == 'b'");
        Assert(splitResult[2] == "c", "Split[2] == 'c'");

        // ── scorecard ──
        Console.WriteLine("T03_Strings: " + _pass + " passed, " + _fail + " failed");
    }
}
