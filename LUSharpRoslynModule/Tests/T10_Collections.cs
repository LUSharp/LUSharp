using System.Collections.Generic;

namespace LUSharpTests;

// ── Manual counting helpers (transpiler limitation) ────────────────────────────
// Dictionary.Count and HashSet.Count are backed by Luau hash tables, where # returns 0.
// We count entries manually by iterating with foreach to verify sizes.

public static class ColHelpers
{
    // Count entries in a string->int dictionary by iterating values
    public static int DictCountByIteration(Dictionary<string, int> d)
    {
        int n = 0;
        foreach (int v in d)
        {
            n = n + 1;
        }
        return n;
    }

    // Sum all values in a string->int dictionary
    public static int DictSumValues(Dictionary<string, int> d)
    {
        int sum = 0;
        foreach (int v in d)
        {
            sum = sum + v;
        }
        return sum;
    }

    // Count entries in an int->bool hashset by iterating values
    public static int HashSetCountByIteration(HashSet<int> h)
    {
        int n = 0;
        // In Luau, HashSet<int> is { [int]: bool }. foreach gives values (booleans), not keys.
        // We use ContainsKey checks on known values to test membership; count via Add tracking.
        // This helper iterates the set; each yielded "v" is true (the bool value), so we count them.
        foreach (bool v in h)
        {
            if (v)
            {
                n = n + 1;
            }
        }
        return n;
    }
}

// ── Test runner ───────────────────────────────────────────────────────────────

public static class T10_Collections
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

    // ── List<int> ─────────────────────────────────────────────────────────────

    private static void TestList()
    {
        Console.WriteLine("  -- List<int> --");

        List<int> list = new List<int>();

        // Add and Count (List is array-backed: # works correctly)
        list.Add(10);
        list.Add(20);
        list.Add(30);
        Assert(list.Count == 3, "List.Count after 3 Add()");

        // Indexer get (1-indexed in Luau, 0-indexed in C# — transpiler converts automatically)
        Assert(list[0] == 10, "List[0] == 10");
        Assert(list[1] == 20, "List[1] == 20");
        Assert(list[2] == 30, "List[2] == 30");

        // Indexer set
        list[1] = 99;
        Assert(list[1] == 99, "List[1] = 99 (indexer set)");

        // Contains
        Assert(list.Contains(10), "List.Contains(10) true");
        Assert(list.Contains(99), "List.Contains(99) true after set");
        Assert(!list.Contains(77), "List.Contains(77) false");

        // IndexOf
        Assert(list.IndexOf(10) == 0, "List.IndexOf(10) == 0");
        Assert(list.IndexOf(77) == -1, "List.IndexOf(missing) == -1");

        // Remove by value
        list[1] = 20;
        list.Remove(20);
        Assert(list.Count == 2, "List.Count after Remove(20)");
        Assert(!list.Contains(20), "List does not contain 20 after Remove");

        // RemoveAt
        list.Add(40);
        list.Add(50);
        // list = { 10, 30, 40, 50 }
        list.RemoveAt(0);
        // list = { 30, 40, 50 }
        Assert(list[0] == 30, "List.RemoveAt(0) shifts: [0] == 30");
        Assert(list.Count == 3, "List.Count after RemoveAt == 3");

        // Insert
        list.Insert(1, 99);
        // list = { 30, 99, 40, 50 }
        Assert(list[1] == 99, "List.Insert(1, 99): [1] == 99");
        Assert(list.Count == 4, "List.Count after Insert == 4");

        // Sort
        List<int> sortable = new List<int>();
        sortable.Add(5);
        sortable.Add(1);
        sortable.Add(3);
        sortable.Add(2);
        sortable.Add(4);
        sortable.Sort();
        Assert(sortable[0] == 1, "List.Sort(): [0] == 1");
        Assert(sortable[1] == 2, "List.Sort(): [1] == 2");
        Assert(sortable[4] == 5, "List.Sort(): [4] == 5");

        // Reverse (in-place)
        sortable.Reverse();
        Assert(sortable[0] == 5, "List.Reverse(): [0] == 5");
        Assert(sortable[4] == 1, "List.Reverse(): [4] == 1");

        // foreach iteration
        List<int> items = new List<int>();
        items.Add(7);
        items.Add(8);
        items.Add(9);
        int sum = 0;
        foreach (int v in items)
        {
            sum = sum + v;
        }
        Assert(sum == 24, "List foreach: sum 7+8+9 == 24");

        // AddRange
        List<int> extra = new List<int>();
        extra.Add(100);
        extra.Add(200);
        List<int> base2 = new List<int>();
        base2.Add(1);
        base2.AddRange(extra);
        Assert(base2.Count == 3, "List.AddRange(): Count == 3");
        Assert(base2[1] == 100, "List.AddRange(): [1] == 100");
        Assert(base2[2] == 200, "List.AddRange(): [2] == 200");

        // ToArray
        List<int> forArr = new List<int>();
        forArr.Add(11);
        forArr.Add(22);
        forArr.Add(33);
        int[] arr = forArr.ToArray();
        Assert(arr[0] == 11, "List.ToArray(): arr[0] == 11");
        Assert(arr[2] == 33, "List.ToArray(): arr[2] == 33");

        // Exists
        List<int> evens = new List<int>();
        evens.Add(2);
        evens.Add(4);
        evens.Add(6);
        Assert(evens.Exists(x => x > 5), "List.Exists(x > 5) == true");
        Assert(!evens.Exists(x => x > 10), "List.Exists(x > 10) == false");

        // Find
        int found = evens.Find(x => x > 3);
        Assert(found == 4, "List.Find(x > 3) == 4 (first match)");

        // FindIndex
        int fi = evens.FindIndex(x => x == 6);
        Assert(fi == 2, "List.FindIndex(x == 6) == 2");

        // Clear
        List<int> toClear = new List<int>();
        toClear.Add(1);
        toClear.Add(2);
        toClear.Clear();
        Assert(toClear.Count == 0, "List.Clear(): Count == 0");

        // Collection initializer
        List<int> initList = new List<int> { 10, 20, 30 };
        Assert(initList[0] == 10, "List initializer: [0] == 10");
        Assert(initList[2] == 30, "List initializer: [2] == 30");
        Assert(initList.Count == 3, "List initializer: Count == 3");
    }

    // ── Dictionary<string, int> ──────────────────────────────────────────────

    private static void TestDictionary()
    {
        Console.WriteLine("  -- Dictionary<string,int> --");

        Dictionary<string, int> d = new Dictionary<string, int>();

        // Add and indexer set
        d.Add("apple", 1);
        d["banana"] = 2;
        d["cherry"] = 3;

        // Indexer get
        Assert(d["apple"] == 1, "Dict[\"apple\"] == 1");
        Assert(d["banana"] == 2, "Dict[\"banana\"] == 2");
        Assert(d["cherry"] == 3, "Dict[\"cherry\"] == 3");

        // Indexer overwrite
        d["apple"] = 99;
        Assert(d["apple"] == 99, "Dict[\"apple\"] = 99 overwrites");

        // ContainsKey
        Assert(d.ContainsKey("apple"), "Dict.ContainsKey(\"apple\") true");
        Assert(d.ContainsKey("banana"), "Dict.ContainsKey(\"banana\") true");
        Assert(!d.ContainsKey("durian"), "Dict.ContainsKey(\"durian\") false");

        // ContainsValue
        Assert(d.ContainsValue(2), "Dict.ContainsValue(2) true");
        Assert(d.ContainsValue(99), "Dict.ContainsValue(99) true after overwrite");
        Assert(!d.ContainsValue(777), "Dict.ContainsValue(777) false");

        // TryGetValue with out parameter
        bool ok = d.TryGetValue("banana", out int val);
        Assert(ok, "Dict.TryGetValue(\"banana\"): ok == true");
        Assert(val == 2, "Dict.TryGetValue(\"banana\"): val == 2");

        bool ok2 = d.TryGetValue("grape", out int val2);
        Assert(!ok2, "Dict.TryGetValue(\"grape\"): ok == false");

        // Manual count via value iteration (# doesn't work on hash tables in Luau)
        Assert(ColHelpers.DictCountByIteration(d) == 3, "Dict iteration count == 3");

        // Remove
        d.Remove("cherry");
        Assert(!d.ContainsKey("cherry"), "Dict.Remove(\"cherry\"): key gone");
        Assert(ColHelpers.DictCountByIteration(d) == 2, "Dict iteration count after Remove == 2");

        // Sum values via iteration
        Dictionary<string, int> nums = new Dictionary<string, int>();
        nums["x"] = 10;
        nums["y"] = 20;
        nums["z"] = 30;
        int vsum = ColHelpers.DictSumValues(nums);
        Assert(vsum == 60, "Dict sum of values (10+20+30) == 60");

        // Clear
        nums.Clear();
        Assert(!nums.ContainsKey("x"), "Dict.Clear(): key \"x\" gone");
        Assert(ColHelpers.DictCountByIteration(nums) == 0, "Dict iteration count after Clear == 0");

        // Collection initializer
        Dictionary<string, int> init = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        };
        Assert(init["one"] == 1, "Dict initializer: [\"one\"] == 1");
        Assert(init["three"] == 3, "Dict initializer: [\"three\"] == 3");
        Assert(init.ContainsKey("two"), "Dict initializer: ContainsKey(\"two\") true");
    }

    // ── HashSet<int> ─────────────────────────────────────────────────────────
    // NOTE: HashSet<T> is implemented as { [T]: bool } in Luau.
    // foreach gives boolean values (true), not the set elements.
    // Count via # returns 0 (hash table). We test membership via Contains only.

    private static void TestHashSet()
    {
        Console.WriteLine("  -- HashSet<int> --");

        HashSet<int> set = new HashSet<int>();

        // Add and Contains
        set.Add(10);
        set.Add(20);
        set.Add(30);
        Assert(set.Contains(10), "HashSet.Contains(10) after Add");
        Assert(set.Contains(20), "HashSet.Contains(20) after Add");
        Assert(set.Contains(30), "HashSet.Contains(30) after Add");
        Assert(!set.Contains(99), "HashSet.Contains(99) false");

        // Duplicate add is no-op
        set.Add(20);
        Assert(set.Contains(20), "HashSet.Add(20) again: still Contains(20)");

        // Remove
        set.Remove(20);
        Assert(!set.Contains(20), "HashSet.Remove(20): no longer Contains(20)");
        Assert(set.Contains(10), "HashSet.Remove(20): 10 still present");
        Assert(set.Contains(30), "HashSet.Remove(20): 30 still present");

        // Remove non-existent is no-op
        set.Remove(999);
        Assert(set.Contains(10), "HashSet.Remove(999): 10 unaffected");

        // Count via foreach iteration (each yielded value is `true` in Luau)
        HashSet<int> countable = new HashSet<int>();
        countable.Add(1);
        countable.Add(2);
        countable.Add(3);
        Assert(ColHelpers.HashSetCountByIteration(countable) == 3, "HashSet foreach count == 3 (booleans)");

        // UnionWith: a = {1,2}, b = {2,3,4} → a becomes {1,2,3,4}
        HashSet<int> a = new HashSet<int>();
        a.Add(1);
        a.Add(2);
        HashSet<int> b = new HashSet<int>();
        b.Add(2);
        b.Add(3);
        b.Add(4);
        a.UnionWith(b);
        Assert(a.Contains(1), "HashSet.UnionWith: 1 present");
        Assert(a.Contains(2), "HashSet.UnionWith: 2 present");
        Assert(a.Contains(3), "HashSet.UnionWith: 3 added from b");
        Assert(a.Contains(4), "HashSet.UnionWith: 4 added from b");

        // IntersectWith: x = {1,2,3}, y = {2,3,4} → x becomes {2,3}
        HashSet<int> x = new HashSet<int>();
        x.Add(1);
        x.Add(2);
        x.Add(3);
        HashSet<int> y = new HashSet<int>();
        y.Add(2);
        y.Add(3);
        y.Add(4);
        x.IntersectWith(y);
        Assert(!x.Contains(1), "HashSet.IntersectWith: 1 removed (not in y)");
        Assert(x.Contains(2), "HashSet.IntersectWith: 2 remains (in both)");
        Assert(x.Contains(3), "HashSet.IntersectWith: 3 remains (in both)");
        Assert(!x.Contains(4), "HashSet.IntersectWith: 4 absent (never in x)");
    }

    // ── Queue<int> ───────────────────────────────────────────────────────────
    // Queue is array-backed in Luau (table.insert/remove), so Count (#) works correctly.

    private static void TestQueue()
    {
        Console.WriteLine("  -- Queue<int> --");

        Queue<int> q = new Queue<int>();

        // Enqueue
        q.Enqueue(10);
        q.Enqueue(20);
        q.Enqueue(30);

        // Count (array-backed: # works)
        Assert(q.Count == 3, "Queue.Count after 3 Enqueue()");

        // Peek returns front without removing
        Assert(q.Peek() == 10, "Queue.Peek() == 10 (FIFO front)");
        Assert(q.Count == 3, "Queue.Count unchanged after Peek()");

        // Dequeue in FIFO order
        int first = q.Dequeue();
        Assert(first == 10, "Queue.Dequeue() == 10 (first in, first out)");
        Assert(q.Count == 2, "Queue.Count after Dequeue() == 2");

        int second = q.Dequeue();
        Assert(second == 20, "Queue.Dequeue() second == 20");
        Assert(q.Count == 1, "Queue.Count == 1 after second Dequeue");

        // Peek after two dequeues
        Assert(q.Peek() == 30, "Queue.Peek() == 30 after two dequeues");

        // Dequeue last
        int third = q.Dequeue();
        Assert(third == 30, "Queue.Dequeue() third == 30");
        Assert(q.Count == 0, "Queue.Count == 0 after draining");

        // Enqueue after empty
        q.Enqueue(99);
        Assert(q.Count == 1, "Queue.Count == 1 after re-enqueue");
        Assert(q.Peek() == 99, "Queue.Peek() == 99 after re-enqueue");

        // Multiple enqueue then dequeue
        q.Enqueue(1);
        q.Enqueue(2);
        q.Enqueue(3);
        // Queue contains: 99, 1, 2, 3
        Assert(q.Count == 4, "Queue.Count == 4 after adding to existing");
        Assert(q.Dequeue() == 99, "FIFO: dequeue 99 (original re-enqueue)");
        Assert(q.Dequeue() == 1, "FIFO: dequeue 1");
        Assert(q.Dequeue() == 2, "FIFO: dequeue 2");
        Assert(q.Dequeue() == 3, "FIFO: dequeue 3");
        Assert(q.Count == 0, "Queue fully drained again");
    }

    // ── Stack<int> ───────────────────────────────────────────────────────────
    // Stack is array-backed in Luau (table.insert/remove from end), so Count (#) works.

    private static void TestStack()
    {
        Console.WriteLine("  -- Stack<int> --");

        Stack<int> s = new Stack<int>();

        // Push
        s.Push(10);
        s.Push(20);
        s.Push(30);

        // Count (array-backed: # works)
        Assert(s.Count == 3, "Stack.Count after 3 Push()");

        // Peek returns top without removing (LIFO: top is last pushed)
        Assert(s.Peek() == 30, "Stack.Peek() == 30 (LIFO top)");
        Assert(s.Count == 3, "Stack.Count unchanged after Peek()");

        // Pop in LIFO order
        int top = s.Pop();
        Assert(top == 30, "Stack.Pop() == 30 (last in, first out)");
        Assert(s.Count == 2, "Stack.Count after Pop() == 2");

        int second = s.Pop();
        Assert(second == 20, "Stack.Pop() second == 20");
        Assert(s.Count == 1, "Stack.Count == 1 after second Pop");

        // Peek single remaining
        Assert(s.Peek() == 10, "Stack.Peek() == 10 one element remaining");

        // Pop last
        int last = s.Pop();
        Assert(last == 10, "Stack.Pop() last == 10");
        Assert(s.Count == 0, "Stack.Count == 0 after draining");

        // Push after empty
        s.Push(77);
        Assert(s.Count == 1, "Stack.Count == 1 after re-push");
        Assert(s.Peek() == 77, "Stack.Peek() == 77 after re-push");

        // LIFO order with multiple pushes
        s.Push(1);
        s.Push(2);
        s.Push(3);
        // Stack from bottom to top: 77, 1, 2, 3
        Assert(s.Count == 4, "Stack.Count == 4 after pushing 3 more");
        Assert(s.Pop() == 3, "Stack LIFO: Pop 3 (last pushed)");
        Assert(s.Pop() == 2, "Stack LIFO: Pop 2");
        Assert(s.Pop() == 1, "Stack LIFO: Pop 1");
        Assert(s.Pop() == 77, "Stack LIFO: Pop 77 (re-pushed)");
        Assert(s.Count == 0, "Stack fully drained");
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T10_Collections ===");

        TestList();
        TestDictionary();
        TestHashSet();
        TestQueue();
        TestStack();

        // ── scorecard ──
        Console.WriteLine("T10_Collections: " + _pass + " passed, " + _fail + " failed");
    }
}
