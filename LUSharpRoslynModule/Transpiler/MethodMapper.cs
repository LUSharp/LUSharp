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

        // === Enumerable (LINQ) — maps to runtime helpers ===
        Register("Enumerable", "Range", (r, a, _) =>
            $"(function() local __t = {{}}; for __i = {a[0]}, {a[0]} + {a[1]} - 1 do table.insert(__t, __i) end; return __t end)()");
        Register("Enumerable", "Repeat", (r, a, _) =>
            $"(function() local __t = {{}}; for __i = 1, {a[1]} do table.insert(__t, {a[0]}) end; return __t end)()");
        Register("Enumerable", "Empty", (r, a, _) => "{}");
    }
}
