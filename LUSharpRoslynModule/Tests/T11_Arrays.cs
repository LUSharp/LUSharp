using System;
using System.Collections.Generic;

namespace LUSharpTests;

public struct T11_Point2D
{
    public int X;
    public int Y;

    public T11_Point2D(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public static class T11_Arrays
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
        Console.WriteLine("=== T11_Arrays ===");

        // --- Array creation with explicit size ---
        int[] sized = new int[5];
        Assert(sized.Length == 5, "new int[5] has length 5");
        Assert(sized[0] == 0, "new int[5] default value is 0");

        // --- Array creation with initializer ---
        int[] init = new int[] { 10, 20, 30 };
        Assert(init.Length == 3, "initializer array has correct length");
        Assert(init[0] == 10, "initializer array index 0");
        Assert(init[1] == 20, "initializer array index 1");
        Assert(init[2] == 30, "initializer array index 2");

        // --- Array Length ---
        int[] five = new int[] { 1, 2, 3, 4, 5 };
        Assert(five.Length == 5, "array Length property");

        // --- 0-to-1 index conversion ---
        // C# arr[0] maps to Lua arr[1] under the hood
        int[] idx = new int[] { 100, 200, 300 };
        Assert(idx[0] == 100, "index 0 maps to first element");
        Assert(idx[2] == 300, "index 2 maps to third element");

        // --- Array assignment ---
        int[] assign = new int[3];
        assign[0] = 7;
        assign[1] = 8;
        assign[2] = 9;
        Assert(assign[0] == 7, "array element assignment [0]");
        Assert(assign[1] == 8, "array element assignment [1]");
        Assert(assign[2] == 9, "array element assignment [2]");

        // --- Array.Copy ---
        int[] src = new int[] { 1, 2, 3, 4, 5 };
        int[] dst = new int[5];
        Array.Copy(src, dst, 5);
        Assert(dst[0] == 1, "Array.Copy element 0");
        Assert(dst[4] == 5, "Array.Copy element 4");

        // --- Array.Sort ---
        int[] unsorted = new int[] { 5, 3, 1, 4, 2 };
        Array.Sort(unsorted);
        Assert(unsorted[0] == 1, "Array.Sort first element");
        Assert(unsorted[4] == 5, "Array.Sort last element");
        Assert(unsorted[2] == 3, "Array.Sort middle element");

        // --- Array.IndexOf ---
        int[] search = new int[] { 10, 20, 30, 40 };
        int found = Array.IndexOf(search, 30);
        Assert(found == 2, "Array.IndexOf finds element at correct index");
        int notFound = Array.IndexOf(search, 99);
        Assert(notFound == -1, "Array.IndexOf returns -1 when not found");

        // --- First and last element ---
        int[] bounds = new int[] { 42, 0, 0, 0, 99 };
        Assert(bounds[0] == 42, "first element access");
        Assert(bounds[4] == 99, "last element access (index 4)");

        // --- Empty array ---
        int[] empty = new int[0];
        Assert(empty.Length == 0, "empty array has length 0");

        // --- Array of strings ---
        string[] words = new string[] { "hello", "world", "lua" };
        Assert(words.Length == 3, "string array length");
        Assert(words[0] == "hello", "string array index 0");
        Assert(words[2] == "lua", "string array index 2");

        // --- Multi-element initializer {1,2,3,4,5} ---
        int[] multi = new int[] { 1, 2, 3, 4, 5 };
        Assert(multi.Length == 5, "5-element initializer length");
        Assert(multi[0] == 1, "5-element initializer [0]");
        Assert(multi[4] == 5, "5-element initializer [4]");

        // --- Jagged arrays (int[][]) ---
        int[][] jagged = new int[3][];
        jagged[0] = new int[] { 1, 2 };
        jagged[1] = new int[] { 3, 4, 5 };
        jagged[2] = new int[] { 6 };
        Assert(jagged[0].Length == 2, "jagged[0] has length 2");
        Assert(jagged[1].Length == 3, "jagged[1] has length 3");
        Assert(jagged[2].Length == 1, "jagged[2] has length 1");
        Assert(jagged[0][0] == 1, "jagged[0][0]");
        Assert(jagged[1][2] == 5, "jagged[1][2]");
        Assert(jagged[2][0] == 6, "jagged[2][0]");

        // --- Array in foreach ---
        int[] nums = new int[] { 10, 20, 30, 40 };
        int sum = 0;
        foreach (int n in nums)
        {
            sum = sum + n;
        }
        Assert(sum == 100, "foreach over array accumulates correctly");

        // --- Array of structs ---
        T11_Point2D[] points = new T11_Point2D[2];
        points[0] = new T11_Point2D(1, 2);
        points[1] = new T11_Point2D(3, 4);
        Assert(points[0].X == 1, "array of structs [0].X");
        Assert(points[1].Y == 4, "array of structs [1].Y");

        // --- Modify through index, verify length unchanged ---
        int[] mod = new int[] { 0, 0, 0 };
        mod[1] = 55;
        Assert(mod.Length == 3, "length unchanged after assignment");
        Assert(mod[1] == 55, "modified element via index");

        // --- Overwrite existing element and verify ---
        int[] overwrite = new int[] { 100, 200, 300 };
        overwrite[0] = 999;
        Assert(overwrite[0] == 999, "overwrite first element");
        Assert(overwrite[1] == 200, "overwrite: second element unchanged");
        Assert(overwrite[2] == 300, "overwrite: third element unchanged");

        // --- Index into result of expression ---
        int[] exprIdx = new int[] { 5, 10, 15, 20 };
        int offset = 1;
        Assert(exprIdx[offset] == 10, "expression index [offset]");
        Assert(exprIdx[offset + 1] == 15, "expression index [offset+1]");

        Console.WriteLine("T11_Arrays: " + _pass + " passed, " + _fail + " failed");
    }
}
