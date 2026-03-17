using System;
using System.Collections.Generic;
using System.Linq;

namespace LUSharpTests;

public class T12_SortItem
{
    public string Name;
    public int Value;

    public T12_SortItem(string name, int value)
    {
        Name = name;
        Value = value;
    }
}

public static class T12_Linq
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
        Console.WriteLine("=== T12_Linq ===");

        List<int> nums = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // --- Where (filter evens) ---
        List<int> evens = nums.Where(x => x % 2 == 0).ToList();
        Assert(evens.Count == 5, "Where: filter evens count");
        Assert(evens[0] == 2, "Where: first even is 2");
        Assert(evens[4] == 10, "Where: last even is 10");

        // --- Select (transform x*2) ---
        List<int> doubled = nums.Select(x => x * 2).ToList();
        Assert(doubled.Count == 10, "Select: count unchanged");
        Assert(doubled[0] == 2, "Select: first element doubled");
        Assert(doubled[9] == 20, "Select: last element doubled");

        // --- First ---
        int first = nums.First();
        Assert(first == 1, "First: returns first element");

        int firstEven = nums.First(x => x % 2 == 0);
        Assert(firstEven == 2, "First with predicate: first even");

        // --- FirstOrDefault (empty sequence) ---
        // Note: In Luau, FirstOrDefault returns nil for empty sequences (no type info at runtime).
        // C# returns default(T)=0 for int, but nil is the Luau equivalent of "no value".
        List<int> empty = new List<int>();
        object defaultVal = empty.FirstOrDefault();
        Assert(defaultVal == null, "FirstOrDefault on empty returns nil");

        object noMatch = nums.FirstOrDefault(x => x > 100);
        Assert(noMatch == null, "FirstOrDefault no match returns nil");

        // --- Last ---
        int last = nums.Last();
        Assert(last == 10, "Last: returns last element");

        int lastOdd = nums.Last(x => x % 2 != 0);
        Assert(lastOdd == 9, "Last with predicate: last odd");

        // --- LastOrDefault ---
        object lastDefault = empty.LastOrDefault();
        Assert(lastDefault == null, "LastOrDefault on empty returns nil");

        // --- Any ---
        bool anyBig = nums.Any(x => x > 5);
        Assert(anyBig == true, "Any: has element > 5");

        bool anyHuge = nums.Any(x => x > 100);
        Assert(anyHuge == false, "Any: no element > 100");

        bool anyAtAll = empty.Any();
        Assert(anyAtAll == false, "Any: empty list is false");

        // --- All ---
        bool allPositive = nums.All(x => x > 0);
        Assert(allPositive == true, "All: all positive");

        bool allSmall = nums.All(x => x < 5);
        Assert(allSmall == false, "All: not all < 5");

        // --- Count with predicate ---
        int countEvens = nums.Count(x => x % 2 == 0);
        Assert(countEvens == 5, "Count with predicate: evens");

        int countAll = nums.Count();
        Assert(countAll == 10, "Count: total elements");

        // --- Sum ---
        int total = nums.Sum(x => x);
        Assert(total == 55, "Sum: 1+2+...+10 = 55");

        // --- Min ---
        int minVal = nums.Min(x => x);
        Assert(minVal == 1, "Min: minimum value");

        // --- Max ---
        int maxVal = nums.Max(x => x);
        Assert(maxVal == 10, "Max: maximum value");

        // --- Average ---
        double avg = nums.Average(x => x);
        Assert(avg == 5.5, "Average: (1+...+10)/10 = 5.5");

        // --- OrderBy ---
        List<int> disordered = new List<int> { 5, 3, 1, 4, 2 };
        List<int> ordered = disordered.OrderBy(x => x).ToList();
        Assert(ordered[0] == 1, "OrderBy: first is 1");
        Assert(ordered[4] == 5, "OrderBy: last is 5");

        // --- OrderByDescending ---
        List<int> orderedDesc = disordered.OrderByDescending(x => x).ToList();
        Assert(orderedDesc[0] == 5, "OrderByDescending: first is 5");
        Assert(orderedDesc[4] == 1, "OrderByDescending: last is 1");

        // --- Distinct ---
        List<int> withDups = new List<int> { 1, 2, 2, 3, 3, 3, 4 };
        List<int> distinct = withDups.Distinct().ToList();
        Assert(distinct.Count == 4, "Distinct: removes duplicates");
        Assert(distinct[0] == 1, "Distinct: first element");

        // --- Take(3) ---
        List<int> taken = nums.Take(3).ToList();
        Assert(taken.Count == 3, "Take: count is 3");
        Assert(taken[0] == 1, "Take: first element");
        Assert(taken[2] == 3, "Take: third element");

        // --- Skip(2) ---
        List<int> skipped = nums.Skip(2).ToList();
        Assert(skipped.Count == 8, "Skip: count is 8");
        Assert(skipped[0] == 3, "Skip: first element after skip");

        // --- Aggregate (sum via reduce) ---
        int aggregated = nums.Aggregate(0, (acc, x) => acc + x);
        Assert(aggregated == 55, "Aggregate: sum is 55");

        // --- Contains ---
        bool contains5 = nums.Contains(5);
        Assert(contains5 == true, "Contains: 5 is present");

        bool contains99 = nums.Contains(99);
        Assert(contains99 == false, "Contains: 99 is not present");

        // --- ToList ---
        List<int> aslist = nums.Where(x => x < 4).ToList();
        Assert(aslist.Count == 3, "ToList: correct count");

        // --- ToArray ---
        int[] asArray = nums.Take(3).ToArray();
        Assert(asArray.Length == 3, "ToArray: correct length");
        Assert(asArray[0] == 1, "ToArray: first element");

        // --- ToDictionary ---
        List<string> words = new List<string> { "apple", "banana", "cherry" };
        Dictionary<string, int> wordLengths = words.ToDictionary(w => w, w => w.Length);
        Assert(wordLengths["apple"] == 5, "ToDictionary: apple has length 5");
        Assert(wordLengths["banana"] == 6, "ToDictionary: banana has length 6");

        // --- SelectMany (flatten) ---
        List<List<int>> nested = new List<List<int>> {
            new List<int> { 1, 2 },
            new List<int> { 3, 4 },
            new List<int> { 5 }
        };
        List<int> flat = nested.SelectMany(x => x).ToList();
        Assert(flat.Count == 5, "SelectMany: flattened count");
        Assert(flat[0] == 1, "SelectMany: first element");
        Assert(flat[4] == 5, "SelectMany: last element");

        // --- Concat ---
        List<int> ca = new List<int> { 1, 2, 3 };
        List<int> cb = new List<int> { 4, 5, 6 };
        List<int> combined = ca.Concat(cb).ToList();
        Assert(combined.Count == 6, "Concat: count");
        Assert(combined[3] == 4, "Concat: first element from second list");

        // --- Except ---
        List<int> except = ca.Except(new List<int> { 2, 3 }).ToList();
        Assert(except.Count == 1, "Except: removes matching elements");
        Assert(except[0] == 1, "Except: only non-matching element remains");

        // --- Intersect ---
        List<int> cc = new List<int> { 1, 2, 3, 4 };
        List<int> cd = new List<int> { 2, 3, 5 };
        List<int> intersect = cc.Intersect(cd).ToList();
        Assert(intersect.Count == 2, "Intersect: count of common elements");
        Assert(intersect[0] == 2, "Intersect: first common element");

        // --- Union ---
        List<int> union = ca.Union(cb).ToList();
        Assert(union.Count == 6, "Union: distinct elements from both");

        List<int> unionWithDup = new List<int> { 1, 2, 3 };
        List<int> unionOther = new List<int> { 2, 3, 4 };
        List<int> unionResult = unionWithDup.Union(unionOther).ToList();
        Assert(unionResult.Count == 4, "Union: deduplicates across both lists");

        // --- SequenceEqual ---
        List<int> seq1 = new List<int> { 1, 2, 3 };
        List<int> seq2 = new List<int> { 1, 2, 3 };
        List<int> seq3 = new List<int> { 1, 2, 4 };
        bool seqEq = seq1.SequenceEqual(seq2);
        Assert(seqEq == true, "SequenceEqual: equal sequences");
        bool seqNeq = seq1.SequenceEqual(seq3);
        Assert(seqNeq == false, "SequenceEqual: unequal sequences");

        // --- GroupBy (group by category string) ---
        List<string> fruits = new List<string> { "apple", "avocado", "banana", "blueberry", "cherry" };
        // Group by whether the string starts with 'a', 'b', or 'c' using Substring
        var groups = fruits.GroupBy(f => f.Substring(0, 1)).ToList();
        Assert(groups.Count == 3, "GroupBy: three groups (a, b, c)");
        Assert(groups[0].Key == "a", "GroupBy: first group key is 'a'");
        Assert(groups[0].Count() == 2, "GroupBy: two fruits start with 'a'");

        // --- Zip ---
        List<int> zleft = new List<int> { 1, 2, 3 };
        List<string> zright = new List<string> { "one", "two", "three" };
        List<string> zipped = zleft.Zip(zright, (n, s) => n + "=" + s).ToList();
        Assert(zipped.Count == 3, "Zip: count");
        Assert(zipped[0] == "1=one", "Zip: first pair");
        Assert(zipped[2] == "3=three", "Zip: last pair");

        // --- ThenBy (multi-key sort) ---
        List<T12_SortItem> items = new List<T12_SortItem> {
            new T12_SortItem("b", 2),
            new T12_SortItem("a", 3),
            new T12_SortItem("a", 1),
            new T12_SortItem("b", 1)
        };
        List<T12_SortItem> sorted = items.OrderBy(i => i.Name).ThenBy(i => i.Value).ToList();
        Assert(sorted[0].Name == "a", "ThenBy: first group is 'a'");
        Assert(sorted[0].Value == 1, "ThenBy: a group sorted by value, first is 1");
        Assert(sorted[1].Value == 3, "ThenBy: a group sorted by value, second is 3");
        Assert(sorted[2].Name == "b", "ThenBy: second group is 'b'");
        Assert(sorted[2].Value == 1, "ThenBy: b group sorted by value, first is 1");

        Console.WriteLine("T12_Linq: " + _pass + " passed, " + _fail + " failed");
    }
}
