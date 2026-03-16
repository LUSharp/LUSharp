# Runtime Features Plan — Luau Polyfills for .NET BCL

## Context

The Roslyn→Luau transpiler (LUSharpRoslynModule) has reached the limit of compile-time mappings. The remaining .NET patterns require runtime polyfills written in Luau. This plan covers:
1. New runtime helpers needed (Luau code in LUSharpRuntime.lua)
2. Emitter changes to wire them up
3. Priority based on how many transpiled files would benefit

## Current State

- **LUSharpRuntime.lua**: 868 lines, 80 functions (LINQ, collections, Stopwatch, Guid, async)
- **Newtonsoft output**: 229 OK, 0 failed, 11 skipped, 38K lines Luau
- **Remaining .NET leaks**: confined to 6-7 reflection-heavy files (Expression trees, IL emit, Encoding)

---

## Phase 1: Type Reflection Runtime (typeof polyfill)

**Impact:** 100+ sites across many files use typeof(x).Name, typeof(x).FullName, IsAssignableFrom, IsSubclassOf

### RT.Type — Lightweight type metadata registry

```luau
-- Type registry: maps type name → metadata table
RT._typeRegistry = {}

function RT.registerType(name: string, meta: {
    Name: string,
    FullName: string,
    BaseType: string?,
    IsValueType: boolean?,
    Interfaces: {string}?,
})
    RT._typeRegistry[name] = meta
end

function RT.getType(value: any): RT.TypeInfo
    local t = typeof(value)
    if t == "table" then
        -- Check metatable for __type field
        local mt = getmetatable(value)
        if mt and mt.__type then
            return RT._typeRegistry[mt.__type] or { Name = mt.__type, FullName = mt.__type }
        end
    end
    return { Name = t, FullName = t, IsValueType = (t ~= "table") }
end

function RT.isAssignableFrom(baseType: string, derivedType: string): boolean
    -- Walk base type chain
    local current = derivedType
    while current do
        if current == baseType then return true end
        local info = RT._typeRegistry[current]
        if info and info.Interfaces then
            for _, iface in info.Interfaces do
                if iface == baseType then return true end
            end
        end
        current = info and info.BaseType
    end
    return false
end

function RT.isSubclassOf(derivedType: string, baseType: string): boolean
    return RT.isAssignableFrom(baseType, derivedType) and derivedType ~= baseType
end
```

**Emitter changes:**
- `typeof(x).Name` → `RT.getType(x).Name` (already mostly done; wire to runtime)
- `typeof(x).IsSubclassOf(T)` → `RT.isSubclassOf(typeof(x), "T")`
- `typeof(x).IsAssignableFrom(T)` → `RT.isAssignableFrom("T", typeof(x))`
- Each class emission adds: `RT.registerType("ClassName", { Name = "ClassName", BaseType = "Base", ... })`

### RT.TypeCode — Runtime type code lookup

```luau
function RT.getTypeCode(value: any): number
    local t = typeof(value)
    if t == "nil" then return 0 end -- Empty
    if t == "boolean" then return 3 end -- Boolean
    if t == "number" then
        if value == math.floor(value) then return 9 end -- Int32
        return 14 end -- Double
    if t == "string" then return 18 end -- String
    return 1 -- Object
end
```

---

## Phase 2: Encoding/Buffer Runtime

**Impact:** 8 sites in BsonReader/BsonBinaryWriter

### RT.Encoding — UTF-8 encode/decode using Luau buffer API

```luau
RT.Encoding = {}
RT.Encoding.UTF8 = {}

function RT.Encoding.UTF8.GetBytes(s: string): buffer
    local buf = buffer.create(#s)
    buffer.writestring(buf, 0, s)
    return buf
end

function RT.Encoding.UTF8.GetByteCount(s: string): number
    return #s  -- UTF-8 byte count = string length in Luau
end

function RT.Encoding.UTF8.GetChars(bytes: buffer, offset: number, count: number, chars: buffer?, charsOffset: number?): number
    local s = buffer.readstring(bytes, offset, count)
    return #s  -- Return char count (UTF-8 chars ≈ bytes for ASCII)
end

function RT.Encoding.UTF8.GetString(bytes: buffer, offset: number?, count: number?): string
    offset = offset or 0
    count = count or buffer.len(bytes)
    return buffer.readstring(bytes, offset, count)
end

function RT.Encoding.UTF8.GetMaxCharCount(byteCount: number): number
    return byteCount
end
```

**Emitter changes:**
- `Encoding.UTF8.GetBytes(s)` → `RT.Encoding.UTF8.GetBytes(s)`
- `Encoding.GetByteCount(s)` → `#s` (inline) or `RT.Encoding.UTF8.GetByteCount(s)`
- `Encoding.UTF8.GetChars(...)` → `RT.Encoding.UTF8.GetChars(...)`

---

## Phase 3: Expression Tree Stubs

**Impact:** 74 sites in 3 files (DynamicProxyMetaObject, DynamicUtils, ExpressionReflectionDelegateFactory)

These files fundamentally cannot work without a full expression tree runtime. Instead of polyfilling the entire LINQ Expression API, emit these files with `-- UNSUPPORTED` headers and provide stub types that won't crash on construction:

```luau
RT.Expression = {}

function RT.Expression.Constant(value: any, type: any?): any
    return { NodeType = 9, Value = value, Type = type }
end

function RT.Expression.Convert(expr: any, type: any): any
    return { NodeType = 10, Operand = expr, Type = type }
end

function RT.Expression.Call(method: any, args: {any}?): any
    return { NodeType = 6, Method = method, Arguments = args or {} }
end

function RT.Expression.New(ctor: any, args: {any}?): any
    return { NodeType = 31, Constructor = ctor, Arguments = args or {} }
end

function RT.Expression.Parameter(type: any, name: string?): any
    return { NodeType = 38, Type = type, Name = name }
end

function RT.Expression.Assign(left: any, right: any): any
    return { NodeType = 46, Left = left, Right = right }
end

function RT.Expression.ArrayIndex(array: any, index: any): any
    return { NodeType = 5, Array = array, Index = index }
end

function RT.Expression.Variable(type: any, name: string?): any
    return RT.Expression.Parameter(type, name)
end

function RT.Expression.Block(variables: {any}?, expressions: {any}): any
    return { NodeType = 47, Variables = variables or {}, Expressions = expressions }
end

function RT.Expression.Lambda(body: any, params: {any}?): any
    return { NodeType = 18, Body = body, Parameters = params or {} }
end

function RT.Expression.Condition(test: any, ifTrue: any, ifFalse: any): any
    return { NodeType = 8, Test = test, IfTrue = ifTrue, IfFalse = ifFalse }
end
```

**This enables the 3 reflection files to at least construct expression tree objects without crashing**, even though `Expression.Compile()` won't produce executable code. This is acceptable because:
1. Newtonsoft uses these as optimization paths — it falls back to slower reflection when expressions aren't available
2. The LateBoundReflectionDelegateFactory serves as the fallback

---

## Phase 4: ILGenerator Stubs

**Impact:** 64 sites in 2 files (DynamicReflectionDelegateFactory, ILGeneratorExtensions)

Similar to expressions — these are IL code generation and can't work in Luau. Provide no-op stubs:

```luau
RT.ILGenerator = {}
function RT.ILGenerator.new(): any
    return { _instructions = {} }
end
function RT.ILGenerator.Emit(self: any, opcode: any, ...) end
function RT.ILGenerator.DeclareLocal(self: any, type: any) return {} end
function RT.ILGenerator.DefineLabel(self: any) return {} end
function RT.ILGenerator.MarkLabel(self: any, label: any) end

RT.DynamicMethod = {}
function RT.DynamicMethod.new(name: string, returnType: any, paramTypes: {any}?, owner: any?): any
    return { Name = name, _gen = RT.ILGenerator.new() }
end
function RT.DynamicMethod.GetILGenerator(self: any) return self._gen end
function RT.DynamicMethod.CreateDelegate(self: any, delegateType: any)
    return function() end  -- Return no-op function
end
```

---

## Phase 5: Advanced Collection Runtime

**Impact:** Broad — any transpiled code using SortedDictionary, LinkedList, ConcurrentDictionary

```luau
-- SortedDictionary: maintain sorted key order
function RT.sortedDictionary()
    return { _data = {}, _keys = {} }
end

-- LinkedList: doubly-linked list with O(1) insert/remove
function RT.linkedList()
    return { _head = nil, _tail = nil, _count = 0 }
end

function RT.linkedListAddLast(list, value)
    local node = { Value = value, Next = nil, Prev = list._tail }
    if list._tail then list._tail.Next = node end
    list._tail = node
    if not list._head then list._head = node end
    list._count += 1
    return node
end
```

---

## Phase 6: Regex Runtime (string pattern matching)

**Impact:** 5-10 sites using Regex.Match, Regex.Replace, Regex.IsMatch

```luau
RT.Regex = {}

function RT.Regex.new(pattern: string, options: number?): any
    -- Convert basic .NET regex to Lua pattern where possible
    -- Complex regex features won't convert — emit warning
    return { Pattern = pattern, Options = options or 0 }
end

function RT.Regex.IsMatch(input: string, pattern: string): boolean
    return string.find(input, pattern) ~= nil
end

function RT.Regex.Match(input: string, pattern: string): any
    local s, e = string.find(input, pattern)
    if s then
        return { Success = true, Value = string.sub(input, s, e), Index = s - 1 }
    end
    return { Success = false, Value = "", Index = -1 }
end

function RT.Regex.Replace(input: string, pattern: string, replacement: string): string
    return string.gsub(input, pattern, replacement)
end
```

Note: .NET regex and Lua patterns have different syntax. A full regex engine would require a significant library. For common patterns (simple character classes, anchors), direct Lua pattern mapping works. Complex patterns (.NET-specific features like lookahead, named groups) would need a dedicated regex library.

---

## Implementation Order

| Phase | Effort | Impact | Files Fixed |
|-------|--------|--------|-------------|
| 1. Type Reflection | Medium | High | ~20 files (typeof patterns) |
| 2. Encoding/Buffer | Small | Low | 2 files (Bson) |
| 3. Expression Stubs | Medium | Medium | 3 files (no more crashes) |
| 4. ILGenerator Stubs | Small | Low | 2 files (no more crashes) |
| 5. Advanced Collections | Large | Medium | Future projects |
| 6. Regex Runtime | Medium | Medium | 5-10 files |

## Verification

After each phase:
- `dotnet run --project LUSharpRoslynModule -- reference self-emit` → 12/12
- `dotnet run --project LUSharpRoslynModule -- reference transpiler` → 13/13
- Retranspile Newtonsoft → 229/0/11
- Grep for reduced .NET leaks
