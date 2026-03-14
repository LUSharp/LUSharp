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
        // Preprocess: wrap char-typed args with string.char() for String methods
        // so that methods like IndexOf(char) pass a string to string.find, not a number
        if (method.ContainingType?.SpecialType == SpecialType.System_String)
        {
            for (int i = 0; i < method.Parameters.Length && i < args.Length; i++)
            {
                if (method.Parameters[i].Type.SpecialType == SpecialType.System_Char)
                    args[i] = $"string.char({args[i]})";
            }
        }

        var typeName = method.ContainingType?.Name ?? "";
        // Try exact type match first
        if (_map.TryGetValue((typeName, method.Name), out var rewriter))
        {
            try { return rewriter(receiver, args, receiverType); }
            catch (IndexOutOfRangeException) { return null; } // args count mismatch
        }
        // Try base types — skip for static methods since static methods don't inherit
        // (avoids e.g. JsonConvert.ToString(value, ...) matching Object.ToString())
        if (!method.IsStatic)
        {
            var current = method.ContainingType?.BaseType;
            while (current != null)
            {
                if (_map.TryGetValue((current.Name, method.Name), out rewriter))
                {
                    try { return rewriter(receiver, args, receiverType); }
                    catch (IndexOutOfRangeException) { return null; }
                }
                current = current.BaseType;
            }
        }
        // Try interfaces (for LINQ extension methods on IEnumerable, IList, etc.)
        if (method.IsExtensionMethod && method.ReducedFrom != null)
        {
            var containingName = method.ReducedFrom.ContainingType?.Name ?? "";
            if (_map.TryGetValue((containingName, method.Name), out rewriter))
            {
                try { return rewriter(receiver, args, receiverType); }
                catch (IndexOutOfRangeException) { return null; }
            }
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
        Register("String", "IndexOfAny", (r, a, _) => $"__rt.stringIndexOfAny({r}, {a[0]})");
        Register("String", "LastIndexOf", (r, a, _) => $"((select(2, string.find({r}, \".*()\" .. {a[0]})) or 0) - 1)");
        Register("String", "ToString", (r, a, _) => $"tostring({r})");
        Register("String", "ToCharArray", (r, a, _) => $"(function() local __c = {{}}; for i = 1, #{r} do __c[i] = string.byte({r}, i) end; return __c end)()");
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
        Register("String", "Format", (r, a, _) =>
        {
            // First arg is the format string; convert {0}/{1}/... → %s at compile time if literal
            var fmt = a[0];
            if (fmt.StartsWith("\"") && fmt.EndsWith("\""))
                fmt = System.Text.RegularExpressions.Regex.Replace(fmt, @"\{(\d+)\}", "%s");
            var rest = a.Skip(1).Select(arg => $"tostring({arg})");
            var restStr = string.Join(", ", rest);
            return restStr.Length > 0
                ? $"string.format({fmt}, {restStr})"
                : fmt;
        });
        Register("String", "Concat", (r, a, _) => string.Join(" .. ", a));

        // StringUtils.FormatWith (Newtonsoft extension on string)
        // "text {0} {1}".FormatWith(culture, arg0, arg1) → string.format("text %s %s", tostring(arg0), tostring(arg1))
        Register("StringUtils", "FormatWith", (r, a, _) =>
        {
            // a[0] is CultureInfo (skip), a[1..] are the format args
            var formatArgs = a.Skip(1).Select(arg => $"tostring({arg})");
            var joinedArgs = string.Join(", ", formatArgs);
            // If receiver is a string literal, convert {0}/{1}/... → %s at compile time
            if (r.StartsWith("\"") && r.EndsWith("\""))
            {
                var converted = System.Text.RegularExpressions.Regex.Replace(r, @"\{(\d+)\}", "%s");
                return joinedArgs.Length > 0
                    ? $"string.format({converted}, {joinedArgs})"
                    : converted;
            }
            // Dynamic format string — must use runtime gsub
            return $"string.format(string.gsub({r}, \"{{%d}}\", \"%%s\"), {joinedArgs})";
        });

        // === StringBuilder ===
        Register("StringBuilder", "Append", (r, a, _) => $"(function() {r} = {r} .. tostring({a[0]}); return {r} end)()");
        Register("StringBuilder", "AppendLine", (r, a, _) => a.Length > 0
            ? $"(function() {r} = {r} .. tostring({a[0]}) .. \"\\n\"; return {r} end)()"
            : $"(function() {r} = {r} .. \"\\n\"; return {r} end)()");
        Register("StringBuilder", "ToString", (r, a, _) => r);
        Register("StringBuilder", "Clear", (r, a, _) => $"(function() {r} = \"\" end)()");
        Register("StringBuilder", "Insert", (r, a, _) => $"(function() {r} = string.sub({r}, 1, {a[0]}) .. tostring({a[1]}) .. string.sub({r}, {a[0]} + 1) end)()");

        // === List<T> / IList<T> ===
        Register("List", "Add", (r, a, _) => $"table.insert({r}, {a[0]})");
        Register("List", "Insert", (r, a, _) => $"table.insert({r}, {a[0]} + 1, {a[1]})");
        Register("List", "RemoveAt", (r, a, _) => $"table.remove({r}, {a[0]} + 1)");
        // IList<T> may be a custom implementation (e.g. JPropertyList), not a plain table.
        // Use runtime helpers that check for method dispatch before falling back to table ops.
        Register("IList", "Add", (r, a, _) => $"__rt.ilistAdd({r}, {a[0]})");
        Register("IList", "Insert", (r, a, _) => $"__rt.ilistInsert({r}, {a[0]}, {a[1]})");
        Register("IList", "RemoveAt", (r, a, _) => $"__rt.ilistRemoveAt({r}, {a[0]})");
        Register("IList", "Contains", (r, a, _) => $"__rt.ilistContains({r}, {a[0]})");
        Register("IList", "IndexOf", (r, a, _) => $"__rt.ilistIndexOf({r}, {a[0]})");
        Register("IList", "Clear", (r, a, _) => $"__rt.ilistClear({r})");
        Register("List", "Clear", (r, a, _) => $"table.clear({r})");
        Register("List", "Contains", (r, a, _) => $"(table.find({r}, {a[0]}) ~= nil)");
        Register("List", "IndexOf", (r, a, _) => $"((table.find({r}, {a[0]}) or 0) - 1)");
        Register("List", "Sort", (r, a, _) => a.Length > 0 ? $"table.sort({r}, {a[0]})" : $"table.sort({r})");
        Register("List", "ToArray", (r, a, _) => $"table.clone({r})");
        Register("List", "AddRange", (r, a, _) => $"table.move({a[0]}, 1, #{a[0]}, #{r} + 1, {r})");
        Register("List", "Remove", (r, a, _) =>
            $"(function() local __i = table.find({r}, {a[0]}); if __i then table.remove({r}, __i) end end)()");
        Register("List", "Exists", (r, a, _) =>
            $"(function() local __pred = {a[0]}; for _, v in {r} do if __pred(v) then return true end end; return false end)()");
        Register("List", "Find", (r, a, _) =>
            $"(function() local __pred = {a[0]}; for _, v in {r} do if __pred(v) then return v end end; return nil end)()");
        Register("List", "FindIndex", (r, a, _) =>
            $"(function() local __pred = {a[0]}; for i, v in {r} do if __pred(v) then return i - 1 end end; return -1 end)()");
        Register("List", "ForEach", (r, a, _) => $"(function() for _, v in {r} do ({a[0]})(v) end end)()");

        // === Dictionary<K,V> / ConcurrentDictionary<K,V> ===
        Register("Dictionary", "ContainsKey", (r, a, _) => $"({r}[{a[0]}] ~= nil)");
        Register("Dictionary", "ContainsValue", (r, a, _) =>
            $"(function() for _, v in {r} do if v == {a[0]} then return true end end; return false end)()");
        Register("Dictionary", "Remove", (r, a, _) => $"(function() {r}[{a[0]}] = nil end)()");
        Register("Dictionary", "Clear", (r, a, _) => $"table.clear({r})");
        // ConcurrentDictionary.GetOrAdd(key, factory) → table lookup with lazy init
        Register("ConcurrentDictionary", "GetOrAdd", (r, a, _) =>
            $"(function() local __v = {r}[{a[0]}]; if __v == nil then __v = ({a[1]})({a[0]}); {r}[{a[0]}] = __v end; return __v end)()");
        Register("ConcurrentDictionary", "TryGetValue", (r, a, _) => $"__rt.tryGetValue({r}, {a[0]})");
        Register("ConcurrentDictionary", "ContainsKey", (r, a, _) => $"({r}[{a[0]}] ~= nil)");

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

        // === Double/Single static ===
        Register("Double", "IsNaN", (r, a, _) => $"({a[0]} ~= {a[0]})");
        Register("Double", "IsInfinity", (r, a, _) => $"({a[0]} == math.huge or {a[0]} == -math.huge)");
        Register("Double", "IsPositiveInfinity", (r, a, _) => $"({a[0]} == math.huge)");
        Register("Double", "IsNegativeInfinity", (r, a, _) => $"({a[0]} == -math.huge)");
        Register("Single", "IsNaN", (r, a, _) => $"({a[0]} ~= {a[0]})");
        Register("Single", "IsInfinity", (r, a, _) => $"({a[0]} == math.huge or {a[0]} == -math.huge)");

        // === String comparison methods ===
        // Instance: s.Equals(other) or s.Equals(other, StringComparison) → (s == other)
        // Static: String.Equals(a, b) or String.Equals(a, b, StringComparison) → (a == b)
        Register("String", "Equals", (r, a, _) =>
        {
            // 3 args is always static: String.Equals(a, b, comparison)
            if (a.Length >= 3) return $"({a[0]} == {a[1]})";
            // 2 args: could be static String.Equals(a, b) or instance s.Equals(other, comparison)
            // If a[1] parses as a number, it's a StringComparison enum → instance call
            if (a.Length == 2 && int.TryParse(a[1], out var _unused))
                return $"({r} == {a[0]})";
            // Otherwise 2 args is static String.Equals(a, b)
            return a.Length >= 2
                ? $"({a[0]} == {a[1]})"
                : $"({r} == {a[0]})";
        });
        Register("String", "Compare", (r, a, _) => $"(if {a[0]} < {a[1]} then -1 elseif {a[0]} > {a[1]} then 1 else 0)");
        Register("String", "CompareOrdinal", (r, a, _) => $"(if {a[0]} < {a[1]} then -1 elseif {a[0]} > {a[1]} then 1 else 0)");

        // === Char invariant methods ===
        Register("Char", "ToLowerInvariant", (r, a, _) => $"string.byte(string.lower(string.char({a[0]})))");
        Register("Char", "ToUpperInvariant", (r, a, _) => $"string.byte(string.upper(string.char({a[0]})))");
        Register("Char", "IsNumber", (r, a, _) => $"({a[0]} >= 48 and {a[0]} <= 57)");
        Register("Char", "IsSeparator", (r, a, _) => $"(string.match(string.char({a[0]}), \"%s\") ~= nil)");
        Register("Char", "IsControl", (r, a, _) => $"(string.match(string.char({a[0]}), \"%c\") ~= nil)");
        Register("Char", "IsSurrogate", (r, a, _) => $"({a[0]} >= 0xD800 and {a[0]} <= 0xDFFF)");
        Register("Char", "IsHighSurrogate", (r, a, _) => $"({a[0]} >= 0xD800 and {a[0]} <= 0xDBFF)");
        Register("Char", "IsLowSurrogate", (r, a, _) => $"({a[0]} >= 0xDC00 and {a[0]} <= 0xDFFF)");

        // === Boolean ===
        Register("Boolean", "TryParse", (r, a, _) => $"__rt.tryParse_bool({a[0]})");

        // === Decimal ===
        Register("Decimal", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Decimal", "TryParse", (r, a, _) => $"__rt.tryParse_double({a[0]})");

        // === Int64/Long ===
        Register("Int64", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Int64", "TryParse", (r, a, _) => $"__rt.tryParse_int({a[0]})");

        // === Convert ===
        Register("Convert", "ToInt32", (r, a, _) => $"math.floor(tonumber({a[0]}))");
        Register("Convert", "ToString", (r, a, _) => $"tostring({a[0]})");
        Register("Convert", "ToDouble", (r, a, _) => $"tonumber({a[0]})");
        Register("Convert", "ToBoolean", (r, a, _) => $"(not not {a[0]})");
        Register("Convert", "ToByte", (r, a, _) => $"math.floor(tonumber({a[0]}))");

        // === Int32/Double/Single static ===
        Register("Int32", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Int32", "TryParse", (r, a, _) => $"__rt.tryParse_int({a[0]})");
        Register("Double", "Parse", (r, a, _) => $"tonumber({a[0]})");
        Register("Double", "TryParse", (r, a, _) => $"__rt.tryParse_double({a[0]})");
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
        Register("Char", "ToString", (r, a, _) =>
        {
            // Instance: value.ToString() or value.ToString(cultureInfo) → string.char(value)
            // Static: Char.ToString(value) → string.char(value)
            if (a.Length == 0) return $"string.char({r})";
            // If receiver is the type name itself, it's a static call
            if (r is "Char" or "char") return $"string.char({a[0]})";
            // Instance call with extra args (e.g. CultureInfo) — use receiver
            return $"string.char({r})";
        });

        // === Object ===
        Register("Object", "ToString", (r, a, _) => $"tostring({r})");
        Register("Object", "Equals", (r, a, _) => a.Length >= 2 ? $"({a[0]} == {a[1]})" : $"({r} == {a[0]})");
        Register("Object", "GetHashCode", (r, a, _) => $"tostring({r})");
        Register("Object", "GetType", (r, a, _) =>
        {
            if (a.Length > 0) throw new IndexOutOfRangeException(); // not parameterless .GetType()
            return $"__rt.getType({r})";
        });

        // === Type / Assembly (reflection) ===
        // Type.GetType(name) and Assembly.GetType(name) are reflection calls — emit as the name argument
        Register("Type", "GetType", (r, a, _) => a.Length >= 1 ? a[0] : $"typeof({r})");
        Register("Assembly", "GetType", (r, a, _) => a.Length >= 1 ? a[0] : $"typeof({r})");

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

        // === .NET Reflection stubs (Type, FieldInfo, MemberInfo) ===
        // These enable enum reflection (e.g. Enum.GetNames, Type.GetField) in Luau
        Register("Type", "GetField", (r, a, _) => $"__rt.Type_GetField({r}, {a[0]})");
        Register("Type", "IsDefined", (r, a, _) => "false");
        Register("Type", "GetCustomAttributes", (r, a, _) => "{}");
        Register("Type", "GetCustomAttribute", (r, a, _) => "nil");
        Register("Type", "IsAssignableFrom", (r, a, _) => "false");
        Register("Type", "IsSubclassOf", (r, a, _) => "false");
        Register("Type", "IsEnum", (r, a, _) => $"__rt.Type_IsEnum({r})");
        Register("Type", "GetMethods", (r, a, _) => "{}");
        Register("Type", "GetConstructors", (r, a, _) => "{}");
        Register("Type", "GetConstructor", (r, a, _) => "nil");
        Register("Type", "GetProperties", (r, a, _) => "{}");
        Register("Type", "GetFields", (r, a, _) => "{}");
        Register("Type", "GetMethod", (r, a, _) => "nil");
        Register("Type", "GetProperty", (r, a, _) => "nil");
        Register("Type", "GetInterfaces", (r, a, _) => "{}");
        Register("Type", "GetGenericArguments", (r, a, _) => "{}");
        Register("Type", "GetMember", (r, a, _) => "{}");
        Register("Type", "GetGenericTypeDefinition", (r, a, _) => r);
        Register("Type", "MakeGenericType", (r, a, _) => r);
        Register("FieldInfo", "GetValue", (r, a, _) => $"__rt.FieldInfo_GetValue({r})");
        Register("FieldInfo", "GetCustomAttributes", (r, a, _) => "{}");
        Register("MemberInfo", "GetCustomAttributes", (r, a, _) => "{}");
        Register("MemberInfo", "IsDefined", (r, a, _) => "false");

        // === .NET Attribute reflection stubs — always nil in Luau ===
        Register("CachedAttributeGetter", "GetAttribute", (r, a, _) => "nil --[[no attributes]]");
        Register("JsonTypeReflector", "GetCachedAttribute", (r, a, _) => "nil --[[no attributes]]");
        Register("JsonTypeReflector", "GetAttribute", (r, a, _) => "nil --[[no attributes]]");

        // === TypeDescriptor (.NET reflection) — not available in Luau ===
        Register("TypeDescriptor", "GetConverter", (r, a, _) => "nil");
        Register("TypeDescriptor", "GetProperties", (r, a, _) => "{}");

        // === ReflectionUtils (Newtonsoft) — runtime type checks that can't work in Luau ===
        Register("ReflectionUtils", "ImplementsGenericDefinition", (r, a, _) => "false");
        Register("ReflectionUtils", "InheritsGenericDefinition", (r, a, _) => "false");
        Register("ReflectionUtils", "IsNullableType", (r, a, _) => "false");
        Register("ReflectionUtils", "HasDefaultConstructor", (r, a, _) => "true");
        Register("ReflectionUtils", "GetDefaultConstructor", (r, a, _) => "nil");
        Register("ReflectionUtils", "EnsureNotByRefType", (r, a, _) => a.Length > 0 ? a[0] : r);
        Register("ReflectionUtils", "EnsureNotNullableType", (r, a, _) => a.Length > 0 ? a[0] : r);

        // === TypeExtensions (Newtonsoft extension methods on Type) ===
        // These are extension methods resolved by Roslyn as TypeExtensions.MethodName
        // In Luau, types are strings — return sensible defaults
        Register("TypeExtensions", "IsEnum", (r, a, _) => $"__rt.Type_IsEnum({r})");
        Register("TypeExtensions", "IsClass", (r, a, _) => "false");
        Register("TypeExtensions", "IsValueType", (r, a, _) => "false");
        Register("TypeExtensions", "IsPrimitive", (r, a, _) => "false");
        Register("TypeExtensions", "IsAbstract", (r, a, _) => "false");
        Register("TypeExtensions", "IsInterface", (r, a, _) => "false");
        Register("TypeExtensions", "IsGenericType", (r, a, _) => "false");
        Register("TypeExtensions", "IsGenericTypeDefinition", (r, a, _) => "false");
        Register("TypeExtensions", "IsVisible", (r, a, _) => "true");
        Register("TypeExtensions", "IsSealed", (r, a, _) => "false");
        Register("TypeExtensions", "ContainsGenericParameters", (r, a, _) => "false");
        Register("TypeExtensions", "BaseType", (r, a, _) => "nil");
        Register("TypeExtensions", "Assembly", (r, a, _) => "nil");
        Register("TypeExtensions", "GetGenericArguments", (r, a, _) => "{}");
        Register("TypeExtensions", "GetInterfaces", (r, a, _) => "{}");
        Register("TypeExtensions", "IsSubclassOf", (r, a, _) => "false");
        Register("TypeExtensions", "IsAssignableFrom", (r, a, _) => "false");
        Register("TypeExtensions", "IsInstanceOfType", (r, a, _) => "false");
        Register("TypeExtensions", "ImplementInterface", (r, a, _) => "false");
        Register("TypeExtensions", "MemberType", (r, a, _) => "\"Field\"");
        Register("TypeExtensions", "AssignableToTypeName", (r, a, _) => "false");
        Register("TypeExtensions", "AssignableToTypeNameIncludingInterfaces", (r, a, _) => "false");
        Register("TypeExtensions", "GetMethods", (r, a, _) => "{}");
        Register("TypeExtensions", "GetConstructors", (r, a, _) => "{}");
        Register("TypeExtensions", "GetProperties", (r, a, _) => "{}");
        Register("TypeExtensions", "GetFields", (r, a, _) => "{}");
        Register("TypeExtensions", "GetMethod", (r, a, _) => "nil");
        Register("TypeExtensions", "GetConstructor", (r, a, _) => "nil");
        Register("TypeExtensions", "GetProperty", (r, a, _) => "nil");
        Register("TypeExtensions", "GetField", (r, a, _) => "nil");
        Register("TypeExtensions", "GetMember", (r, a, _) => "{}");
        Register("TypeExtensions", "GetBaseDefinition", (r, a, _) => "nil");
        Register("TypeExtensions", "GetGetMethod", (r, a, _) => "nil");
        Register("TypeExtensions", "GetSetMethod", (r, a, _) => "nil");
        Register("TypeExtensions", "IsDefined", (r, a, _) => "false");
        Register("TypeExtensions", "Method", (r, a, _) => "nil");
    }

    private static void RegisterLinq(string methodName, Func<string, string[], string> rewriter)
    {
        // LINQ extension methods are resolved by Roslyn as Enumerable.MethodName
        // but called as receiver.MethodName(args) — receiver is the collection
        Register("Enumerable", methodName, (r, a, _) => rewriter(r, a));
    }
}
