using Microsoft.CodeAnalysis;

namespace LUSharpRoslynModule.Transpiler;

public delegate string MethodRewriter(string receiver, string[] args, ITypeSymbol? receiverType);

public static class MethodMapper
{
    private static readonly Dictionary<(string TypeName, string MethodName), MethodRewriter> _map = new();

    public static void Register(string typeName, string methodName, MethodRewriter rewriter)
    {
        _map[(typeName, methodName)] = rewriter;
    }

    public static string? TryRewrite(IMethodSymbol method, string receiver, string[] args, ITypeSymbol? receiverType)
    {
        var typeName = method.ContainingType?.Name ?? "";
        // Try exact type match first
        if (_map.TryGetValue((typeName, method.Name), out var rewriter))
            return rewriter(receiver, args, receiverType);
        // Try base types
        var current = method.ContainingType?.BaseType;
        while (current != null)
        {
            if (_map.TryGetValue((current.Name, method.Name), out rewriter))
                return rewriter(receiver, args, receiverType);
            current = current.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Try to rewrite based on type name string (for non-SemanticModel fallback path).
    /// </summary>
    public static string? TryRewriteByName(string typeName, string methodName, string receiver, string[] args)
    {
        if (_map.TryGetValue((typeName, methodName), out var rewriter))
            return rewriter(receiver, args, null);
        return null;
    }

    static MethodMapper()
    {
        RegisterAll();
    }

    private static void RegisterAll()
    {
        // === Console ===
        Register("Console", "WriteLine", (r, a, _) => a.Length > 0 ? $"print({a[0]})" : "print()");
        Register("Console", "Write", (r, a, _) => a.Length > 0 ? $"print({a[0]})" : "print()");

        // === String instance ===
        Register("String", "Substring", (r, a, _) => a.Length == 2
            ? $"string.sub({r}, {a[0]} + 1, {a[0]} + {a[1]})"
            : $"string.sub({r}, {a[0]} + 1)");
        Register("String", "Contains", (r, a, _) => $"(string.find({r}, {a[0]}, 1, true) ~= nil)");
        Register("String", "StartsWith", (r, a, _) => $"(string.sub({r}, 1, #{a[0]}) == {a[0]})");
        Register("String", "EndsWith", (r, a, _) => $"(string.sub({r}, -#{a[0]}) == {a[0]})");
        Register("String", "ToLower", (r, a, _) => $"string.lower({r})");
        Register("String", "ToUpper", (r, a, _) => $"string.upper({r})");
        Register("String", "Trim", (r, a, _) => "string.match(" + r + ", \"^%s*(.-)%s*$\")");
        Register("String", "TrimStart", (r, a, _) => "string.match(" + r + ", \"^%s*(.*)\")");
        Register("String", "TrimEnd", (r, a, _) => "string.match(" + r + ", \"(.-)%s*$\")");
        Register("String", "Replace", (r, a, _) => $"string.gsub({r}, {a[0]}, {a[1]})");
        Register("String", "Split", (r, a, _) => $"string.split({r}, {a[0]})");
        Register("String", "IndexOf", (r, a, _) => $"((string.find({r}, {a[0]}, 1, true) or 0) - 1)");
        Register("String", "LastIndexOf", (r, a, _) => $"((select(2, string.find({r}, \".*()\" .. {a[0]})) or 0) - 1)");
        Register("String", "ToString", (r, a, _) => $"tostring({r})");
        Register("String", "Insert", (r, a, _) => $"string.sub({r}, 1, {a[0]}) .. {a[1]} .. string.sub({r}, {a[0]} + 1)");
        Register("String", "Remove", (r, a, _) => a.Length == 2
            ? $"string.sub({r}, 1, {a[0]}) .. string.sub({r}, {a[0]} + {a[1]} + 1)"
            : $"string.sub({r}, 1, {a[0]})");
        Register("String", "PadLeft", (r, a, _) => a.Length == 2
            ? $"string.rep({a[1]}, {a[0]} - #{r}) .. {r}"
            : $"string.rep(\" \", {a[0]} - #{r}) .. {r}");
        Register("String", "PadRight", (r, a, _) => a.Length == 2
            ? $"{r} .. string.rep({a[1]}, {a[0]} - #{r})"
            : $"{r} .. string.rep(\" \", {a[0]} - #{r})");

        // === String static ===
        Register("String", "IsNullOrEmpty", (r, a, _) => $"({a[0]} == nil or {a[0]} == \"\")");
        Register("String", "IsNullOrWhiteSpace", (r, a, _) => $"({a[0]} == nil or string.match({a[0]}, \"^%s*$\") ~= nil)");
        Register("String", "Join", (r, a, _) => $"table.concat({a[1]}, {a[0]})");
        Register("String", "Format", (r, a, _) => $"string.format({string.Join(", ", a)})");
        Register("String", "Concat", (r, a, _) => string.Join(" .. ", a));

        // === StringBuilder ===
        Register("StringBuilder", "Append", (r, a, _) => $"(function() {r} = {r} .. tostring({a[0]}); return {r} end)()");
        Register("StringBuilder", "AppendLine", (r, a, _) => a.Length > 0
            ? $"(function() {r} = {r} .. tostring({a[0]}) .. \"\\n\"; return {r} end)()"
            : $"(function() {r} = {r} .. \"\\n\"; return {r} end)()");
        Register("StringBuilder", "ToString", (r, a, _) => r);
        Register("StringBuilder", "Clear", (r, a, _) => $"(function() {r} = \"\" end)()");
        Register("StringBuilder", "Insert", (r, a, _) => $"(function() {r} = string.sub({r}, 1, {a[0]}) .. tostring({a[1]}) .. string.sub({r}, {a[0]} + 1) end)()");

        // === List<T> ===
        Register("List", "Add", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("List", "Insert", (r, a, _) => $"table.insert({r}, {a[0]} + 1, {a[1]})");
        Register("List", "RemoveAt", (r, a, _) => $"table.remove({r}, {a[0]} + 1)");
        Register("List", "Clear", (r, a, _) => $"table.clear({r})");
        Register("List", "Contains", (r, a, _) => $"(table.find({r}, {a[0]}) ~= nil)");
        Register("List", "IndexOf", (r, a, _) => $"((table.find({r}, {a[0]}) or 0) - 1)");
        Register("List", "Sort", (r, a, _) => a.Length > 0 ? $"table.sort({r}, {a[0]})" : $"table.sort({r})");
        Register("List", "ToArray", (r, a, _) => $"table.clone({r})");
        Register("List", "AddRange", (r, a, _) => $"table.move({a[0]}, 1, #{a[0]}, #{r} + 1, {r})");
        Register("List", "Remove", (r, a, _) =>
            $"(function() local __i = table.find({r}, {a[0]}); if __i then table.remove({r}, __i) end end)()");
        Register("List", "Exists", (r, a, _) =>
            $"(function() for _, v in {r} do if {a[0]}(v) then return true end end; return false end)()");
        Register("List", "Find", (r, a, _) =>
            $"(function() for _, v in {r} do if {a[0]}(v) then return v end end; return nil end)()");
        Register("List", "FindIndex", (r, a, _) =>
            $"(function() for i, v in {r} do if {a[0]}(v) then return i - 1 end end; return -1 end)()");
        Register("List", "ForEach", (r, a, _) => $"(function() for _, v in {r} do {a[0]}(v) end end)()");

        // === Dictionary<K,V> ===
        Register("Dictionary", "ContainsKey", (r, a, _) => $"({r}[{a[0]}] ~= nil)");
        Register("Dictionary", "ContainsValue", (r, a, _) =>
            $"(function() for _, v in {r} do if v == {a[0]} then return true end end; return false end)()");
        Register("Dictionary", "Remove", (r, a, _) => $"(function() {r}[{a[0]}] = nil end)()");
        Register("Dictionary", "Clear", (r, a, _) => $"table.clear({r})");

        // === HashSet<T> ===
        Register("HashSet", "Add", (r, a, _) => $"(function() {r}[{a[0]}] = true end)()");
        Register("HashSet", "Remove", (r, a, _) => $"(function() {r}[{a[0]}] = nil end)()");
        Register("HashSet", "Contains", (r, a, _) => $"({r}[{a[0]}] == true)");

        // === Queue<T> / Stack<T> ===
        Register("Queue", "Enqueue", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("Queue", "Dequeue", (r, a, _) => $"table.remove({r}, 1)");
        Register("Queue", "Peek", (r, a, _) => $"{r}[1]");
        Register("Stack", "Push", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("Stack", "Pop", (r, a, _) => $"table.remove({r})");
        Register("Stack", "Peek", (r, a, _) => $"{r}[#{r}]");

        // === Math ===
        Register("Math", "Max", (r, a, _) => $"math.max({a[0]}, {a[1]})");
        Register("Math", "Min", (r, a, _) => $"math.min({a[0]}, {a[1]})");
        Register("Math", "Abs", (r, a, _) => $"math.abs({a[0]})");
        Register("Math", "Floor", (r, a, _) => $"math.floor({a[0]})");
        Register("Math", "Ceiling", (r, a, _) => $"math.ceil({a[0]})");
        Register("Math", "Round", (r, a, _) => $"math.round({a[0]})");
        Register("Math", "Sqrt", (r, a, _) => $"math.sqrt({a[0]})");
        Register("Math", "Pow", (r, a, _) => $"({a[0]} ^ {a[1]})");
        Register("Math", "Log", (r, a, _) => a.Length == 2 ? $"math.log({a[0]}, {a[1]})" : $"math.log({a[0]})");
        Register("Math", "Log10", (r, a, _) => $"math.log({a[0]}, 10)");
        Register("Math", "Sin", (r, a, _) => $"math.sin({a[0]})");
        Register("Math", "Cos", (r, a, _) => $"math.cos({a[0]})");
        Register("Math", "Tan", (r, a, _) => $"math.tan({a[0]})");
        Register("Math", "Asin", (r, a, _) => $"math.asin({a[0]})");
        Register("Math", "Acos", (r, a, _) => $"math.acos({a[0]})");
        Register("Math", "Atan", (r, a, _) => $"math.atan({a[0]})");
        Register("Math", "Atan2", (r, a, _) => $"math.atan2({a[0]}, {a[1]})");
        Register("Math", "Clamp", (r, a, _) => $"math.clamp({a[0]}, {a[1]}, {a[2]})");
        Register("Math", "Sign", (r, a, _) => $"math.sign({a[0]})");
        Register("Math", "Exp", (r, a, _) => $"math.exp({a[0]})");
        Register("Math", "Truncate", (r, a, _) => $"(if {a[0]} >= 0 then math.floor({a[0]}) else math.ceil({a[0]}))");

        // === Convert ===
        Register("Convert", "ToInt32", (r, a, _) => $"math.floor(tonumber({a[0]}))");
        Register("Convert", "ToString", (r, a, _) => $"tostring({a[0]})");
        Register("Convert", "ToDouble", (r, a, _) => $"tonumber({a[0]})");
        Register("Convert", "ToBoolean", (r, a, _) => $"(not not {a[0]})");
        Register("Convert", "ToByte", (r, a, _) => $"math.floor(tonumber({a[0]}))");

        // === Int32/Double/Single static ===
        Register("Int32", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Int32", "TryParse", (r, a, _) => $"(tonumber({a[0]}) ~= nil)");
        Register("Double", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Double", "TryParse", (r, a, _) => $"(tonumber({a[0]}) ~= nil)");
        Register("Single", "Parse", (r, a, _) => $"tonumber({a[0]})");

        // === Char static ===
        Register("Char", "IsLetter", (r, a, _) => $"(string.match(string.char({a[0]}), \"%a\") ~= nil)");
        Register("Char", "IsDigit", (r, a, _) => $"({a[0]} >= 48 and {a[0]} <= 57)");
        Register("Char", "IsLetterOrDigit", (r, a, _) => $"(string.match(string.char({a[0]}), \"%w\") ~= nil)");
        Register("Char", "IsWhiteSpace", (r, a, _) => $"(string.match(string.char({a[0]}), \"%s\") ~= nil)");
        Register("Char", "IsUpper", (r, a, _) => $"(string.match(string.char({a[0]}), \"%u\") ~= nil)");
        Register("Char", "IsLower", (r, a, _) => $"(string.match(string.char({a[0]}), \"%l\") ~= nil)");
        Register("Char", "IsPunctuation", (r, a, _) => $"(string.match(string.char({a[0]}), \"%p\") ~= nil)");
        Register("Char", "ToUpper", (r, a, _) => $"string.byte(string.upper(string.char({a[0]})))");
        Register("Char", "ToLower", (r, a, _) => $"string.byte(string.lower(string.char({a[0]})))");
        Register("Char", "ToString", (r, a, _) => $"string.char({a[0]})");

        // === Object ===
        Register("Object", "ToString", (r, a, _) => $"tostring({r})");
        Register("Object", "Equals", (r, a, _) => $"({r} == {a[0]})");
        Register("Object", "GetHashCode", (r, a, _) => $"tostring({r})");
        Register("Object", "GetType", (r, a, _) => $"typeof({r})");

        // === Task ===
        Register("Task", "Run", (r, a, _) => $"task.spawn({a[0]})");
        Register("Task", "Delay", (r, a, _) => $"task.wait({a[0]} / 1000)");
        Register("Task", "FromResult", (r, a, _) => a[0]);

        // === Array ===
        Register("Array", "Copy", (r, a, _) => a.Length >= 5
            ? $"table.move({a[0]}, {a[1]} + 1, {a[1]} + {a[4]}, {a[3]} + 1, {a[2]})"
            : $"table.move({a[0]}, 1, {a[2]}, 1, {a[1]})");
        Register("Array", "Sort", (r, a, _) => $"table.sort({a[0]})");
        Register("Array", "Resize", (r, a, _) => $"-- Array.Resize not directly supported");

        // === Enumerable (LINQ) — static methods ===
        Register("Enumerable", "Range", (r, a, _) => $"__rt.range({a[0]}, {a[1]})");
        Register("Enumerable", "Repeat", (r, a, _) => $"__rt.repeat_({a[0]}, {a[1]})");
        Register("Enumerable", "Empty", (r, a, _) => "{}");

        // === LINQ extension methods (instance-style calls resolved by Roslyn) ===
        // Where/Select/First/etc. come through as calls on Enumerable but receiver is the collection
        RegisterLinq("Where", (r, a) => $"__rt.where({r}, {a[0]})");
        RegisterLinq("Select", (r, a) => $"__rt.select({r}, {a[0]})");
        RegisterLinq("SelectMany", (r, a) => $"__rt.selectMany({r}, {a[0]})");
        RegisterLinq("First", (r, a) => a.Length > 0 ? $"__rt.first({r}, {a[0]})" : $"__rt.first({r})");
        RegisterLinq("FirstOrDefault", (r, a) => a.Length > 0 ? $"__rt.firstOrDefault({r}, {a[0]})" : $"__rt.firstOrDefault({r})");
        RegisterLinq("Last", (r, a) => a.Length > 0 ? $"__rt.last({r}, {a[0]})" : $"__rt.last({r})");
        RegisterLinq("LastOrDefault", (r, a) => a.Length > 0 ? $"__rt.lastOrDefault({r}, {a[0]})" : $"__rt.lastOrDefault({r})");
        RegisterLinq("Single", (r, a) => a.Length > 0 ? $"__rt.single({r}, {a[0]})" : $"__rt.single({r})");
        RegisterLinq("SingleOrDefault", (r, a) => a.Length > 0 ? $"__rt.singleOrDefault({r}, {a[0]})" : $"__rt.singleOrDefault({r})");
        RegisterLinq("Any", (r, a) => a.Length > 0 ? $"__rt.any({r}, {a[0]})" : $"__rt.any({r})");
        RegisterLinq("All", (r, a) => $"__rt.all({r}, {a[0]})");
        RegisterLinq("Count", (r, a) => a.Length > 0 ? $"__rt.count({r}, {a[0]})" : $"__rt.count({r})");
        RegisterLinq("Sum", (r, a) => a.Length > 0 ? $"__rt.sum({r}, {a[0]})" : $"__rt.sum({r})");
        RegisterLinq("Min", (r, a) => a.Length > 0 ? $"__rt.min({r}, {a[0]})" : $"__rt.min({r})");
        RegisterLinq("Max", (r, a) => a.Length > 0 ? $"__rt.max({r}, {a[0]})" : $"__rt.max({r})");
        RegisterLinq("Average", (r, a) => a.Length > 0 ? $"__rt.average({r}, {a[0]})" : $"__rt.average({r})");
        RegisterLinq("OrderBy", (r, a) => $"__rt.orderBy({r}, {a[0]})");
        RegisterLinq("OrderByDescending", (r, a) => $"__rt.orderByDescending({r}, {a[0]})");
        RegisterLinq("ThenBy", (r, a) => $"__rt.thenBy({r}, {a[0]})");
        RegisterLinq("ThenByDescending", (r, a) => $"__rt.thenByDescending({r}, {a[0]})");
        RegisterLinq("GroupBy", (r, a) => $"__rt.groupBy({r}, {a[0]})");
        RegisterLinq("Distinct", (r, a) => $"__rt.distinct({r})");
        RegisterLinq("Zip", (r, a) => a.Length > 1 ? $"__rt.zip({r}, {a[0]}, {a[1]})" : $"__rt.zip({r}, {a[0]})");
        RegisterLinq("Aggregate", (r, a) => a.Length > 1 ? $"__rt.aggregate({r}, {a[0]}, {a[1]})" : $"__rt.aggregate({r}, nil, {a[0]})");
        RegisterLinq("Take", (r, a) => $"__rt.take({r}, {a[0]})");
        RegisterLinq("Skip", (r, a) => $"__rt.skip({r}, {a[0]})");
        RegisterLinq("TakeWhile", (r, a) => $"__rt.takeWhile({r}, {a[0]})");
        RegisterLinq("SkipWhile", (r, a) => $"__rt.skipWhile({r}, {a[0]})");
        RegisterLinq("Concat", (r, a) => $"__rt.concat({r}, {a[0]})");
        RegisterLinq("Except", (r, a) => $"__rt.except({r}, {a[0]})");
        RegisterLinq("Intersect", (r, a) => $"__rt.intersect({r}, {a[0]})");
        RegisterLinq("Union", (r, a) => $"__rt.union({r}, {a[0]})");
        RegisterLinq("SequenceEqual", (r, a) => $"__rt.sequenceEqual({r}, {a[0]})");
        RegisterLinq("Contains", (r, a) => $"__rt.contains({r}, {a[0]})");
        RegisterLinq("ToDictionary", (r, a) => a.Length > 1 ? $"__rt.toDictionary({r}, {a[0]}, {a[1]})" : $"__rt.toDictionary({r}, {a[0]})");
        RegisterLinq("ToLookup", (r, a) => $"__rt.toLookup({r}, {a[0]})");
        RegisterLinq("ToHashSet", (r, a) => $"__rt.toHashSet({r})");
        RegisterLinq("ToList", (r, a) => $"table.clone({r})");
        RegisterLinq("ToArray", (r, a) => $"table.clone({r})");
        RegisterLinq("Reverse", (r, a) => $"(function() local __t = table.clone({r}); RT.reverseInPlace(__t); return __t end)()");

        // === List methods that need runtime ===
        Register("List", "RemoveAll", (r, a, _) => $"__rt.removeAll({r}, {a[0]})");
        Register("List", "RemoveRange", (r, a, _) => $"__rt.removeRange({r}, {a[0]}, {a[1]})");
        Register("List", "GetRange", (r, a, _) => $"__rt.getRange({r}, {a[0]}, {a[1]})");
        Register("List", "Reverse", (r, a, _) => $"__rt.reverseInPlace({r})");

        // === HashSet methods that need runtime ===
        Register("HashSet", "UnionWith", (r, a, _) => $"__rt.unionWith({r}, {a[0]})");
        Register("HashSet", "IntersectWith", (r, a, _) => $"__rt.intersectWith({r}, {a[0]})");
        Register("HashSet", "ExceptWith", (r, a, _) => $"__rt.exceptWith({r}, {a[0]})");
        Register("HashSet", "SymmetricExceptWith", (r, a, _) => $"__rt.symmetricExceptWith({r}, {a[0]})");
        Register("HashSet", "IsSubsetOf", (r, a, _) => $"__rt.isSubsetOf({r}, {a[0]})");
        Register("HashSet", "IsSupersetOf", (r, a, _) => $"__rt.isSupersetOf({r}, {a[0]})");
        Register("HashSet", "Overlaps", (r, a, _) => $"__rt.overlaps({r}, {a[0]})");
        Register("HashSet", "SetEquals", (r, a, _) => $"__rt.setEquals({r}, {a[0]})");

        // === Dictionary methods that need runtime ===
        Register("Dictionary", "TryGetValue", (r, a, _) => $"__rt.tryGetValue({r}, {a[0]})");

        // === Stopwatch ===
        Register("Stopwatch", "StartNew", (r, a, _) => $"__rt.Stopwatch.StartNew()");
        Register("Stopwatch", "Start", (r, a, _) => $"__rt.Stopwatch.Start({r})");
        Register("Stopwatch", "Stop", (r, a, _) => $"__rt.Stopwatch.Stop({r})");
        Register("Stopwatch", "Reset", (r, a, _) => $"__rt.Stopwatch.Reset({r})");
        Register("Stopwatch", "Restart", (r, a, _) => $"__rt.Stopwatch.Restart({r})");

        // === Guid ===
        Register("Guid", "NewGuid", (r, a, _) => $"__rt.newGuid()");
    }

    private static void RegisterLinq(string methodName, Func<string, string[], string> rewriter)
    {
        // LINQ extension methods are resolved by Roslyn as Enumerable.MethodName
        // but called as receiver.MethodName(args) — receiver is the collection
        Register("Enumerable", methodName, (r, a, _) => rewriter(r, a));
    }
}
