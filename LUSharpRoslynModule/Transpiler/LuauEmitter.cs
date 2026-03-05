using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpRoslynModule.Transpiler;

public class LuauEmitter
{
    private readonly StringBuilder _sb = new();
    private int _indent = 0;

    /// <summary>Luau reserved keywords that cannot be used as identifiers.</summary>
    private static readonly HashSet<string> LuauReservedWords = new(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for",
        "function", "if", "in", "local", "nil", "not", "or", "repeat",
        "return", "then", "true", "until", "while", "continue", "export",
        "type", "typeof"
    };

    /// <summary>Escape a C# identifier if it collides with a Luau reserved word.</summary>
    private static string EscapeIdentifier(string name)
        => LuauReservedWords.Contains(name) ? name + "_" : name;

    /// <summary>
    /// The class name currently being emitted (used to qualify static method calls).
    /// </summary>
    private string? _currentClassName;

    /// <summary>
    /// Whether we are currently emitting inside an instance method/constructor body.
    /// When true, bare identifiers that match instance fields/methods get prefixed with self.
    /// </summary>
    private bool _isInstanceContext;

    /// <summary>
    /// Set of instance field names for the current class (used for implicit self resolution).
    /// </summary>
    private HashSet<string> _instanceFields = new();

    /// <summary>
    /// Set of instance method names for the current class (used for implicit self resolution).
    /// </summary>
    private HashSet<string> _instanceMethods = new();

    /// <summary>
    /// The base class name for the current class (null if no inheritance).
    /// Used to resolve `base.X` calls to `ParentClass.X(self, ...)`.
    /// </summary>
    private string? _baseClassName;

    /// <summary>
    /// Set of nested type names declared inside the current class.
    /// </summary>
    private HashSet<string> _nestedTypeNames = new();

    /// <summary>
    /// Set of parameter names in the current method (prevent self. prefix for these).
    /// </summary>
    private HashSet<string> _currentMethodParams = new();

    /// <summary>
    /// Set of local variable names declared in the current method scope.
    /// </summary>
    private HashSet<string> _currentMethodLocals = new();

    /// <summary>
    /// Map of parameter/local names to their raw C# type strings.
    /// Used to determine whether .Length should emit as # (for array/collection types)
    /// or stay as .Length (for struct/class types with a Length property).
    /// </summary>
    private Dictionary<string, string> _currentMethodParamTypes = new();

    /// <summary>
    /// Pattern variable aliases: maps `is` pattern variables to their subject expressions.
    /// e.g., `m is PropertyInfo p` registers p → m, so references to `p` resolve to `m`.
    /// </summary>
    private Dictionary<string, string> _patternVarAliases = new();

    /// <summary>
    /// When true, all + binary expressions should emit as .. (string concatenation).
    /// Set by the top-level string concat detection in EmitBinary, so inner + nodes
    /// in the same chain also use .. instead of +.
    /// </summary>
    private bool _forceStringConcat = false;

    /// <summary>
    /// Set of const field names for the current class (accessed as ClassName.ConstField).
    /// </summary>
    private HashSet<string> _constFields = new();

    /// <summary>
    /// Map of instance field names to their C# type name (simplified, no namespace).
    /// Used for cross-type method dispatch resolution.
    /// </summary>
    private Dictionary<string, string> _instanceFieldTypes = new();

    /// <summary>
    /// Map from original overloaded method name to disambiguated names.
    /// Used so call sites can resolve the correct overloaded variant.
    /// Key: (OriginalName, ParamCount) → DisambiguatedName
    /// </summary>
    private Dictionary<(string Name, int ParamCount), string> _overloadMap = new();

    /// <summary>
    /// Set of type names referenced as member access (e.g., SyntaxKind.X) that need requires.
    /// Populated during emission, consumed by the orchestrator to insert requires.
    /// </summary>
    public HashSet<string> ReferencedModules { get; } = new();

    /// <summary>
    /// Whether this file requires the LUSharpRuntime module (set when LINQ, dictionary helpers, etc. are used).
    /// </summary>
    public bool NeedsRuntime { get; private set; }

    /// <summary>
    /// Registry of instance fields for each emitted type.
    /// Used to resolve inherited field access in child classes (same-file inheritance).
    /// Key: type name, Value: set of instance field names.
    /// </summary>
    private Dictionary<string, HashSet<string>> _emittedTypeFields = new();

    /// <summary>
    /// Roslyn SemanticModel for type resolution. Null when running without a compilation.
    /// </summary>
    private readonly SemanticModel? _model;

    /// <summary>
    /// Counter for generating unique temp variable names for evaluation order hoisting.
    /// </summary>
    private int _evalTempCounter = 0;

    public LuauEmitter(SemanticModel? model = null)
    {
        _model = model;
    }

    // ── SemanticModel query helpers ──────────────────────────────────────

    private ITypeSymbol? GetExpressionType(ExpressionSyntax expr)
    {
        if (_model == null) return null;
        return _model.GetTypeInfo(expr).Type;
    }

    private ISymbol? GetSymbol(ExpressionSyntax expr)
    {
        if (_model == null) return null;
        return _model.GetSymbolInfo(expr).Symbol;
    }

    private bool IsStringType(ExpressionSyntax expr)
    {
        var type = GetExpressionType(expr);
        return type?.SpecialType == SpecialType.System_String;
    }

    private bool IsEnumType(ExpressionSyntax expr)
    {
        var type = GetExpressionType(expr);
        return type?.TypeKind == TypeKind.Enum;
    }

    /// <summary>
    /// Global overload registry from pre-scan: (TypeName, MethodName, ArgCount) → DisambiguatedName.
    /// Set by the orchestrator before emission for cross-type overload resolution.
    /// </summary>
    public Dictionary<(string TypeName, string MethodName, int ArgCount), string> GlobalOverloadMap { get; set; } = new();

    /// <summary>
    /// Global base class map: ClassName → BaseClassName.
    /// Populated by PreScan across all files, resolves partial class base types.
    /// </summary>
    public Dictionary<string, string> GlobalBaseClassMap { get; set; } = new();

    /// <summary>
    /// When true, individual type emissions will not emit their own return statements.
    /// The orchestrator is responsible for emitting a unified return at the end.
    /// </summary>
    public bool SuppressReturn { get; set; } = false;

    /// <summary>
    /// Track all top-level type names emitted in this file (used for unified return).
    /// </summary>
    public List<string> EmittedTopLevelTypes { get; } = new();

    public string GetOutput() => _sb.ToString();

    public void EmitHeader()
    {
        AppendLine("--!strict");
        AppendLine("-- Auto-generated by LUSharpRoslynModule");
        AppendLine("-- Do not edit manually");
        AppendLine();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Enum emission (unchanged from original)
    // ────────────────────────────────────────────────────────────────────

    public void EmitEnum(EnumDeclarationSyntax enumDecl)
    {
        var name = enumDecl.Identifier.Text;

        bool isFlags = enumDecl.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(a => a.Name.ToString() is "Flags" or "System.Flags" or "FlagsAttribute" or "System.FlagsAttribute");

        // Compute enum member values (auto-increment when no explicit = value)
        var memberValues = new List<(string Name, string Value)>();
        int nextAutoValue = 0;
        foreach (var member in enumDecl.Members)
        {
            var memberName = member.Identifier.Text;
            if (member.EqualsValue != null)
            {
                var explicitValue = member.EqualsValue.Value.ToString();
                // Try to parse for auto-increment tracking
                if (int.TryParse(explicitValue, out var parsed))
                    nextAutoValue = parsed + 1;
                else
                    nextAutoValue++; // expression-based (e.g., 1 << 3), can't track — just bump
                memberValues.Add((memberName, explicitValue));
            }
            else
            {
                memberValues.Add((memberName, nextAutoValue.ToString()));
                nextAutoValue++;
            }
        }

        // Forward enum table
        AppendLine($"local {name} = table.freeze({{");
        _indent++;
        foreach (var (memberName, value) in memberValues)
        {
            AppendLine($"{memberName} = {value},");
        }
        _indent--;
        AppendLine("})");
        AppendLine();

        // Export type
        AppendLine($"export type {name} = number");
        AppendLine();

        // Flags helper
        if (isFlags)
        {
            AppendLine($"local function {name}_HasFlag(value: number, flag: number): boolean");
            _indent++;
            AppendLine("return bit32.band(value, flag) == flag");
            _indent--;
            AppendLine("end");
            AppendLine();
        }

        // Reverse lookup table
        AppendLine($"local {name}_Name: {{ [number]: string }} = table.freeze({{");
        _indent++;
        foreach (var (memberName, value) in memberValues)
        {
            AppendLine($"[{value}] = \"{memberName}\",");
        }
        _indent--;
        AppendLine("})");
        AppendLine();

        // Only emit return for top-level enums, not nested ones
        bool isNested = enumDecl.Parent is ClassDeclarationSyntax
            or StructDeclarationSyntax;
        if (!isNested)
        {
            EmittedTopLevelTypes.Add(name);
            if (!SuppressReturn)
            {
                if (isFlags)
                    AppendLine($"return {{ {name} = {name}, {name}_Name = {name}_Name, {name}_HasFlag = {name}_HasFlag }}");
                else
                    AppendLine($"return {{ {name} = {name}, {name}_Name = {name}_Name }}");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Static class emission (static class → module table)
    // ────────────────────────────────────────────────────────────────────

    public void EmitStaticClass(ClassDeclarationSyntax classDecl)
    {
        var className = classDecl.Identifier.Text;
        _currentClassName = className;
        _isInstanceContext = false;
        var nestedStaticTypeNames = new List<string>();

        AppendLine($"local {className} = {{}}");
        AppendLine();

        // Detect overloaded method names so we can disambiguate them
        var methodNameCounts = new Dictionary<string, int>();
        var methodNameSeen = new Dictionary<string, int>();
        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax m)
            {
                var name = m.Identifier.Text;
                methodNameCounts[name] = methodNameCounts.GetValueOrDefault(name) + 1;
            }
        }

        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                {
                    var name = method.Identifier.Text;
                    string emitName = name;

                    // Disambiguate overloaded methods by appending parameter type info
                    if (methodNameCounts.GetValueOrDefault(name) > 1)
                    {
                        var overloadIndex = methodNameSeen.GetValueOrDefault(name);
                        methodNameSeen[name] = overloadIndex + 1;

                        if (overloadIndex > 0)
                        {
                            // Use first parameter type as suffix
                            var firstParam = method.ParameterList.Parameters.FirstOrDefault();
                            var suffix = firstParam?.Type?.ToString() ?? $"_{overloadIndex}";
                            // Clean up the suffix for Luau identifier
                            suffix = suffix.Replace(".", "_").Replace("<", "_").Replace(">", "_");
                            emitName = $"{name}_{suffix}";
                        }
                    }

                    EmitStaticMethod(className, method, emitName);
                    AppendLine();
                    break;
                }

                case FieldDeclarationSyntax fieldDecl:
                {
                    var luauType = MapTypeNode(fieldDecl.Declaration.Type);
                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.Text;
                        if (variable.Initializer != null)
                        {
                            var value = EmitExpression(variable.Initializer.Value);
                            AppendLine($"{className}.{fieldName} = {value}");
                        }
                        else
                        {
                            AppendLine($"{className}.{fieldName} = nil :: {luauType}");
                        }
                    }
                    break;
                }

                case PropertyDeclarationSyntax propDecl:
                {
                    var propName = propDecl.Identifier.Text;
                    if (propDecl.ExpressionBody != null)
                    {
                        // Expression-bodied property → getter function
                        var expr = EmitExpression(propDecl.ExpressionBody.Expression);
                        AppendLine($"function {className}.get_{propName}(): {MapTypeNode(propDecl.Type)}");
                        _indent++;
                        AppendLine($"return {expr}");
                        _indent--;
                        AppendLine("end");
                        AppendLine();
                    }
                    else if (propDecl.Initializer != null)
                    {
                        var value = EmitExpression(propDecl.Initializer.Value);
                        AppendLine($"{className}.{propName} = {value}");
                    }
                    else
                    {
                        // Auto-property on static class → just a field
                        AppendLine($"{className}.{propName} = nil :: {MapTypeNode(propDecl.Type)}");
                    }
                    break;
                }

                case EnumDeclarationSyntax nestedEnum:
                    nestedStaticTypeNames.Add(nestedEnum.Identifier.Text);
                    EmitEnum(nestedEnum);
                    AppendLine();
                    break;

                case ClassDeclarationSyntax nestedClass:
                {
                    nestedStaticTypeNames.Add(nestedClass.Identifier.Text);
                    var savedClassName = _currentClassName;
                    EmitClass(nestedClass);
                    _currentClassName = savedClassName;
                    break;
                }

                case StructDeclarationSyntax nestedStruct:
                    nestedStaticTypeNames.Add(nestedStruct.Identifier.Text);
                    EmitStruct(nestedStruct);
                    break;

                case InterfaceDeclarationSyntax nestedIface:
                    EmitInterface(nestedIface);
                    break;

                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case EventFieldDeclarationSyntax:
                    // Static class operators/ctors/events — skip silently
                    break;

                default:
                    AppendLine($"-- TODO: unsupported member {member.Kind()}");
                    break;
            }
        }

        // Only emit return for top-level static classes, not nested ones
        bool isNested = classDecl.Parent is ClassDeclarationSyntax
            or StructDeclarationSyntax;
        if (!isNested)
        {
            EmittedTopLevelTypes.Add(className);
            foreach (var nested in nestedStaticTypeNames)
                EmittedTopLevelTypes.Add(nested);
            if (!SuppressReturn)
            {
                var returnEntries = new List<string> { $"{className} = {className}" };
                foreach (var nested in nestedStaticTypeNames)
                    returnEntries.Add($"{nested} = {nested}");
                AppendLine($"return {{ {string.Join(", ", returnEntries)} }}");
            }
        }
        _currentClassName = null;
    }

    /// <summary>
    /// Route a ClassDeclarationSyntax to either static or instance emission.
    /// </summary>
    public void EmitClass(ClassDeclarationSyntax classDecl)
    {
        bool isStatic = classDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
        if (isStatic)
        {
            EmitStaticClass(classDecl);
        }
        else
        {
            // Extract base class name from BaseList (if any)
            string? baseClassName = null;
            if (classDecl.BaseList != null)
            {
                // Take the first base type (C# single inheritance — interfaces come later)
                var firstBase = classDecl.BaseList.Types.FirstOrDefault();
                if (firstBase != null)
                {
                    var baseTypeName = firstBase.Type.ToString();
                    // Strip namespace qualifiers
                    if (baseTypeName.Contains('.'))
                        baseTypeName = baseTypeName.Substring(baseTypeName.LastIndexOf('.') + 1);
                    // Strip generic args
                    if (baseTypeName.Contains('<'))
                        baseTypeName = baseTypeName.Substring(0, baseTypeName.IndexOf('<'));
                    baseClassName = baseTypeName;
                }
            }
            // Fallback: check global base class map (for partial classes where BaseList is in another file)
            if (baseClassName == null)
                GlobalBaseClassMap.TryGetValue(classDecl.Identifier.Text, out baseClassName);

            bool isNested = classDecl.Parent is ClassDeclarationSyntax
                or StructDeclarationSyntax;
            EmitInstanceClass(classDecl.Identifier.Text, classDecl.Members, isNested: isNested, baseClassName: baseClassName);
        }
    }

    /// <summary>
    /// Route a StructDeclarationSyntax to instance emission (same pattern as instance class).
    /// </summary>
    public void EmitStruct(StructDeclarationSyntax structDecl)
    {
        EmitInstanceClass(structDecl.Identifier.Text, structDecl.Members);
    }

    /// <summary>
    /// Emit an interface as a Luau export type with method signatures.
    /// C# interfaces map to Luau structural type annotations:
    ///   export type IFoo = {
    ///     MethodA: (self: IFoo, x: number) -> string,
    ///     PropertyB: number,
    ///   }
    /// </summary>
    public void EmitInterface(InterfaceDeclarationSyntax ifaceDecl)
    {
        var name = ifaceDecl.Identifier.Text;
        AppendLine($"export type {name} = {{");
        _indent++;

        foreach (var member in ifaceDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                {
                    var methodName = method.Identifier.Text;
                    var returnType = MapTypeNode(method.ReturnType);
                    var paramParts = new List<string> { $"self: {name}" };
                    foreach (var p in method.ParameterList.Parameters)
                    {
                        var pType = MapTypeNode(p.Type!);
                        paramParts.Add($"{EscapeIdentifier(p.Identifier.Text)}: {pType}");
                    }
                    AppendLine($"{methodName}: ({string.Join(", ", paramParts)}) -> {returnType},");
                    break;
                }
                case PropertyDeclarationSyntax prop:
                {
                    var propType = MapTypeNode(prop.Type);
                    AppendLine($"{prop.Identifier.Text}: {propType},");
                    break;
                }
                case EventDeclarationSyntax evt:
                {
                    AppendLine($"{evt.Identifier.Text}: RBXScriptSignal,");
                    break;
                }
                case EventFieldDeclarationSyntax evtField:
                {
                    foreach (var v in evtField.Declaration.Variables)
                    {
                        AppendLine($"{v.Identifier.Text}: RBXScriptSignal,");
                    }
                    break;
                }
                case IndexerDeclarationSyntax indexer:
                {
                    // Indexers can't be directly represented in Luau type syntax — skip with comment
                    AppendLine($"-- indexer: [{MapTypeNode(indexer.ParameterList.Parameters[0].Type!)}] -> {MapTypeNode(indexer.Type)}");
                    break;
                }
            }
        }

        _indent--;
        AppendLine("}");
        AppendLine();

        // Track for unified return
        EmittedTopLevelTypes.Add(name);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Instance class/struct emission
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emit an instance class or struct using the Luau metatable pattern:
    ///   type ClassName_self = { field: Type; ... }
    ///   local ClassName = {}
    ///   ClassName.__index = ClassName
    ///   export type ClassName = typeof(setmetatable({} :: ClassName_self, ClassName))
    ///   function ClassName.new(...): ClassName ... end
    ///   function ClassName.method(self: ClassName, ...): ReturnType ... end
    ///
    /// When baseClassName is provided (class inheritance), emits:
    ///   local ClassName = setmetatable({}, {__index = BaseClass})
    ///   ClassName.__index = ClassName
    /// </summary>
    private void EmitInstanceClass(string className, SyntaxList<MemberDeclarationSyntax> members, bool isNested = false, string? baseClassName = null)
    {
        _currentClassName = className;
        _baseClassName = baseClassName;

        // Track the base class as a referenced module (so it gets a require())
        if (baseClassName != null)
        {
            ReferencedModules.Add(baseClassName);
        }

        // ── Phase 1: collect all member info ──

        var fields = new List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)>();
        var properties = new List<PropertyDeclarationSyntax>();
        var constructors = new List<ConstructorDeclarationSyntax>();
        var methods = new List<MethodDeclarationSyntax>();
        var nestedTypes = new List<MemberDeclarationSyntax>(); // nested struct/class

        _instanceFields = new HashSet<string>();
        _instanceMethods = new HashSet<string>();
        _nestedTypeNames = new HashSet<string>();
        _instanceFieldTypes = new Dictionary<string, string>();

        var fieldRawTypes = new List<string>(); // collected for deferred type tracking

        foreach (var member in members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax fieldDecl:
                {
                    bool isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
                    bool isStatic = fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
                    var rawType = fieldDecl.Declaration.Type.ToString();
                    var luauType = MapTypeNode(fieldDecl.Declaration.Type);

                    // Defer type tracking until all members are collected (so nested types are known)
                    fieldRawTypes.Add(rawType);

                    foreach (var variable in fieldDecl.Declaration.Variables)
                    {
                        var name = variable.Identifier.Text;
                        string? defaultValue = null;
                        if (variable.Initializer != null)
                        {
                            defaultValue = EmitExpression(variable.Initializer.Value);
                        }
                        fields.Add((name, luauType, defaultValue, isConst, isStatic || isConst));

                        if (!isStatic && !isConst)
                        {
                            _instanceFields.Add(name);
                            // Track the simplified type name for cross-type method dispatch
                            var simplifiedType = rawType;
                            if (simplifiedType.Contains('.'))
                                simplifiedType = simplifiedType.Substring(simplifiedType.LastIndexOf('.') + 1);
                            if (simplifiedType.Contains('<'))
                                simplifiedType = simplifiedType.Substring(0, simplifiedType.IndexOf('<'));
                            _instanceFieldTypes[name] = simplifiedType;
                        }
                    }
                    break;
                }
                case PropertyDeclarationSyntax propDecl:
                    properties.Add(propDecl);
                    // Track auto-properties as instance fields for bare identifier → self.field resolution
                    if (!propDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        bool isAutoProperty = propDecl.AccessorList != null
                            && propDecl.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null);
                        if (isAutoProperty)
                        {
                            var propName = propDecl.Identifier.Text;
                            _instanceFields.Add(propName);
                            // Add to type self fields list for the type annotation
                            var luauType = MapTypeNode(propDecl.Type);
                            fields.Add((propName, luauType, null, false, false));
                            // Track field type for cross-type dispatch
                            var rawType = propDecl.Type.ToString();
                            var simplifiedType = rawType;
                            if (simplifiedType.Contains('.'))
                                simplifiedType = simplifiedType.Substring(simplifiedType.LastIndexOf('.') + 1);
                            if (simplifiedType.Contains('<'))
                                simplifiedType = simplifiedType.Substring(0, simplifiedType.IndexOf('<'));
                            _instanceFieldTypes[propName] = simplifiedType;
                        }
                    }
                    break;
                case ConstructorDeclarationSyntax ctorDecl:
                    constructors.Add(ctorDecl);
                    break;
                case MethodDeclarationSyntax methodDecl:
                    methods.Add(methodDecl);
                    if (!methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        _instanceMethods.Add(methodDecl.Identifier.Text);
                    }
                    break;
                case StructDeclarationSyntax nestedStruct:
                    nestedTypes.Add(nestedStruct);
                    _nestedTypeNames.Add(nestedStruct.Identifier.Text);
                    break;
                case ClassDeclarationSyntax nestedClass:
                    nestedTypes.Add(nestedClass);
                    _nestedTypeNames.Add(nestedClass.Identifier.Text);
                    break;
                case EnumDeclarationSyntax nestedEnum:
                    nestedTypes.Add(nestedEnum);
                    _nestedTypeNames.Add(nestedEnum.Identifier.Text);
                    break;
                case InterfaceDeclarationSyntax nestedIface:
                    nestedTypes.Add(nestedIface);
                    _nestedTypeNames.Add(nestedIface.Identifier.Text);
                    break;
                case EventFieldDeclarationSyntax eventField:
                {
                    foreach (var v in eventField.Declaration.Variables)
                    {
                        var eventName = v.Identifier.Text;
                        _instanceFields.Add(eventName);
                        fields.Add((eventName, "RBXScriptSignal", null, false, false));
                    }
                    break;
                }
                case EventDeclarationSyntax eventDecl:
                {
                    var eventName = eventDecl.Identifier.Text;
                    _instanceFields.Add(eventName);
                    fields.Add((eventName, "RBXScriptSignal", null, false, false));
                    break;
                }
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                    // Collected for Phase 6.5 metamethods
                    break;
                case IndexerDeclarationSyntax:
                    // Collected for Phase 6.5 metamethods
                    break;
                default:
                    // Ignore other members for now
                    break;
            }
        }

        // Track external type references from field types (deferred so nested types are known)
        foreach (var rawType in fieldRawTypes)
        {
            TrackTypeReferences(rawType);
        }

        // ── Phase 1.25: inherit parent class fields (for same-file inheritance) ──
        if (baseClassName != null && _emittedTypeFields.TryGetValue(baseClassName, out var parentFields))
        {
            foreach (var parentField in parentFields)
            {
                _instanceFields.Add(parentField);
            }
        }

        // ── Phase 1.5: emit nested types first (they are independent modules) ──
        foreach (var nested in nestedTypes)
        {
            switch (nested)
            {
                case StructDeclarationSyntax nestedStruct:
                {
                    // Save/restore parent class context
                    var savedClassName = _currentClassName;
                    var savedBaseClassName = _baseClassName;
                    var savedFields = _instanceFields;
                    var savedMethods = _instanceMethods;
                    var savedNested = _nestedTypeNames;
                    var savedConsts = _constFields;
                    var savedOverloads = _overloadMap;
                    var savedFieldTypes = _instanceFieldTypes;

                    EmitInstanceClass(nestedStruct.Identifier.Text, nestedStruct.Members, isNested: true);
                    AppendLine();

                    _currentClassName = savedClassName;
                    _baseClassName = savedBaseClassName;
                    _instanceFields = savedFields;
                    _instanceMethods = savedMethods;
                    _nestedTypeNames = savedNested;
                    _constFields = savedConsts;
                    _overloadMap = savedOverloads;
                    _instanceFieldTypes = savedFieldTypes;
                    break;
                }

                case ClassDeclarationSyntax nestedClass:
                {
                    var savedClassName = _currentClassName;
                    var savedBaseClassName = _baseClassName;
                    var savedFields = _instanceFields;
                    var savedMethods = _instanceMethods;
                    var savedNested = _nestedTypeNames;
                    var savedConsts = _constFields;
                    var savedOverloads = _overloadMap;
                    var savedFieldTypes = _instanceFieldTypes;

                    EmitInstanceClass(nestedClass.Identifier.Text, nestedClass.Members, isNested: true);
                    AppendLine();

                    _currentClassName = savedClassName;
                    _baseClassName = savedBaseClassName;
                    _instanceFields = savedFields;
                    _instanceMethods = savedMethods;
                    _nestedTypeNames = savedNested;
                    _constFields = savedConsts;
                    _overloadMap = savedOverloads;
                    _instanceFieldTypes = savedFieldTypes;
                    break;
                }

                case EnumDeclarationSyntax nestedEnum:
                    EmitEnum(nestedEnum);
                    AppendLine();
                    break;

                case InterfaceDeclarationSyntax nestedIface:
                    EmitInterface(nestedIface);
                    break;
            }
        }

        // Separate instance vs static/const fields
        var instanceFields = fields.Where(f => !f.IsStatic && !f.IsConst).ToList();
        var staticFields = fields.Where(f => f.IsStatic && !f.IsConst).ToList();
        var constFields = fields.Where(f => f.IsConst).ToList();

        // Track const fields for bare identifier resolution (e.g., InvalidCharacter → ClassName.InvalidCharacter)
        _constFields = new HashSet<string>();
        foreach (var f in constFields)
            _constFields.Add(f.Name);
        foreach (var f in staticFields)
            _constFields.Add(f.Name);

        // Build overload disambiguation map
        _overloadMap = new Dictionary<(string Name, int ParamCount), string>();
        var methodNameCounts = new Dictionary<string, int>();
        var methodNameSeen = new Dictionary<string, int>();
        foreach (var method in methods)
        {
            var name = method.Identifier.Text;
            methodNameCounts[name] = methodNameCounts.GetValueOrDefault(name) + 1;
        }
        // Pre-compute the disambiguated names
        foreach (var method in methods)
        {
            var name = method.Identifier.Text;
            int paramCount = method.ParameterList.Parameters.Count;
            string emitName = name;

            if (methodNameCounts.GetValueOrDefault(name) > 1)
            {
                var overloadIndex = methodNameSeen.GetValueOrDefault(name);
                methodNameSeen[name] = overloadIndex + 1;

                if (overloadIndex > 0)
                {
                    var firstParam = method.ParameterList.Parameters.FirstOrDefault();
                    var suffix = firstParam?.Type?.ToString() ?? $"_{overloadIndex}";
                    suffix = suffix.Replace(".", "_").Replace("<", "_").Replace(">", "_");
                    emitName = $"{name}_{suffix}";
                }
            }

            _overloadMap[(name, paramCount)] = emitName;
        }

        // ── Phase 2: type self ──
        if (instanceFields.Count > 0)
        {
            AppendLine($"type {className}_self = {{");
            _indent++;
            foreach (var field in instanceFields)
            {
                AppendLine($"{field.Name}: {field.LuauType};");
            }
            _indent--;
            AppendLine("}");
            AppendLine();
        }

        // ── Phase 3: class table + __index + export type ──
        if (baseClassName != null)
        {
            AppendLine($"local {className} = setmetatable({{}}, {{__index = {baseClassName}}})");
        }
        else
        {
            AppendLine($"local {className} = {{}}");
        }
        AppendLine($"{className}.__index = {className}");

        if (instanceFields.Count > 0)
            AppendLine($"export type {className} = typeof(setmetatable({{}} :: {className}_self, {className}))");
        else
            AppendLine($"export type {className} = typeof(setmetatable({{}}, {className}))");
        AppendLine();

        // ── Phase 3.5: const fields as module-level locals ──
        foreach (var field in constFields)
        {
            if (field.DefaultValue != null)
                AppendLine($"{className}.{field.Name} = {field.DefaultValue}");
            else
                AppendLine($"{className}.{field.Name} = nil");
        }

        // ── Phase 3.6: static fields ──
        foreach (var field in staticFields)
        {
            if (field.DefaultValue != null)
                AppendLine($"{className}.{field.Name} = {field.DefaultValue}");
            else
                AppendLine($"{className}.{field.Name} = nil");
        }

        if (constFields.Count > 0 || staticFields.Count > 0)
            AppendLine();

        // ── Phase 4: constructor ──
        _isInstanceContext = true;
        if (constructors.Count > 0)
        {
            var ctor = constructors[0]; // Use first constructor
            EmitConstructor(className, ctor, instanceFields, baseClassName);
        }
        else if (instanceFields.Count > 0 || baseClassName != null)
        {
            // Auto-generate parameterless constructor
            EmitAutoConstructor(className, instanceFields, baseClassName);
        }

        // ── Phase 5: properties (expression-bodied → getter function) ──
        foreach (var prop in properties)
        {
            EmitProperty(className, prop);
        }

        // ── Phase 6: methods (use pre-computed overload map) ──
        foreach (var method in methods)
        {
            var name = method.Identifier.Text;
            int paramCount = method.ParameterList.Parameters.Count;
            string emitName = _overloadMap.GetValueOrDefault((name, paramCount), name);

            // Abstract methods have no body — emit as a comment stub only
            bool isAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword);
            if (isAbstract)
            {
                AppendLine($"-- abstract: {className}.{emitName}");
                AppendLine();
                continue;
            }

            bool isStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword);
            if (isStatic)
            {
                _isInstanceContext = false;
                EmitStaticMethod(className, method, emitName);
                _isInstanceContext = true;
            }
            else
            {
                EmitInstanceMethod(className, method, emitName);
            }
            AppendLine();
        }

        _isInstanceContext = false;

        // ── Phase 6.5: operator overloads → metamethods, indexers ──
        foreach (var member in members)
        {
            switch (member)
            {
                case OperatorDeclarationSyntax opDecl:
                    EmitOperatorOverload(className, opDecl);
                    break;
                case ConversionOperatorDeclarationSyntax convDecl:
                    EmitConversionOperator(className, convDecl);
                    break;
                case IndexerDeclarationSyntax indexerDecl:
                    EmitIndexer(className, indexerDecl);
                    break;
            }
        }

        // Register this type's instance fields for child class inheritance resolution
        _emittedTypeFields[className] = new HashSet<string>(_instanceFields);

        // ── Phase 7: return (only for top-level types, not nested) ──
        if (!isNested)
        {
            // Track this type for the unified return
            EmittedTopLevelTypes.Add(className);
            foreach (var nested in _nestedTypeNames)
            {
                EmittedTopLevelTypes.Add(nested);
            }

            if (!SuppressReturn)
            {
                // Build return table including the main class and any nested types
                var returnEntries = new List<string>();
                returnEntries.Add($"{className} = {className}");
                foreach (var nested in _nestedTypeNames)
                {
                    returnEntries.Add($"{nested} = {nested}");
                }
                AppendLine($"return {{ {string.Join(", ", returnEntries)} }}");
            }
        }
        _currentClassName = null;
        _baseClassName = null;
        _instanceFields = new HashSet<string>();
        _instanceMethods = new HashSet<string>();
        _nestedTypeNames = new HashSet<string>();
        _constFields = new HashSet<string>();
        _instanceFieldTypes = new Dictionary<string, string>();
        _overloadMap = new Dictionary<(string Name, int ParamCount), string>();
    }

    /// <summary>
    /// Emit a constructor as ClassName.new(params): ClassName
    /// When baseClassName is provided, handles base() constructor initializer.
    /// </summary>
    private void EmitConstructor(
        string className,
        ConstructorDeclarationSyntax ctor,
        List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)> instanceFields,
        string? baseClassName = null)
    {
        var parameters = ctor.ParameterList.Parameters;
        var paramParts = new List<string>();
        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();

        foreach (var param in parameters)
        {
            var paramName = EscapeIdentifier(param.Identifier.Text);
            var paramType = MapTypeNode(param.Type);
            paramParts.Add($"{paramName}: {paramType}");
            _currentMethodParams.Add(param.Identifier.Text);
            if (paramName != param.Identifier.Text) _currentMethodParams.Add(paramName);
            _currentMethodParamTypes[paramName] = param.Type?.ToString() ?? "";
        }

        var paramStr = string.Join(", ", paramParts);
        AppendLine($"function {className}.new({paramStr}): {className}");
        _indent++;

        // Handle base constructor initializer: `: base(args)`
        if (baseClassName != null && ctor.Initializer != null
            && ctor.Initializer.IsKind(SyntaxKind.BaseConstructorInitializer))
        {
            // Call parent constructor to get base fields, then overlay child fields
            var baseArgs = ctor.Initializer.ArgumentList.Arguments
                .Select(a => EmitExpression(a.Expression));
            var baseArgStr = string.Join(", ", baseArgs);
            AppendLine($"local self = setmetatable({baseClassName}.new({baseArgStr}) :: any, {className})");

            // Set child-specific field defaults
            foreach (var field in instanceFields)
            {
                var value = field.DefaultValue ?? GetDefaultValueForType(field.LuauType);
                AppendLine($"self.{field.Name} = {value}");
            }
        }
        else if (baseClassName != null)
        {
            // Inheritance but no explicit base() call — call parameterless parent constructor
            AppendLine($"local self = setmetatable({baseClassName}.new() :: any, {className})");

            // Set child-specific field defaults
            foreach (var field in instanceFields)
            {
                var value = field.DefaultValue ?? GetDefaultValueForType(field.LuauType);
                AppendLine($"self.{field.Name} = {value}");
            }
        }
        else
        {
            // No inheritance: setmetatable with field defaults
            if (instanceFields.Count > 0)
            {
                AppendLine($"local self = setmetatable({{");
                _indent++;
                foreach (var field in instanceFields)
                {
                    var value = field.DefaultValue ?? GetDefaultValueForType(field.LuauType);
                    AppendLine($"{field.Name} = {value},");
                }
                _indent--;
                AppendLine($"}} :: {className}_self, {className})");
            }
            else
            {
                AppendLine($"local self = setmetatable({{}}, {className})");
            }
        }

        // Emit constructor body (assignments, etc.)
        if (ctor.Body != null)
        {
            EmitBlock(ctor.Body);
        }

        AppendLine("return self");
        _indent--;
        AppendLine("end");
        AppendLine();

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    /// <summary>
    /// Emit an auto-generated parameterless constructor for types with instance fields but no explicit constructor.
    /// When baseClassName is provided, chains to parent constructor.
    /// </summary>
    private void EmitAutoConstructor(
        string className,
        List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)> instanceFields,
        string? baseClassName = null)
    {
        AppendLine($"function {className}.new(): {className}");
        _indent++;

        if (baseClassName != null)
        {
            // Inheritance: call parent constructor and re-set metatable to child
            AppendLine($"local self = setmetatable({baseClassName}.new() :: any, {className})");

            // Set child-specific field defaults
            foreach (var field in instanceFields)
            {
                var value = field.DefaultValue ?? GetDefaultValueForType(field.LuauType);
                AppendLine($"self.{field.Name} = {value}");
            }
        }
        else if (instanceFields.Count > 0)
        {
            AppendLine($"local self = setmetatable({{");
            _indent++;
            foreach (var field in instanceFields)
            {
                var value = field.DefaultValue ?? GetDefaultValueForType(field.LuauType);
                AppendLine($"{field.Name} = {value},");
            }
            _indent--;
            AppendLine($"}} :: {className}_self, {className})");
        }
        else
        {
            AppendLine($"local self = setmetatable({{}}, {className})");
        }

        AppendLine("return self");

        _indent--;
        AppendLine("end");
        AppendLine();
    }

    /// <summary>
    /// Get a sensible default value for a Luau type.
    /// </summary>
    private static string GetDefaultValueForType(string luauType)
    {
        return luauType switch
        {
            "number" => "0",
            "string" => "\"\"",
            "boolean" => "false",
            _ when luauType.StartsWith("{") => "{}", // table types
            _ => "nil :: any"
        };
    }

    /// <summary>
    /// Emit a property as a getter function (and optionally a setter).
    /// Expression-bodied properties become simple getters.
    /// Pure auto-properties ({ get; set; }) are treated as fields and skipped here.
    /// </summary>
    private void EmitProperty(string className, PropertyDeclarationSyntax prop)
    {
        var propName = prop.Identifier.Text;
        var propType = MapTypeNode(prop.Type);

        // Skip pure auto-properties — they are emitted as fields in the type self block
        if (prop.AccessorList != null
            && prop.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null)
            && prop.ExpressionBody == null)
        {
            return;
        }

        // Expression-bodied property: public int Position => _position;
        if (prop.ExpressionBody != null)
        {
            _currentMethodParams = new HashSet<string>();
            _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
            AppendLine($"function {className}.get_{propName}(self: {className}): {propType}");
            _indent++;
            var expr = EmitExpression(prop.ExpressionBody.Expression);
            AppendLine($"return {expr}");
            _indent--;
            AppendLine("end");
            AppendLine();
            return;
        }

        // Auto-property with accessors
        if (prop.AccessorList != null)
        {
            foreach (var accessor in prop.AccessorList.Accessors)
            {
                if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
                {
                    _currentMethodParams = new HashSet<string>();
                    _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
                    AppendLine($"function {className}.get_{propName}(self: {className}): {propType}");
                    _indent++;
                    if (accessor.Body != null)
                    {
                        EmitBlock(accessor.Body);
                    }
                    else if (accessor.ExpressionBody != null)
                    {
                        var expr = EmitExpression(accessor.ExpressionBody.Expression);
                        AppendLine($"return {expr}");
                    }
                    else
                    {
                        // Auto-property getter: return backing field
                        AppendLine($"return self._{propName}");
                    }
                    _indent--;
                    AppendLine("end");
                    AppendLine();
                }
                else if (accessor.Keyword.IsKind(SyntaxKind.SetKeyword))
                {
                    _currentMethodParams = new HashSet<string> { "value" };
                    _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
                    AppendLine($"function {className}.set_{propName}(self: {className}, value: {propType})");
                    _indent++;
                    if (accessor.Body != null)
                    {
                        EmitBlock(accessor.Body);
                    }
                    else if (accessor.ExpressionBody != null)
                    {
                        var expr = EmitExpression(accessor.ExpressionBody.Expression);
                        AppendLine(expr);
                    }
                    else
                    {
                        // Auto-property setter
                        AppendLine($"self._{propName} = value");
                    }
                    _indent--;
                    AppendLine("end");
                    AppendLine();
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Method emission
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emit an instance method with explicit self parameter:
    ///   function ClassName.method(self: ClassName, param: Type): ReturnType
    /// </summary>
    private void EmitInstanceMethod(string className, MethodDeclarationSyntax method, string? emitName = null)
    {
        var methodName = emitName ?? method.Identifier.Text;
        var returnType = MapReturnType(method.ReturnType);
        var parameters = method.ParameterList.Parameters;

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();

        // Build parameter list: self first, then declared params
        var paramParts = new List<string>();
        paramParts.Add($"self: {className}");
        foreach (var param in parameters)
        {
            var paramName = EscapeIdentifier(param.Identifier.Text);
            var paramType = MapTypeNode(param.Type);
            paramParts.Add($"{paramName}: {paramType}");
            _currentMethodParams.Add(param.Identifier.Text);
            if (paramName != param.Identifier.Text) _currentMethodParams.Add(paramName);
            _currentMethodParamTypes[paramName] = param.Type?.ToString() ?? "";
        }

        var paramStr = string.Join(", ", paramParts);
        AppendLine($"function {className}.{methodName}({paramStr}): {returnType}");
        _indent++;

        if (method.Body != null)
        {
            EmitBlock(method.Body);
        }
        else if (method.ExpressionBody != null)
        {
            var expr = EmitExpression(method.ExpressionBody.Expression);
            AppendLine($"return {expr}");
        }
        else
        {
            AppendLine("-- empty method body");
        }

        _indent--;
        AppendLine("end");

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    /// <summary>
    /// Emit a static method (no self parameter):
    ///   function ClassName.method(param: Type): ReturnType
    /// </summary>
    private void EmitStaticMethod(string className, MethodDeclarationSyntax method, string? emitName = null)
    {
        var methodName = emitName ?? method.Identifier.Text;
        var returnType = MapReturnType(method.ReturnType);
        var parameters = method.ParameterList.Parameters;

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();

        // Build parameter list with types
        var paramParts = new List<string>();
        foreach (var param in parameters)
        {
            var paramName = EscapeIdentifier(param.Identifier.Text);
            var paramType = MapTypeNode(param.Type);
            paramParts.Add($"{paramName}: {paramType}");
            _currentMethodParams.Add(param.Identifier.Text);
            if (paramName != param.Identifier.Text) _currentMethodParams.Add(paramName);
            _currentMethodParamTypes[paramName] = param.Type?.ToString() ?? "";
        }

        var paramStr = string.Join(", ", paramParts);
        AppendLine($"function {className}.{methodName}({paramStr}): {returnType}");
        _indent++;

        if (method.Body != null)
        {
            EmitBlock(method.Body);
        }
        else if (method.ExpressionBody != null)
        {
            var expr = EmitExpression(method.ExpressionBody.Expression);
            AppendLine($"return {expr}");
        }
        else
        {
            AppendLine("-- empty method body");
        }

        _indent--;
        AppendLine("end");

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Operator overloads → metamethods
    // ────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> OperatorToMetamethod = new()
    {
        ["+"] = "__add", ["-"] = "__sub", ["*"] = "__mul", ["/"] = "__div",
        ["%"] = "__mod", ["=="] = "__eq", ["<"] = "__lt", ["<="] = "__le",
        ["-_unary"] = "__unm",
    };

    private void EmitOperatorOverload(string className, OperatorDeclarationSyntax opDecl)
    {
        var op = opDecl.OperatorToken.Text;
        bool isUnary = opDecl.ParameterList.Parameters.Count == 1;
        var metaKey = isUnary && op == "-" ? "-_unary" : op;

        if (!OperatorToMetamethod.TryGetValue(metaKey, out var metamethod))
        {
            AppendLine($"-- unsupported operator overload: {op}");
            return;
        }

        _isInstanceContext = false;
        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();

        var parameters = opDecl.ParameterList.Parameters;
        var paramParts = new List<string>();
        foreach (var param in parameters)
        {
            var paramName = EscapeIdentifier(param.Identifier.Text);
            var paramType = MapTypeNode(param.Type);
            paramParts.Add($"{paramName}: {paramType}");
            _currentMethodParams.Add(param.Identifier.Text);
            if (paramName != param.Identifier.Text) _currentMethodParams.Add(paramName);
            _currentMethodParamTypes[paramName] = param.Type?.ToString() ?? "";
        }

        var returnType = MapTypeNode(opDecl.ReturnType);
        AppendLine($"function {className}.{metamethod}({string.Join(", ", paramParts)}): {returnType}");
        _indent++;

        if (opDecl.Body != null)
            EmitBlock(opDecl.Body);
        else if (opDecl.ExpressionBody != null)
            AppendLine($"return {EmitExpression(opDecl.ExpressionBody.Expression)}");

        _indent--;
        AppendLine("end");
        AppendLine();

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    private void EmitConversionOperator(string className, ConversionOperatorDeclarationSyntax convDecl)
    {
        // C# implicit/explicit conversion operators don't map cleanly to Luau metamethods.
        // Emit as a static conversion method.
        var targetType = MapTypeNode(convDecl.Type);
        var direction = convDecl.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ImplicitKeyword)
            ? "from" : "to";
        var methodName = $"__{direction}_{convDecl.Type.ToString().Replace(".", "_").Replace("<", "_").Replace(">", "_")}";

        _isInstanceContext = false;
        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();

        var param = convDecl.ParameterList.Parameters[0];
        var paramName = EscapeIdentifier(param.Identifier.Text);
        var paramType = MapTypeNode(param.Type);
        _currentMethodParams.Add(param.Identifier.Text);
        if (paramName != param.Identifier.Text) _currentMethodParams.Add(paramName);
        _currentMethodParamTypes[paramName] = param.Type?.ToString() ?? "";

        AppendLine($"function {className}.{methodName}({paramName}: {paramType}): {targetType}");
        _indent++;

        if (convDecl.Body != null)
            EmitBlock(convDecl.Body);
        else if (convDecl.ExpressionBody != null)
            AppendLine($"return {EmitExpression(convDecl.ExpressionBody.Expression)}");

        _indent--;
        AppendLine("end");
        AppendLine();

        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    private void EmitIndexer(string className, IndexerDeclarationSyntax indexerDecl)
    {
        // Emit getter and setter as __index/__newindex overrides via a wrapper
        var paramType = MapTypeNode(indexerDecl.ParameterList.Parameters[0].Type!);
        var returnType = MapTypeNode(indexerDecl.Type);
        var paramName = EscapeIdentifier(indexerDecl.ParameterList.Parameters[0].Identifier.Text);

        if (indexerDecl.AccessorList != null)
        {
            foreach (var accessor in indexerDecl.AccessorList.Accessors)
            {
                if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                {
                    _isInstanceContext = true;
                    _currentMethodParams = new HashSet<string> { paramName };
                    _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
                    _currentMethodParamTypes = new Dictionary<string, string>
                    {
                        [paramName] = indexerDecl.ParameterList.Parameters[0].Type?.ToString() ?? ""
                    };

                    AppendLine($"function {className}.__index_get(self: {className}, {paramName}: {paramType}): {returnType}");
                    _indent++;
                    if (accessor.Body != null) EmitBlock(accessor.Body);
                    else if (accessor.ExpressionBody != null)
                        AppendLine($"return {EmitExpression(accessor.ExpressionBody.Expression)}");
                    _indent--;
                    AppendLine("end");
                    AppendLine();
                }
                else if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
                {
                    _isInstanceContext = true;
                    _currentMethodParams = new HashSet<string> { paramName, "value" };
                    _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
                    _currentMethodParamTypes = new Dictionary<string, string>
                    {
                        [paramName] = indexerDecl.ParameterList.Parameters[0].Type?.ToString() ?? "",
                        ["value"] = indexerDecl.Type.ToString()
                    };

                    AppendLine($"function {className}.__index_set(self: {className}, {paramName}: {paramType}, value: {returnType})");
                    _indent++;
                    if (accessor.Body != null) EmitBlock(accessor.Body);
                    else if (accessor.ExpressionBody != null)
                        AppendLine(EmitExpression(accessor.ExpressionBody.Expression));
                    _indent--;
                    AppendLine("end");
                    AppendLine();
                }
            }
        }
        else if (indexerDecl.ExpressionBody != null)
        {
            // Expression-bodied indexer (getter only)
            _isInstanceContext = true;
            _currentMethodParams = new HashSet<string> { paramName };
            _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
            _currentMethodParamTypes = new Dictionary<string, string>
            {
                [paramName] = indexerDecl.ParameterList.Parameters[0].Type?.ToString() ?? ""
            };

            AppendLine($"function {className}.__index_get(self: {className}, {paramName}: {paramType}): {returnType}");
            _indent++;
            AppendLine($"return {EmitExpression(indexerDecl.ExpressionBody.Expression)}");
            _indent--;
            AppendLine("end");
            AppendLine();
        }

        _isInstanceContext = false;
        _currentMethodParams = new HashSet<string>();
        _currentMethodLocals = new HashSet<string>();
        _patternVarAliases.Clear();
        _currentMethodParamTypes = new Dictionary<string, string>();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Statement emission
    // ────────────────────────────────────────────────────────────────────

    private void EmitBlock(BlockSyntax block)
    {
        foreach (var statement in block.Statements)
        {
            EmitStatement(statement);
        }
    }

    private void EmitStatement(StatementSyntax statement)
    {
        switch (statement)
        {
            case ReturnStatementSyntax ret:
                EmitReturn(ret);
                break;
            case IfStatementSyntax ifStmt:
                EmitIf(ifStmt);
                break;
            case SwitchStatementSyntax switchStmt:
                EmitSwitch(switchStmt);
                break;
            case BlockSyntax block:
                EmitBlock(block);
                break;
            case ExpressionStatementSyntax exprStmt:
                EmitExpressionStatement(exprStmt);
                break;
            case LocalDeclarationStatementSyntax localDecl:
                EmitLocalDeclaration(localDecl);
                break;
            case ThrowStatementSyntax throwStmt:
                EmitThrow(throwStmt);
                break;
            case BreakStatementSyntax:
                AppendLine("break");
                break;
            case ContinueStatementSyntax:
                AppendLine("continue");
                break;
            case DoStatementSyntax doStmt:
                EmitDoWhile(doStmt);
                break;
            case TryStatementSyntax tryStmt:
                EmitTryCatch(tryStmt);
                break;
            case WhileStatementSyntax whileStmt:
                EmitWhile(whileStmt);
                break;
            case ForStatementSyntax forStmt:
                EmitFor(forStmt);
                break;
            case ForEachStatementSyntax foreachStmt:
                EmitForEach(foreachStmt);
                break;
            case UsingStatementSyntax usingStmt:
                EmitUsingStatement(usingStmt);
                break;
            case LockStatementSyntax lockStmt:
                AppendLine($"-- lock ({EmitExpression(lockStmt.Expression)})");
                EmitStatementBody(lockStmt.Statement);
                break;
            case YieldStatementSyntax yieldStmt:
                EmitYieldStatement(yieldStmt);
                break;
            case LocalFunctionStatementSyntax localFunc:
                EmitLocalFunction(localFunc);
                break;
            case CheckedStatementSyntax checkedStmt:
                // checked/unchecked blocks → just emit the body (Luau has no overflow)
                EmitBlock(checkedStmt.Block);
                break;
            case GotoStatementSyntax gotoStmt:
                EmitGotoStatement(gotoStmt);
                break;
            case LabeledStatementSyntax labeledStmt:
                AppendLine($"-- label: {labeledStmt.Identifier.Text}");
                EmitStatement(labeledStmt.Statement);
                break;
            case EmptyStatementSyntax:
                break; // skip semicolons
            default:
                AppendLine($"-- TODO: {statement.Kind()}");
                break;
        }
    }

    private void EmitReturn(ReturnStatementSyntax ret)
    {
        if (ret.Expression == null)
        {
            AppendLine("return");
        }
        else if (ret.Expression is SwitchExpressionSyntax switchExpr)
        {
            // Switch expression used as return: emit as if/elseif chain
            EmitSwitchExpressionAsReturn(switchExpr);
        }
        else
        {
            var expr = EmitExpression(ret.Expression);
            AppendLine($"return {expr}");
        }
    }

    private void EmitIf(IfStatementSyntax ifStmt)
    {
        // If the condition is an is-pattern with a declaration, emit variable binding first
        EmitPatternVariableBindings(ifStmt.Condition);
        var condition = EmitExpression(ifStmt.Condition);
        AppendLine($"if {condition} then");
        _indent++;
        EmitStatementBody(ifStmt.Statement);
        _indent--;

        if (ifStmt.Else != null)
        {
            if (ifStmt.Else.Statement is IfStatementSyntax elseIf)
            {
                // elseif chain
                EmitElseIf(elseIf);
            }
            else
            {
                AppendLine("else");
                _indent++;
                EmitStatementBody(ifStmt.Else.Statement);
                _indent--;
                AppendLine("end");
            }
        }
        else
        {
            AppendLine("end");
        }
    }

    private void EmitElseIf(IfStatementSyntax ifStmt)
    {
        var condition = EmitExpression(ifStmt.Condition);
        AppendLine($"elseif {condition} then");
        _indent++;
        EmitStatementBody(ifStmt.Statement);
        _indent--;

        if (ifStmt.Else != null)
        {
            if (ifStmt.Else.Statement is IfStatementSyntax elseIf)
            {
                EmitElseIf(elseIf);
            }
            else
            {
                AppendLine("else");
                _indent++;
                EmitStatementBody(ifStmt.Else.Statement);
                _indent--;
                AppendLine("end");
            }
        }
        else
        {
            AppendLine("end");
        }
    }

    /// <summary>
    /// Emit the body of an if/else/switch — which may be a block or a single statement.
    /// </summary>
    private void EmitStatementBody(StatementSyntax statement)
    {
        if (statement is BlockSyntax block)
        {
            EmitBlock(block);
        }
        else
        {
            EmitStatement(statement);
        }
    }

    private void EmitWhile(WhileStatementSyntax whileStmt)
    {
        var condition = EmitExpression(whileStmt.Condition);
        AppendLine($"while {condition} do");
        _indent++;
        EmitStatementBody(whileStmt.Statement);
        _indent--;
        AppendLine("end");
    }

    private void EmitDoWhile(DoStatementSyntax doStmt)
    {
        var condition = EmitExpression(doStmt.Condition);
        AppendLine("repeat");
        _indent++;
        EmitStatementBody(doStmt.Statement);
        _indent--;
        AppendLine($"until not ({condition})");
    }

    /// <summary>
    /// Emit a try/catch/finally block using pcall.
    ///   try { body } catch (Exception e) { handler } finally { cleanup }
    /// becomes:
    ///   local __ok, __pcall_ret = pcall(function() body end)
    ///   if not __ok then handler end
    ///   cleanup
    ///   return __pcall_ret  -- only if try block contains return statements
    /// pcall returns (true, returnValue) on success, (false, error) on failure.
    /// </summary>
    private void EmitTryCatch(TryStatementSyntax tryStmt)
    {
        // Check if the try block contains any return statements
        bool tryHasReturn = tryStmt.Block.DescendantNodes().OfType<ReturnStatementSyntax>().Any();

        // Emit pcall wrapper for the try block
        AppendLine("local __ok, __pcall_ret = pcall(function()");
        _indent++;
        EmitBlock(tryStmt.Block);
        _indent--;
        AppendLine("end)");

        // Emit catch clauses
        if (tryStmt.Catches.Count > 0)
        {
            bool isFirst = true;
            foreach (var catchClause in tryStmt.Catches)
            {
                var keyword = isFirst ? "if" : "elseif";
                isFirst = false;

                AppendLine($"{keyword} not __ok then");
                _indent++;

                // If the catch has a declaration (e.g., catch (Exception e)),
                // declare the exception variable as a table with Message property
                if (catchClause.Declaration != null)
                {
                    var varName = catchClause.Declaration.Identifier.Text;
                    if (!string.IsNullOrEmpty(varName))
                    {
                        _currentMethodLocals.Add(varName);
                        // Create exception as table with Message for .Message access
                        // and tostring() for passing as inner exception
                        AppendLine($"local {varName} = {{ Message = tostring(__pcall_ret) }}");
                    }
                }

                EmitBlock(catchClause.Block);
                _indent--;
            }
            AppendLine("end");
        }

        // Emit finally block (runs unconditionally after pcall + catch)
        if (tryStmt.Finally != null)
        {
            EmitBlock(tryStmt.Finally.Block);
        }

        // Forward the try block's return value if it contained return statements
        // pcall returns (true, retval) on success — we need to propagate retval
        if (tryHasReturn)
        {
            AppendLine("if __ok then return __pcall_ret end");
        }
    }

    private void EmitFor(ForStatementSyntax forStmt)
    {
        // C# for loops: for (init; condition; incrementors) body
        // We emit as: init; while condition do body; incrementors end

        // Emit initializer (typically: var i = 0)
        if (forStmt.Declaration != null)
        {
            foreach (var variable in forStmt.Declaration.Variables)
            {
                var rawName = variable.Identifier.Text;
                var name = EscapeIdentifier(rawName);
                _currentMethodLocals.Add(rawName);
                if (name != rawName) _currentMethodLocals.Add(name);
                if (variable.Initializer != null)
                {
                    var init = EmitExpression(variable.Initializer.Value);
                    AppendLine($"local {name} = {init}");
                }
                else
                {
                    AppendLine($"local {name}");
                }
            }
        }
        foreach (var initializer in forStmt.Initializers)
        {
            var expr = EmitExpression(initializer);
            AppendLine(expr);
        }

        // Emit while loop
        var condition = forStmt.Condition != null ? EmitExpression(forStmt.Condition) : "true";
        AppendLine($"while {condition} do");
        _indent++;
        EmitStatementBody(forStmt.Statement);

        // Emit incrementors
        foreach (var incrementor in forStmt.Incrementors)
        {
            EmitIncrementorStatement(incrementor);
        }

        _indent--;
        AppendLine("end");
    }

    /// <summary>
    /// Emit an incrementor expression (like i++) as a statement.
    /// </summary>
    private void EmitIncrementorStatement(ExpressionSyntax expr)
    {
        if (expr is PostfixUnaryExpressionSyntax postfix)
        {
            var operand = EmitExpression(postfix.Operand);
            switch (postfix.Kind())
            {
                case SyntaxKind.PostIncrementExpression:
                    AppendLine($"{operand} += 1");
                    return;
                case SyntaxKind.PostDecrementExpression:
                    AppendLine($"{operand} -= 1");
                    return;
            }
        }
        else if (expr is PrefixUnaryExpressionSyntax prefix)
        {
            var operand = EmitExpression(prefix.Operand);
            switch (prefix.Kind())
            {
                case SyntaxKind.PreIncrementExpression:
                    AppendLine($"{operand} += 1");
                    return;
                case SyntaxKind.PreDecrementExpression:
                    AppendLine($"{operand} -= 1");
                    return;
            }
        }

        // Fallback: emit as expression statement
        var exprStr = EmitExpression(expr);
        AppendLine(exprStr);
    }

    private void EmitForEach(ForEachStatementSyntax foreachStmt)
    {
        var varName = EscapeIdentifier(foreachStmt.Identifier.Text);
        var collection = EmitExpression(foreachStmt.Expression);
        _currentMethodLocals.Add(foreachStmt.Identifier.Text);
        if (varName != foreachStmt.Identifier.Text) _currentMethodLocals.Add(varName);

        AppendLine($"for _, {varName} in {collection} do");
        _indent++;
        EmitStatementBody(foreachStmt.Statement);
        _indent--;
        AppendLine("end");
    }

    private void EmitUsingStatement(UsingStatementSyntax usingStmt)
    {
        if (usingStmt.Declaration != null)
        {
            foreach (var v in usingStmt.Declaration.Variables)
            {
                var vName = EscapeIdentifier(v.Identifier.Text);
                var init = v.Initializer != null ? EmitExpression(v.Initializer.Value) : "nil";
                AppendLine($"local {vName} = {init}");
                _currentMethodLocals.Add(v.Identifier.Text);
                if (vName != v.Identifier.Text) _currentMethodLocals.Add(vName);
            }
        }
        else if (usingStmt.Expression != null)
        {
            AppendLine($"local __using = {EmitExpression(usingStmt.Expression)}");
        }
        AppendLine("do");
        _indent++;
        EmitStatementBody(usingStmt.Statement);
        _indent--;
        AppendLine("end");
    }

    private void EmitYieldStatement(YieldStatementSyntax yieldStmt)
    {
        if (yieldStmt.IsKind(SyntaxKind.YieldReturnStatement))
        {
            var expr = EmitExpression(yieldStmt.Expression!);
            AppendLine($"coroutine.yield({expr})");
        }
        else // YieldBreakStatement
        {
            AppendLine("return");
        }
    }

    private void EmitGotoStatement(GotoStatementSyntax gotoStmt)
    {
        if (gotoStmt.IsKind(SyntaxKind.GotoCaseStatement) && gotoStmt.Expression != null)
        {
            // goto case X; → not directly supported in Luau, emit as comment
            var caseValue = EmitExpression(gotoStmt.Expression);
            AppendLine($"-- goto case {caseValue} (not supported in Luau — restructure switch)");
        }
        else if (gotoStmt.IsKind(SyntaxKind.GotoDefaultStatement))
        {
            AppendLine("-- goto default (not supported in Luau — restructure switch)");
        }
        else
        {
            // goto label;
            AppendLine($"-- goto {gotoStmt.Expression?.ToString() ?? gotoStmt.CaseOrDefaultKeyword.Text}");
        }
    }

    private void EmitLocalFunction(LocalFunctionStatementSyntax localFunc)
    {
        var name = localFunc.Identifier.Text;
        var parameters = localFunc.ParameterList.Parameters
            .Select(p => p.Identifier.Text);
        var paramStr = string.Join(", ", parameters);

        AppendLine($"local function {name}({paramStr})");
        _indent++;
        if (localFunc.Body != null)
        {
            foreach (var stmt in localFunc.Body.Statements)
                EmitStatement(stmt);
        }
        else if (localFunc.ExpressionBody != null)
        {
            var expr = EmitExpression(localFunc.ExpressionBody.Expression);
            AppendLine($"return {expr}");
        }
        _indent--;
        AppendLine("end");
    }

    private void EmitSwitch(SwitchStatementSyntax switchStmt)
    {
        var governing = EmitExpression(switchStmt.Expression);

        // Gather sections. Each section can have multiple labels and multiple statements.
        // We convert to if/elseif chains.
        bool isFirst = true;
        SwitchSectionSyntax? defaultSection = null;

        foreach (var section in switchStmt.Sections)
        {
            var caseLabels = section.Labels.OfType<CaseSwitchLabelSyntax>().ToList();
            var hasDefault = section.Labels.Any(l => l is DefaultSwitchLabelSyntax);

            if (caseLabels.Count == 0 && hasDefault)
            {
                defaultSection = section;
                continue;
            }

            // Build condition: governing == val1 or governing == val2 ...
            var conditions = caseLabels.Select(c => $"{governing} == {EmitExpression(c.Value)}");
            var condStr = string.Join(" or ", conditions);

            if (isFirst)
            {
                AppendLine($"if {condStr} then");
                isFirst = false;
            }
            else
            {
                AppendLine($"elseif {condStr} then");
            }

            _indent++;
            EmitSwitchSectionStatements(section.Statements);
            _indent--;

            // If this section also has a default label, treat it as having both
            if (hasDefault)
                defaultSection = null; // handled inline
        }

        if (defaultSection != null)
        {
            if (!isFirst)
            {
                AppendLine("else");
                _indent++;
                EmitSwitchSectionStatements(defaultSection.Statements);
                _indent--;
                AppendLine("end");
            }
            else
            {
                // Only default section — just emit the statements
                EmitSwitchSectionStatements(defaultSection.Statements);
            }
        }
        else if (!isFirst)
        {
            AppendLine("end");
        }
    }

    private void EmitSwitchSectionStatements(SyntaxList<StatementSyntax> statements)
    {
        foreach (var stmt in statements)
        {
            // Skip break statements in switch sections (Luau doesn't need them)
            if (stmt is BreakStatementSyntax)
                continue;
            EmitStatement(stmt);
        }
    }

    /// <summary>
    /// Emit a C# switch expression as a return-oriented if/elseif chain.
    /// Called when we see: return expr switch { ... };
    /// </summary>
    private void EmitSwitchExpressionAsReturn(SwitchExpressionSyntax switchExpr)
    {
        var governing = EmitExpression(switchExpr.GoverningExpression);
        bool isFirst = true;

        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is DiscardPatternSyntax)
            {
                // Default arm — handle after the chain
                continue;
            }

            var pattern = EmitSwitchPattern(governing, arm.Pattern);
            var value = EmitExpression(arm.Expression);

            if (isFirst)
            {
                AppendLine($"if {pattern} then");
                isFirst = false;
            }
            else
            {
                AppendLine($"elseif {pattern} then");
            }
            _indent++;
            AppendLine($"return {value}");
            _indent--;
        }

        // Handle default arm
        var defaultArm = switchExpr.Arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
        if (defaultArm != null)
        {
            var defaultValue = EmitExpression(defaultArm.Expression);
            if (!isFirst)
            {
                AppendLine("end");
                AppendLine($"return {defaultValue}");
            }
            else
            {
                AppendLine($"return {defaultValue}");
            }
        }
        else if (!isFirst)
        {
            AppendLine("end");
        }
    }

    private string EmitSwitchPattern(string governing, PatternSyntax pattern)
    {
        // Delegate to the unified pattern emission
        return EmitPattern(governing, pattern);
    }

    private void EmitExpressionStatement(ExpressionStatementSyntax exprStmt)
    {
        // Handle increment/decrement as statements
        if (exprStmt.Expression is PostfixUnaryExpressionSyntax postfix)
        {
            var operand = EmitExpression(postfix.Operand);
            switch (postfix.Kind())
            {
                case SyntaxKind.PostIncrementExpression:
                    AppendLine($"{operand} += 1");
                    return;
                case SyntaxKind.PostDecrementExpression:
                    AppendLine($"{operand} -= 1");
                    return;
            }
        }
        else if (exprStmt.Expression is PrefixUnaryExpressionSyntax prefix)
        {
            var operand = EmitExpression(prefix.Operand);
            switch (prefix.Kind())
            {
                case SyntaxKind.PreIncrementExpression:
                    AppendLine($"{operand} += 1");
                    return;
                case SyntaxKind.PreDecrementExpression:
                    AppendLine($"{operand} -= 1");
                    return;
            }
        }
        // Handle compound assignment: x += y, x -= y, etc.
        else if (exprStmt.Expression is AssignmentExpressionSyntax assignment)
        {
            EmitAssignmentStatement(assignment);
            return;
        }

        var expr = EmitExpression(exprStmt.Expression);
        AppendLine(expr);
    }

    /// <summary>
    /// Emit an assignment expression as a statement.
    /// </summary>
    private void EmitAssignmentStatement(AssignmentExpressionSyntax assignment)
    {
        var left = EmitExpression(assignment.Left);
        var right = EmitExpression(assignment.Right);

        // String += → concat: x = x .. y (Luau has no ..= operator)
        if (assignment.Kind() == SyntaxKind.AddAssignmentExpression)
        {
            bool isString = false;
            if (_model != null)
                isString = IsStringType(assignment.Left);
            else
                isString = IsLikelyStringAccess(assignment.Left);

            if (isString)
            {
                AppendLine($"{left} = {left} .. {right}");
                return;
            }
        }

        var op = assignment.Kind() switch
        {
            SyntaxKind.SimpleAssignmentExpression => "=",
            SyntaxKind.AddAssignmentExpression => "+=",
            SyntaxKind.SubtractAssignmentExpression => "-=",
            SyntaxKind.MultiplyAssignmentExpression => "*=",
            SyntaxKind.DivideAssignmentExpression => "/=",
            SyntaxKind.ModuloAssignmentExpression => "%=",
            _ => "="
        };

        AppendLine($"{left} {op} {right}");
    }

    private void EmitLocalDeclaration(LocalDeclarationStatementSyntax localDecl)
    {
        foreach (var declarator in localDecl.Declaration.Variables)
        {
            var rawName = declarator.Identifier.Text;
            var name = EscapeIdentifier(rawName);
            _currentMethodLocals.Add(rawName);
            if (name != rawName) _currentMethodLocals.Add(name);
            if (declarator.Initializer != null)
            {
                var init = EmitExpression(declarator.Initializer.Value);
                AppendLine($"local {name} = {init}");
            }
            else
            {
                AppendLine($"local {name}");
            }
        }
    }

    private void EmitThrow(ThrowStatementSyntax throwStmt)
    {
        if (throwStmt.Expression is ObjectCreationExpressionSyntax objCreate)
        {
            var exType = objCreate.Type.ToString();
            var args = objCreate.ArgumentList?.Arguments;
            if (args != null && args.Value.Count > 0)
            {
                // Try to get a meaningful error message
                var firstArg = args.Value[0].Expression;
                if (firstArg is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameofCall)
                {
                    var nameofArg = nameofCall.ArgumentList.Arguments[0].Expression.ToString();
                    AppendLine($"error(\"{exType}: {nameofArg}\")");
                }
                else
                {
                    var argStr = EmitExpression(firstArg);
                    AppendLine($"error(\"{exType}: \" .. {argStr})");
                }
            }
            else
            {
                AppendLine($"error(\"{exType}\")");
            }
        }
        else if (throwStmt.Expression != null)
        {
            var expr = EmitExpression(throwStmt.Expression);
            AppendLine($"error({expr})");
        }
        else
        {
            // Bare throw; inside catch → rethrow the original pcall error
            AppendLine("error(__pcall_ret)");
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Expression emission
    // ────────────────────────────────────────────────────────────────────

    private string EmitExpression(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax literal => EmitLiteral(literal),
            IdentifierNameSyntax ident => EmitIdentifier(ident),
            MemberAccessExpressionSyntax memberAccess => EmitMemberAccess(memberAccess),
            InvocationExpressionSyntax invocation => EmitInvocation(invocation),
            BinaryExpressionSyntax binary => EmitBinary(binary),
            PrefixUnaryExpressionSyntax prefixUnary => EmitPrefixUnary(prefixUnary),
            ParenthesizedExpressionSyntax paren => EmitParenthesized(paren),
            CastExpressionSyntax cast => EmitCast(cast),
            ConditionalExpressionSyntax conditional => EmitConditional(conditional),
            SwitchExpressionSyntax switchExpr => EmitSwitchExpression(switchExpr),
            DefaultExpressionSyntax => "nil",
            PostfixUnaryExpressionSyntax postfix => EmitPostfixUnary(postfix),
            ThrowExpressionSyntax throwExpr => EmitThrowExpression(throwExpr),
            ObjectCreationExpressionSyntax objCreate => EmitObjectCreation(objCreate),
            ImplicitObjectCreationExpressionSyntax implicitCreate => EmitImplicitObjectCreation(implicitCreate),
            ArrayCreationExpressionSyntax arrayCreate => EmitArrayCreation(arrayCreate),
            ThisExpressionSyntax => "self",
            BaseExpressionSyntax => _baseClassName ?? "--[[TODO: base without parent]] nil",
            ElementAccessExpressionSyntax elementAccess => EmitElementAccess(elementAccess),
            AssignmentExpressionSyntax assignment => EmitAssignmentExpression(assignment),
            ParenthesizedLambdaExpressionSyntax parenLambda => EmitParenthesizedLambda(parenLambda),
            SimpleLambdaExpressionSyntax simpleLambda => EmitSimpleLambda(simpleLambda),
            ConditionalAccessExpressionSyntax conditionalAccess => EmitConditionalAccess(conditionalAccess),
            InterpolatedStringExpressionSyntax interp => EmitInterpolatedString(interp),
            IsPatternExpressionSyntax isPattern => EmitIsPattern(isPattern),
            AwaitExpressionSyntax awaitExpr => EmitAwait(awaitExpr),
            TypeOfExpressionSyntax typeOf => $"\"{typeOf.Type}\"",
            InitializerExpressionSyntax initExpr => EmitInitializerExpression(initExpr),
            DeclarationExpressionSyntax declExpr => EmitDeclarationExpression(declExpr),
            ImplicitArrayCreationExpressionSyntax implicitArray => EmitImplicitArrayCreation(implicitArray),
            CheckedExpressionSyntax checkedExpr => EmitExpression(checkedExpr.Expression), // checked/unchecked → just emit inner
            GenericNameSyntax genericName => genericName.Identifier.Text, // Generic<T> → just the name
            MemberBindingExpressionSyntax memberBinding => $".{memberBinding.Name.Identifier.Text}",
            SizeOfExpressionSyntax sizeOf => $"--[[sizeof({sizeOf.Type})]] 0",
            TupleExpressionSyntax tuple => EmitTupleExpression(tuple),
            RangeExpressionSyntax range => EmitRangeExpression(range),
            RefExpressionSyntax refExpr => EmitExpression(refExpr.Expression), // ref → just emit inner
            StackAllocArrayCreationExpressionSyntax stackAlloc => EmitArrayCreation(stackAlloc),
            AnonymousObjectCreationExpressionSyntax anonObj => EmitAnonymousObjectCreation(anonObj),
            QueryExpressionSyntax queryExpr => EmitQueryExpression(queryExpr),
            PredefinedTypeSyntax predefined => predefined.Keyword.Text, // int, string → literal type name
            _ => $"--[[TODO: {expr.Kind()}]] nil"
        };
    }

    private string EmitLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() switch
        {
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.NullLiteralExpression => "nil",
            SyntaxKind.NumericLiteralExpression => EmitNumericLiteral(literal),
            SyntaxKind.StringLiteralExpression => EmitStringLiteral(literal),
            SyntaxKind.CharacterLiteralExpression => EmitCharLiteral(literal),
            SyntaxKind.DefaultLiteralExpression => "nil",
            _ => literal.Token.Text
        };
    }

    private string EmitNumericLiteral(LiteralExpressionSyntax literal)
    {
        var text = literal.Token.Text;
        // Handle hex literals: keep as-is (0xFF → 0xFF, valid in Luau)
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return text;
        // Handle numeric suffixes (remove them)
        text = text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'l', 'L', 'u', 'U');
        return text;
    }

    private static string EmitStringLiteral(LiteralExpressionSyntax literal)
    {
        // Use the C# string value and re-escape for Lua
        if (literal.Token.Value is string strVal)
        {
            // Escape for Lua string
            var escaped = strVal
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            return $"\"{escaped}\"";
        }
        return literal.Token.Text;
    }

    private static string EmitCharLiteral(LiteralExpressionSyntax literal)
    {
        // Convert char literal to its integer value at transpile time
        if (literal.Token.Value is char charVal)
        {
            return ((int)charVal).ToString();
        }
        // Fallback: try to parse from the token text
        return literal.Token.Text;
    }

    private string EmitIdentifier(IdentifierNameSyntax ident)
    {
        var name = EscapeIdentifier(ident.Identifier.Text);

        // Resolve pattern variable aliases: `m is T p` → p resolves to m
        if (_patternVarAliases.TryGetValue(name, out var alias))
            return alias;

        // Check for well-known replacements
        if (name == "string") return "string";

        // In instance context, bare identifiers that are instance fields → self.field
        if (_isInstanceContext && _instanceFields.Contains(name)
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name))
        {
            return $"self.{name}";
        }

        // Const/static fields accessed as bare identifiers → ClassName.Field
        if (_isInstanceContext && _constFields.Contains(name)
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name)
            && _currentClassName != null)
        {
            return $"{_currentClassName}.{name}";
        }

        // SemanticModel: resolve inherited instance members (fields/properties) → self.member
        if (_isInstanceContext && _model != null
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name))
        {
            var symbol = GetSymbol(ident);
            if (symbol is IFieldSymbol fs && !fs.IsStatic && !fs.IsConst)
                return $"self.{name}";
            if (symbol is IPropertySymbol ps && !ps.IsStatic)
                return $"self.{name}";
            if (symbol is IMethodSymbol ms && !ms.IsStatic && ms.MethodKind == MethodKind.Ordinary)
                return $"self.{name}";
        }

        return name;
    }

    private string EmitMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var memberName = memberAccess.Name.Identifier.Text;

        // Handle `base.field` → `self.field` (metatable chain handles lookup)
        if (memberAccess.Expression is BaseExpressionSyntax)
        {
            return $"self.{memberName}";
        }

        // Handle `this.field` → `self.field`
        if (memberAccess.Expression is ThisExpressionSyntax)
        {
            return $"self.{memberName}";
        }

        // Nullable<T>.HasValue → (x ~= nil), .Value → x
        if (memberName is "HasValue" or "Value")
        {
            bool isNullable = false;
            if (_model != null)
            {
                var typeInfo = _model.GetTypeInfo(memberAccess.Expression);
                isNullable = typeInfo.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
            }
            if (isNullable)
            {
                var receiver = EmitExpression(memberAccess.Expression);
                return memberName == "HasValue" ? $"({receiver} ~= nil)" : receiver;
            }
        }

        // Handle PredefinedTypeSyntax: string.Empty, int.MaxValue, etc.
        if (memberAccess.Expression is PredefinedTypeSyntax predefined)
        {
            var typeStr = predefined.Keyword.Text;

            // string.Empty → ""
            if (typeStr == "string" && memberName == "Empty")
                return "\"\"";

            // int/long/short/byte MaxValue/MinValue
            if (memberName == "MaxValue")
            {
                return typeStr switch
                {
                    "int" => "2147483647",
                    "uint" => "4294967295",
                    "long" => "9223372036854775807",
                    "short" => "32767",
                    "ushort" => "65535",
                    "byte" => "255",
                    "sbyte" => "127",
                    "float" or "double" => "math.huge",
                    _ => $"--[[TODO: {typeStr}.MaxValue]] 0",
                };
            }
            if (memberName == "MinValue")
            {
                return typeStr switch
                {
                    "int" => "-2147483648",
                    "uint" => "0",
                    "long" => "-9223372036854775808",
                    "short" => "-32768",
                    "ushort" => "0",
                    "byte" => "0",
                    "sbyte" => "-128",
                    "float" or "double" => "-math.huge",
                    _ => $"--[[TODO: {typeStr}.MinValue]] 0",
                };
            }

            // double/float special values
            if (typeStr is "double" or "float")
            {
                return memberName switch
                {
                    "NaN" => "(0/0)",
                    "PositiveInfinity" => "math.huge",
                    "NegativeInfinity" => "-math.huge",
                    "Epsilon" => "2.2204460492503131e-16",
                    _ => $"--[[TODO: {typeStr}.{memberName}]] 0",
                };
            }

            // char members
            if (typeStr == "char")
            {
                return memberName switch
                {
                    "MaxValue" => "65535",
                    "MinValue" => "0",
                    _ => $"--[[TODO: char.{memberName}]] 0",
                };
            }

            return $"--[[TODO: {typeStr}.{memberName}]] nil";
        }

        // Handle enum member access: SyntaxKind.TrueKeyword → SyntaxKind.TrueKeyword
        if (memberAccess.Expression is IdentifierNameSyntax typeName)
        {
            var typeStr = typeName.Identifier.Text;

            // Track external module references (not our own class, not a nested type)
            if (typeStr != _currentClassName
                && !_nestedTypeNames.Contains(typeStr)
                && IsLikelyEnumOrExternalType(typeStr)
                && !_currentMethodParams.Contains(typeStr)
                && !_currentMethodLocals.Contains(typeStr)
                && !_instanceFields.Contains(typeStr))
            {
                ReferencedModules.Add(typeStr);
            }

            // string.Empty → ""
            if (typeStr == "string" && memberName == "Empty")
                return "\"\"";

            // Predefined type constants accessed via .NET type names (Int32.MaxValue, Double.NaN, etc.)
            if (typeStr is "Int32" or "UInt32" or "Int64" or "Int16" or "UInt16" or "Byte" or "SByte")
            {
                if (memberName == "MaxValue")
                {
                    return typeStr switch
                    {
                        "Int32" => "2147483647",
                        "UInt32" => "4294967295",
                        "Int64" => "9223372036854775807",
                        "Int16" => "32767",
                        "UInt16" => "65535",
                        "Byte" => "255",
                        "SByte" => "127",
                        _ => "0",
                    };
                }
                if (memberName == "MinValue")
                {
                    return typeStr switch
                    {
                        "Int32" => "-2147483648",
                        "UInt32" or "UInt16" or "Byte" => "0",
                        "Int64" => "-9223372036854775808",
                        "Int16" => "-32768",
                        "SByte" => "-128",
                        _ => "0",
                    };
                }
            }
            if (typeStr is "Double" or "Single")
            {
                var result = memberName switch
                {
                    "MaxValue" => "math.huge",
                    "MinValue" => "-math.huge",
                    "NaN" => "(0/0)",
                    "PositiveInfinity" => "math.huge",
                    "NegativeInfinity" => "-math.huge",
                    "Epsilon" => "2.2204460492503131e-16",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "Char")
            {
                var result = memberName switch
                {
                    "MaxValue" => "65535",
                    "MinValue" => "0",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }

            // .Length on a string variable → string.len(var) or #var
            if (memberName == "Length")
            {
                // If typeStr is an instance field, prefix with self
                if (_isInstanceContext && _instanceFields.Contains(typeStr)
                    && !_currentMethodParams.Contains(typeStr)
                    && !_currentMethodLocals.Contains(typeStr))
                {
                    return $"#self.{typeStr}";
                }
                // Generic .Length → #var (works for strings and tables)
                if (!IsLikelyEnumOrExternalType(typeStr) || _currentMethodParams.Contains(typeStr) || _currentMethodLocals.Contains(typeStr))
                {
                    return $"#{typeStr}";
                }
            }

            // .Count on a List/collection → #var
            if (memberName == "Count")
            {
                if (_isInstanceContext && _instanceFields.Contains(typeStr)
                    && !_currentMethodParams.Contains(typeStr)
                    && !_currentMethodLocals.Contains(typeStr))
                {
                    return $"#self.{typeStr}";
                }
                if (!IsLikelyEnumOrExternalType(typeStr) || _currentMethodParams.Contains(typeStr) || _currentMethodLocals.Contains(typeStr))
                {
                    return $"#{typeStr}";
                }
            }

            // .Position, .Width etc. on instance fields → getter or direct access
            // If accessing a member of an instance field (e.g., _window.Position),
            // and we're in instance context, prefix with self
            if (_isInstanceContext && _instanceFields.Contains(typeStr)
                && !_currentMethodParams.Contains(typeStr)
                && !_currentMethodLocals.Contains(typeStr))
            {
                return $"self.{typeStr}.{memberName}";
            }

            // SyntaxKind.None etc. → SyntaxKind.None (module.member)
            return $"{typeStr}.{memberName}";
        }

        // Handle member access on complex expressions
        var left = EmitExpression(memberAccess.Expression);

        // .Length/.Count → #expr (for strings, arrays, collections)
        if (memberName == "Length" || memberName == "Count")
        {
            if (_model != null)
            {
                var ownerType = GetExpressionType(memberAccess.Expression);
                // String, array, List, Queue, Stack, etc. → #
                if (ownerType?.SpecialType == SpecialType.System_String
                    || ownerType is IArrayTypeSymbol
                    || ownerType?.Name is "List" or "Array" or "Queue" or "Stack" or "StringBuilder")
                {
                    return $"#{left}";
                }
                // Dictionary.Count needs a runtime helper (# doesn't work on dicts)
                if (ownerType?.Name is "Dictionary" or "ConcurrentDictionary")
                {
                    return $"--[[dictCount]] #{left}"; // TODO: replace with __rt.dictCount
                }
            }
            // Fallback: always use # (matches old behavior)
            return $"#{left}";
        }

        return $"{left}.{memberName}";
    }

    private string EmitInvocation(InvocationExpressionSyntax invocation)
    {
        // Delegate to EmitMemberInvocation early to avoid double-emitting arguments
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return EmitMemberInvocation(memberAccess, invocation.ArgumentList.Arguments);
        }

        var args = invocation.ArgumentList.Arguments.Select(a => EmitExpression(a.Expression));
        var argStr = string.Join(", ", args);
        int argCount = invocation.ArgumentList.Arguments.Count;

        // Handle the expression being called
        if (invocation.Expression is IdentifierNameSyntax methodName)
        {
            var name = methodName.Identifier.Text;

            // nameof(x) → "x"
            if (name == "nameof")
            {
                var firstArg = invocation.ArgumentList.Arguments[0].Expression.ToString();
                return $"\"{firstArg}\"";
            }

            // Resolve overloaded name via the overload map
            var resolvedName = ResolveOverloadedName(name, argCount);

            // In instance context, calling an instance method by bare name → self dispatch
            if (_isInstanceContext && _instanceMethods.Contains(name)
                && !_currentMethodParams.Contains(name)
                && !_currentMethodLocals.Contains(name))
            {
                // Use ClassName.Method(self, args) for explicit dispatch
                if (string.IsNullOrEmpty(argStr))
                    return $"{_currentClassName}.{resolvedName}(self)";
                return $"{_currentClassName}.{resolvedName}(self, {argStr})";
            }

            // SemanticModel: resolve local functions and inherited instance methods
            if (_model != null)
            {
                var symbol = GetSymbol(methodName);
                if (symbol is IMethodSymbol ms)
                {
                    if (ms.MethodKind == MethodKind.LocalFunction)
                        return $"{resolvedName}({argStr})";

                    // Inherited instance method called by bare name → add self dispatch
                    if (!ms.IsStatic && ms.MethodKind == MethodKind.Ordinary && _currentClassName != null)
                    {
                        if (string.IsNullOrEmpty(argStr))
                            return $"{_currentClassName}.{resolvedName}(self)";
                        return $"{_currentClassName}.{resolvedName}(self, {argStr})";
                    }
                }
            }

            // Calls to static methods in the same class → ClassName.MethodName(...)
            // But not local variables / parameters that are delegates
            if (_currentClassName != null
                && !_currentMethodLocals.Contains(name)
                && !_currentMethodParams.Contains(name))
            {
                return $"{_currentClassName}.{resolvedName}({argStr})";
            }

            return $"{resolvedName}({argStr})";
        }

        // Fallback
        var exprStr = EmitExpression(invocation.Expression);
        return $"{exprStr}({argStr})";
    }

    /// <summary>
    /// Resolve an overloaded method name using the overload map.
    /// Falls back to the original name if no overload is found.
    /// </summary>
    private string ResolveOverloadedName(string name, int argCount)
    {
        if (_overloadMap.TryGetValue((name, argCount), out var resolved))
            return resolved;
        return name;
    }

    /// <summary>
    /// Handle method calls via member access: obj.Method(args), Type.Method(args), etc.
    /// This is the central place for .NET → Luau method mapping.
    /// </summary>
    private string EmitMemberInvocation(MemberAccessExpressionSyntax memberAccess, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var memberName = memberAccess.Name.Identifier.Text;

        // ── Strip no-op .NET methods — identity in Luau ──
        if (memberName is "ConfigureAwait" or "Cast" or "AsEnumerable")
            return EmitExpression(memberAccess.Expression);

        // ── Nullable<T>.GetValueOrDefault() → (x or 0) / (x or default) ──
        if (memberName == "GetValueOrDefault")
        {
            var receiver = EmitExpression(memberAccess.Expression);
            if (arguments.Count == 1)
                return $"({receiver} or {EmitExpression(arguments[0].Expression)})";
            // No args: default to 0 (most Nullable<T> uses are numeric)
            return $"({receiver} or 0)";
        }

        // ── String case methods — work on any expression receiver ──
        if (memberName is "ToLower" or "ToLowerInvariant" && arguments.Count == 0)
            return $"string.lower({EmitExpression(memberAccess.Expression)})";
        if (memberName is "ToUpper" or "ToUpperInvariant" && arguments.Count == 0)
            return $"string.upper({EmitExpression(memberAccess.Expression)})";

        var args = arguments.Select(a => EmitExpression(a.Expression)).ToList();
        var argStr = string.Join(", ", args);

        // ── Handle base.Method(args) → ParentClass.Method(self, args) ──
        if (memberAccess.Expression is BaseExpressionSyntax && _baseClassName != null)
        {
            if (string.IsNullOrEmpty(argStr))
                return $"{_baseClassName}.{memberName}(self)";
            return $"{_baseClassName}.{memberName}(self, {argStr})";
        }

        // ── Handle this.Method(args) → ClassName.Method(self, args) ──
        if (memberAccess.Expression is ThisExpressionSyntax)
        {
            if (_instanceMethods.Contains(memberName))
            {
                if (string.IsNullOrEmpty(argStr))
                    return $"{_currentClassName}.{memberName}(self)";
                return $"{_currentClassName}.{memberName}(self, {argStr})";
            }
            return $"self.{memberName}({argStr})";
        }

        // ── SemanticModel + MethodMapper: centralized method rewriting ──
        if (_model != null)
        {
            var symbol = _model.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol
                ?? _model.GetSymbolInfo(memberAccess.Parent!).Symbol as IMethodSymbol;
            if (symbol != null)
            {
                var obj = EmitExpression(memberAccess.Expression);
                var receiverType = GetExpressionType(memberAccess.Expression);
                var result = MethodMapper.TryRewrite(symbol, obj, args.ToArray(), receiverType);
                if (result != null)
                {
                    if (result.Contains("__rt.")) NeedsRuntime = true;
                    return result;
                }
            }
        }

        // ── Handle calls on identifier targets ──
        if (memberAccess.Expression is IdentifierNameSyntax ownerIdent)
        {
            var ownerName = ownerIdent.Identifier.Text;

            // Console.WriteLine(x) → print(x)
            if (ownerName == "Console" && memberName is "WriteLine" or "Write")
            {
                return $"print({argStr})";
            }

            // Array.Empty<T>() → {}
            if (ownerName == "Array" && memberName == "Empty")
                return "{}";

            // Known external calls that we can't transpile — emit as TODO
            if (ownerName is "CharUnicodeInfo" or "Char")
            {
                return $"--[[TODO: {ownerName}.{memberName}]] nil";
            }

            // ── .NET string/collection method rewrites ──

            // Resolve the actual Luau name for the owner
            string luauOwner;
            if (_isInstanceContext && _instanceFields.Contains(ownerName)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName))
            {
                luauOwner = $"self.{ownerName}";
            }
            else
            {
                luauOwner = ownerName;
            }

            // List<T>.Add(item) → table.insert(list, item)
            if (memberName == "Add")
            {
                return $"table.insert({luauOwner}, {argStr})";
            }

            // List<T>.Clear() → table.clear(list)
            if (memberName == "Clear")
            {
                return $"table.clear({luauOwner})";
            }

            // string.Substring(start, length) → string.sub(str, start + 1, start + length)
            if (memberName == "Substring" && args.Count == 2)
            {
                return $"string.sub({luauOwner}, {args[0]} + 1, {args[0]} + {args[1]})";
            }

            // string.Substring(start) → string.sub(str, start + 1)
            if (memberName == "Substring" && args.Count == 1)
            {
                return $"string.sub({luauOwner}, {args[0]} + 1)";
            }

            // string.Contains(s) → string.find(str, s, 1, true) ~= nil
            if (memberName == "Contains" && args.Count == 1)
            {
                return $"(string.find({luauOwner}, {args[0]}, 1, true) ~= nil)";
            }

            // string.StartsWith(s) → (string.sub(str, 1, #s) == s)
            if (memberName == "StartsWith" && args.Count == 1)
            {
                return $"(string.sub({luauOwner}, 1, #{args[0]}) == {args[0]})";
            }

            // string.ToLower() → string.lower(str)
            if (memberName == "ToLower" && args.Count == 0)
            {
                return $"string.lower({luauOwner})";
            }

            // string.ToUpper() → string.upper(str)
            if (memberName == "ToUpper" && args.Count == 0)
            {
                return $"string.upper({luauOwner})";
            }

            // .ToString() → tostring(obj)
            if (memberName == "ToString" && args.Count == 0)
            {
                return $"tostring({luauOwner})";
            }

            // If the owner is an instance field, dispatch the method call.
            // Use the global overload map to resolve the correct method name,
            // and use Type.Method(self.field, args) for proper dispatch.
            if (_isInstanceContext && _instanceFields.Contains(ownerName)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName))
            {
                // Try to resolve overloaded name via the field's type and global overload map
                if (_instanceFieldTypes.TryGetValue(ownerName, out var fieldType)
                    && GlobalOverloadMap.TryGetValue((fieldType, memberName, args.Count), out var resolvedName))
                {
                    // Track the field type as a referenced module (it's used for explicit dispatch)
                    if (fieldType != _currentClassName && !_nestedTypeNames.Contains(fieldType))
                    {
                        ReferencedModules.Add(fieldType);
                    }

                    // Use Type.Method(self.field, args) for explicit dispatch with correct overload
                    var fullArgs = string.IsNullOrEmpty(argStr)
                        ? luauOwner
                        : $"{luauOwner}, {argStr}";
                    return $"{fieldType}.{resolvedName}({fullArgs})";
                }

                // Fallback: use colon syntax (works via __index for non-overloaded methods)
                return $"{luauOwner}:{memberName}({argStr})";
            }

            // Calls to our own type's static methods stay as ClassName.Method
            if (ownerName == _currentClassName)
            {
                return $"{ownerName}.{memberName}({argStr})";
            }

            // Other type calls (external)
            if (IsLikelyEnumOrExternalType(ownerName)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName))
            {
                ReferencedModules.Add(ownerName);
            }

            // If the owner is a parameter or local variable (not a static/enum type),
            // use colon syntax for instance method dispatch
            if (_currentMethodParams.Contains(ownerName) || _currentMethodLocals.Contains(ownerName))
            {
                return $"{luauOwner}:{memberName}({argStr})";
            }

            return $"{luauOwner}.{memberName}({argStr})";
        }

        // ── Handle PredefinedType method calls: string.IsNullOrEmpty etc. ──
        if (memberAccess.Expression is PredefinedTypeSyntax predefinedType)
        {
            var typeStr = predefinedType.Keyword.Text;

            // string.IsNullOrEmpty(s) → (s == nil or s == "")
            if (typeStr == "string" && memberName == "IsNullOrEmpty" && args.Count == 1)
            {
                return $"({args[0]} == nil or {args[0]} == \"\")";
            }

            // Try MethodMapper fallback for predefined type calls (string.Equals, int.Parse, etc.)
            // Map C# keyword to .NET type name for lookup
            var dotNetTypeName = typeStr switch
            {
                "string" => "String",
                "int" => "Int32",
                "long" => "Int64",
                "double" => "Double",
                "float" => "Single",
                "bool" => "Boolean",
                "char" => "Char",
                "byte" => "Byte",
                "short" => "Int16",
                "decimal" => "Decimal",
                _ => typeStr,
            };
            var mapped = MethodMapper.TryRewriteByName(dotNetTypeName, memberName, "", args.Select(a => a).ToArray());
            if (mapped != null)
            {
                if (mapped.Contains("__rt.")) NeedsRuntime = true;
                return mapped;
            }

            return $"--[[TODO: {typeStr}.{memberName}]] nil";
        }

        // ── Handle nested member access calls ──
        var left = EmitExpression(memberAccess.Expression);

        // .Length → # operator (already handled in EmitMemberAccess, but catch here for method form)

        // For method calls on element access or complex expressions (non-type),
        // use colon syntax for instance method dispatch (e.g., arr[i]:Method())
        if (memberAccess.Expression is ElementAccessExpressionSyntax
            || (memberAccess.Expression is MemberAccessExpressionSyntax nestedMA
                && nestedMA.Expression is not PredefinedTypeSyntax
                && !IsLikelyEnumOrExternalType(nestedMA.Expression.ToString())))
        {
            return $"{left}:{memberName}({argStr})";
        }

        return $"{left}.{memberName}({argStr})";
    }

    /// <summary>
    /// Emit a `new Type(args)` expression → `Type.new(args)`
    /// Also handles collection initializers: `new List<T>()` → `{}`
    /// </summary>
    private string EmitObjectCreation(ObjectCreationExpressionSyntax objCreate)
    {
        var typeName = objCreate.Type.ToString();

        // StringBuilder → empty string (all methods mapped in MethodMapper)
        if (typeName == "StringBuilder" || typeName == "System.Text.StringBuilder")
        {
            // new StringBuilder() → ""
            // new StringBuilder("initial") → "initial"
            var ctorArgs = objCreate.ArgumentList?.Arguments;
            if (ctorArgs != null && ctorArgs.Value.Count > 0)
            {
                var firstArg = EmitExpression(ctorArgs.Value[0].Expression);
                return firstArg; // new StringBuilder("text") → "text"
            }
            return "\"\"";
        }

        // new object() → {} (empty table, used as lock objects and generic instances)
        if (typeName == "object" || typeName == "System.Object")
            return "{}";

        // Strip generic type args for Luau: List<TokenInfo> → just use {}
        if (typeName.StartsWith("List<") || typeName.StartsWith("Dictionary<")
            || typeName.StartsWith("HashSet<") || typeName.StartsWith("Queue<")
            || typeName.StartsWith("Stack<"))
        {
            // Check for collection initializer
            if (objCreate.Initializer != null)
            {
                var elements = objCreate.Initializer.Expressions
                    .Select(e => EmitExpression(e));
                return $"{{ {string.Join(", ", elements)} }}";
            }
            return "{}";
        }

        // Clean up type name (remove namespace qualifications, generics)
        var cleanType = typeName;
        if (cleanType.Contains('.'))
            cleanType = cleanType.Substring(cleanType.LastIndexOf('.') + 1);
        if (cleanType.Contains('<'))
            cleanType = cleanType.Substring(0, cleanType.IndexOf('<'));

        // Track as external module reference
        if (cleanType != _currentClassName && !_nestedTypeNames.Contains(cleanType)
            && IsLikelyEnumOrExternalType(cleanType))
        {
            ReferencedModules.Add(cleanType);
        }

        var args = objCreate.ArgumentList?.Arguments.Select(a => EmitExpression(a.Expression)) ?? Enumerable.Empty<string>();
        var argStr = string.Join(", ", args);

        // Handle object initializer: new T() { Field = value, ... }
        if (objCreate.Initializer != null && objCreate.Initializer.Kind() == SyntaxKind.ObjectInitializerExpression)
        {
            var constructorCall = $"{cleanType}.new({argStr})";
            var sb = new StringBuilder();
            sb.Append($"(function() local __o = {constructorCall}; ");
            foreach (var expr in objCreate.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assign)
                {
                    var name = assign.Left.ToString();
                    var value = EmitExpression(assign.Right);
                    sb.Append($"__o.{name} = {value}; ");
                }
            }
            sb.Append("return __o end)()");
            return sb.ToString();
        }

        // Handle collection initializer: new T() { item1, item2, ... }
        if (objCreate.Initializer != null && objCreate.Initializer.Kind() == SyntaxKind.CollectionInitializerExpression)
        {
            var elements = objCreate.Initializer.Expressions
                .Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", elements)} }}";
        }

        return $"{cleanType}.new({argStr})";
    }

    /// <summary>
    /// Handle `new() { ... }` (target-typed new) — emit as table constructor.
    /// </summary>
    private string EmitImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax implicitCreate)
    {
        var args = implicitCreate.ArgumentList.Arguments.Select(a => EmitExpression(a.Expression));
        var argStr = string.Join(", ", args);

        // Without a type name, we can't emit a proper constructor call.
        // If there's an initializer, emit as table literal.
        if (implicitCreate.Initializer != null)
        {
            var elements = implicitCreate.Initializer.Expressions
                .Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", elements)} }}";
        }

        return $"--[[TODO: implicit new]] nil";
    }

    /// <summary>
    /// Handle `new Type[size]` array creation → `table.create(size)` or `{}`.
    /// Also handles `new Type[] { items }` → `{ items }`.
    /// </summary>
    private string EmitArrayCreation(ArrayCreationExpressionSyntax arrayCreate)
    {
        // If there's an initializer with elements: new int[] { 1, 2, 3 } → { 1, 2, 3 }
        if (arrayCreate.Initializer != null && arrayCreate.Initializer.Expressions.Count > 0)
        {
            var elements = arrayCreate.Initializer.Expressions.Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", elements)} }}";
        }

        // new Type[size] → table.create(size)
        if (arrayCreate.Type.RankSpecifiers.Count > 0)
        {
            var rankSpec = arrayCreate.Type.RankSpecifiers[0];
            if (rankSpec.Sizes.Count > 0 && rankSpec.Sizes[0] is not OmittedArraySizeExpressionSyntax)
            {
                var size = EmitExpression(rankSpec.Sizes[0]);
                return $"table.create({size})";
            }
        }

        // Fallback: empty table
        return "{}";
    }

    /// <summary>
    /// Handle element access: arr[index], str[index], dict[key]
    /// For strings: str[index] → string.byte(str, index + 1) (0→1 indexing, char as number)
    /// For arrays/lists: arr[index] → arr[index + 1] (0→1 indexing)
    /// </summary>
    private string EmitElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        var obj = EmitExpression(elementAccess.Expression);
        var args = elementAccess.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            // Multi-dimensional — just emit as-is
            var indices = args.Select(a => EmitExpression(a.Expression));
            return $"{obj}[{string.Join(", ", indices)}]";
        }

        var index = EmitExpression(args[0].Expression);

        // Check if target is a string → string.byte()
        if (_model != null)
        {
            var receiverType = GetExpressionType(elementAccess.Expression);
            if (receiverType?.SpecialType == SpecialType.System_String)
                return $"string.byte({obj}, {index} + 1)";

            // Dictionary access: no 0→1 offset
            if (receiverType?.Name is "Dictionary" or "IDictionary" or "ConcurrentDictionary"
                || (receiverType?.AllInterfaces.Any(i => i.Name == "IDictionary") ?? false))
                return $"{obj}[{index}]";
        }
        else if (IsLikelyStringAccess(elementAccess.Expression))
        {
            return $"string.byte({obj}, {index} + 1)";
        }

        // Default: array/list access with 0→1 index adjustment
        return $"{obj}[{index} + 1]";
    }

    /// <summary>
    /// Heuristic to determine if an element access target is a string.
    /// Checks for known string field names and types.
    /// </summary>
    private bool IsLikelyStringAccess(ExpressionSyntax expr)
    {
        // Check for identifiers named _text, text, _source, source, etc.
        if (expr is IdentifierNameSyntax ident)
        {
            var name = ident.Identifier.Text;
            return name is "_text" or "text" or "_source" or "source" or "_str" or "str"
                or "_input" or "input" or "_content" or "content";
        }

        // this._text
        if (expr is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is ThisExpressionSyntax)
        {
            var name = memberAccess.Name.Identifier.Text;
            return name is "_text" or "text" or "_source" or "source" or "_str" or "str"
                or "_input" or "input" or "_content" or "content";
        }

        return false;
    }

    /// <summary>
    /// Handle assignment as an expression (rare in C#, but valid).
    /// </summary>
    private string EmitAssignmentExpression(AssignmentExpressionSyntax assignment)
    {
        // Most assignments in C# are expression statements; in Luau they're always statements.
        // When forced into expression position, emit the right-hand side.
        return EmitExpression(assignment.Right);
    }

    private string EmitBinary(BinaryExpressionSyntax binary)
    {
        // Handle 'is' expression: x is T → type check
        if (binary.Kind() == SyntaxKind.IsExpression)
        {
            var isLeft = EmitExpression(binary.Left);
            var typeName = binary.Right.ToString();
            return EmitTypeCheck(isLeft, typeName);
        }

        // Handle 'as' expression: x as T → just emit x (type erasure in Luau)
        if (binary.Kind() == SyntaxKind.AsExpression)
        {
            return EmitExpression(binary.Left);
        }

        // Handle null-coalescing early (before emitting children) since it has special syntax
        if (binary.Kind() == SyntaxKind.CoalesceExpression)
        {
            var coalLeft = EmitExpression(binary.Left);
            var coalRight = EmitExpression(binary.Right);
            // If left is a complex expression (not a simple identifier/literal), hoist to a temp
            // to avoid evaluating it twice. Common case: settings?.Prop ?? default
            bool isSimple = binary.Left is IdentifierNameSyntax
                || binary.Left is LiteralExpressionSyntax
                || binary.Left is ThisExpressionSyntax;
            if (!isSimple)
            {
                var tempName = $"__coal_{_evalTempCounter++}";
                AppendLine($"local {tempName} = {coalLeft}");
                return $"(if {tempName} ~= nil then {tempName} else {coalRight})";
            }
            return $"(if {coalLeft} ~= nil then {coalLeft} else {coalRight})";
        }

        // Detect string concatenation: + with a string expression → ..
        bool isTopLevelStringConcat = false;
        if (binary.Kind() == SyntaxKind.AddExpression && !_forceStringConcat)
        {
            if (_model != null)
            {
                // SemanticModel: check if the + resolves to string concat
                var typeInfo = _model.GetTypeInfo(binary);
                if (typeInfo.Type?.SpecialType == SpecialType.System_String)
                {
                    isTopLevelStringConcat = true;
                    _forceStringConcat = true;
                }
            }
            else
            {
                // Fallback: old heuristic (when no SemanticModel)
                if (IsStringConcatenation(binary) || IsStringConcatenationChain(binary))
                {
                    isTopLevelStringConcat = true;
                    _forceStringConcat = true;
                }
            }
        }

        // Evaluation order fix: Luau's register-based VM doesn't materialize simple local
        // variable reads into temp registers, so if the right operand is a function call that
        // mutates a field used on the left, the left sees the post-mutation value.
        // C# guarantees left-to-right evaluation. We hoist the left into a temp when:
        //   1. Left is a simple identifier that resolves to a field/property (mutable by calls)
        //   2. Right subtree contains any invocation (could have side effects)
        // We do NOT hoist locals/parameters — they can't be mutated by a call in Luau.
        string left;
        bool needsHoist = false;
        if (binary.Left is IdentifierNameSyntax leftIdent && ContainsInvocation(binary.Right))
        {
            if (_model != null)
            {
                var sym = GetSymbol(leftIdent);
                // Only hoist fields and properties — locals/params are safe
                needsHoist = sym is IFieldSymbol or IPropertySymbol;
            }
            else
            {
                // Without semantic model, conservatively hoist
                needsHoist = true;
            }
        }
        if (needsHoist)
        {
            var rawLeft = EmitExpression(binary.Left);
            var tempName = $"__eval_{_evalTempCounter++}";
            AppendLine($"local {tempName} = {rawLeft}");
            left = tempName;
        }
        else
        {
            left = EmitExpression(binary.Left);
        }
        var right = EmitExpression(binary.Right);

        // Restore flag if we set it at this level
        if (isTopLevelStringConcat)
        {
            _forceStringConcat = false;
        }

        // Use .. for string concatenation (either detected here or forced by parent)
        if (binary.Kind() == SyntaxKind.AddExpression && (_forceStringConcat || isTopLevelStringConcat))
        {
            return $"{left} .. {right}";
        }

        var op = binary.Kind() switch
        {
            SyntaxKind.AddExpression => "+",
            SyntaxKind.SubtractExpression => "-",
            SyntaxKind.MultiplyExpression => "*",
            SyntaxKind.DivideExpression => "/",
            SyntaxKind.ModuloExpression => "%",
            SyntaxKind.EqualsExpression => "==",
            SyntaxKind.NotEqualsExpression => "~=",
            SyntaxKind.LessThanExpression => "<",
            SyntaxKind.LessThanOrEqualExpression => "<=",
            SyntaxKind.GreaterThanExpression => ">",
            SyntaxKind.GreaterThanOrEqualExpression => ">=",
            SyntaxKind.LogicalAndExpression => "and",
            SyntaxKind.LogicalOrExpression => "or",
            SyntaxKind.BitwiseAndExpression => null, // handled below
            SyntaxKind.BitwiseOrExpression => null,
            SyntaxKind.ExclusiveOrExpression => null,
            SyntaxKind.LeftShiftExpression => null,
            SyntaxKind.RightShiftExpression => null,
            _ => null
        };

        if (op != null)
        {
            return $"{left} {op} {right}";
        }

        // Bitwise operations → bit32 functions
        return binary.Kind() switch
        {
            SyntaxKind.BitwiseAndExpression => $"bit32.band({left}, {right})",
            SyntaxKind.BitwiseOrExpression => $"bit32.bor({left}, {right})",
            SyntaxKind.ExclusiveOrExpression => $"bit32.bxor({left}, {right})",
            SyntaxKind.LeftShiftExpression => $"bit32.lshift({left}, {right})",
            SyntaxKind.RightShiftExpression => $"bit32.rshift({left}, {right})",
            _ => $"--[[TODO: binary {binary.Kind()}]] ({left} ?? {right})"
        };
    }

    private string EmitPrefixUnary(PrefixUnaryExpressionSyntax prefixUnary)
    {
        var operand = EmitExpression(prefixUnary.Operand);
        return prefixUnary.Kind() switch
        {
            SyntaxKind.LogicalNotExpression => $"not {operand}",
            SyntaxKind.UnaryMinusExpression => $"-{operand}",
            SyntaxKind.UnaryPlusExpression => operand,
            SyntaxKind.BitwiseNotExpression => $"bit32.bnot({operand})",
            SyntaxKind.PreIncrementExpression => $"({operand} + 1)", // approximation
            SyntaxKind.PreDecrementExpression => $"({operand} - 1)", // approximation
            _ => $"--[[TODO: prefix {prefixUnary.Kind()}]] {operand}"
        };
    }

    private string EmitThrowExpression(ThrowExpressionSyntax throwExpr)
    {
        // throw expressions in expression context → error() wrapped in IIFE
        var inner = EmitExpression(throwExpr.Expression);
        return $"(function() error({inner}) end)()";
    }

    private string EmitPostfixUnary(PostfixUnaryExpressionSyntax postfix)
    {
        var operand = EmitExpression(postfix.Operand);
        return postfix.Kind() switch
        {
            SyntaxKind.PostIncrementExpression => $"({operand} + 1)", // approximation
            SyntaxKind.PostDecrementExpression => $"({operand} - 1)", // approximation
            SyntaxKind.SuppressNullableWarningExpression => operand, // just drop the !
            _ => $"--[[TODO: postfix {postfix.Kind()}]] {operand}"
        };
    }

    private string EmitParenthesized(ParenthesizedExpressionSyntax paren)
    {
        var inner = EmitExpression(paren.Expression);
        return $"({inner})";
    }

    private string EmitCast(CastExpressionSyntax cast)
    {
        var inner = EmitExpression(cast.Expression);
        var targetType = cast.Type.ToString();

        // Enum casts: (int)kind → kind, (SyntaxKind)7 → 7
        // Numeric casts: (int)x → x, (char)x → x
        // These are all no-ops in Luau since everything is number
        if (targetType is "int" or "uint" or "long" or "ulong"
            or "short" or "ushort" or "byte" or "sbyte"
            or "float" or "double" or "char"
            || IsLikelyEnumOrExternalType(targetType))
        {
            return inner;
        }

        return inner; // Default: drop the cast
    }

    private string EmitConditional(ConditionalExpressionSyntax conditional)
    {
        var condition = EmitExpression(conditional.Condition);
        var whenTrue = EmitExpression(conditional.WhenTrue);
        var whenFalse = EmitExpression(conditional.WhenFalse);
        // Luau ternary: wrap in parens for correct precedence in larger expressions
        return $"(if {condition} then {whenTrue} else {whenFalse})";
    }

    /// <summary>
    /// Emit a switch expression as an inline if/elseif expression (Luau's if-then-else expression).
    /// This handles switch expressions used in non-return contexts.
    /// For return contexts, EmitSwitchExpressionAsReturn is used instead.
    /// </summary>
    private string EmitSwitchExpression(SwitchExpressionSyntax switchExpr)
    {
        // For switch expressions used as expressions (not in return position),
        // we build a nested if-then-else expression.
        var governing = EmitExpression(switchExpr.GoverningExpression);

        // Find default arm
        var defaultArm = switchExpr.Arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
        var caseArms = switchExpr.Arms.Where(a => a.Pattern is not DiscardPatternSyntax).ToList();

        if (caseArms.Count == 0 && defaultArm != null)
        {
            return EmitExpression(defaultArm.Expression);
        }

        // Build nested: if cond1 then val1 elseif cond2 then val2 ... else default
        var sb = new StringBuilder();
        for (int i = 0; i < caseArms.Count; i++)
        {
            var arm = caseArms[i];
            var pattern = EmitSwitchPattern(governing, arm.Pattern);
            var value = EmitExpression(arm.Expression);

            if (i == 0)
                sb.Append($"if {pattern} then {value}");
            else
                sb.Append($" elseif {pattern} then {value}");
        }

        if (defaultArm != null)
        {
            sb.Append($" else {EmitExpression(defaultArm.Expression)}");
        }
        else
        {
            sb.Append(" else nil");
        }

        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Lambda expression emission
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emit a parenthesized lambda: (x, y) => expr  →  function(x, y) return expr end
    /// Or (x, y) => { stmts }  →  function(x, y) stmts end
    /// </summary>
    private string EmitParenthesizedLambda(ParenthesizedLambdaExpressionSyntax lambda)
    {
        var paramParts = new List<string>();
        foreach (var param in lambda.ParameterList.Parameters)
        {
            var paramName = EscapeIdentifier(param.Identifier.Text);
            if (param.Type != null)
            {
                var paramType = MapTypeNode(param.Type);
                paramParts.Add($"{paramName}: {paramType}");
            }
            else
            {
                paramParts.Add(paramName);
            }
        }
        var paramStr = string.Join(", ", paramParts);

        if (lambda.ExpressionBody != null)
        {
            var body = EmitExpression(lambda.ExpressionBody);
            return $"function({paramStr}) return {body} end";
        }
        else if (lambda.Block != null)
        {
            return EmitLambdaBlockBody(paramStr, lambda.Block);
        }

        return $"function({paramStr}) end";
    }

    /// <summary>
    /// Emit a simple lambda: x => expr  →  function(x) return expr end
    /// Or x => { stmts }  →  function(x) stmts end
    /// </summary>
    private string EmitSimpleLambda(SimpleLambdaExpressionSyntax lambda)
    {
        var paramName = EscapeIdentifier(lambda.Parameter.Identifier.Text);
        string paramStr;
        if (lambda.Parameter.Type != null)
        {
            var paramType = MapTypeNode(lambda.Parameter.Type);
            paramStr = $"{paramName}: {paramType}";
        }
        else
        {
            paramStr = paramName;
        }

        if (lambda.ExpressionBody != null)
        {
            var body = EmitExpression(lambda.ExpressionBody);
            return $"function({paramStr}) return {body} end";
        }
        else if (lambda.Block != null)
        {
            return EmitLambdaBlockBody(paramStr, lambda.Block);
        }

        return $"function({paramStr}) end";
    }

    /// <summary>
    /// Emit a lambda block body as an inline function string.
    /// Captures output by recording the position in the main StringBuilder before and after,
    /// then extracting the emitted text and removing it from the main output.
    /// </summary>
    private string EmitLambdaBlockBody(string paramStr, BlockSyntax block)
    {
        // Record position before emitting
        var startPos = _sb.Length;
        var savedIndent = _indent;
        _indent = 0;
        EmitBlock(block);
        // Extract the emitted text
        var bodyStr = _sb.ToString(startPos, _sb.Length - startPos).TrimEnd('\r', '\n');
        // Remove the emitted text from the main output
        _sb.Length = startPos;
        _indent = savedIndent;

        // For single-line blocks, emit inline
        var lines = bodyStr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 1)
        {
            return $"function({paramStr}) {lines[0].Trim()} end";
        }

        // Multi-line: emit on separate lines
        var result = new StringBuilder();
        result.Append($"function({paramStr})");
        foreach (var line in lines)
        {
            result.Append($"\n{new string('\t', _indent + 1)}{line.Trim()}");
        }
        result.Append($"\n{new string('\t', _indent)}end");
        return result.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Null-conditional access emission
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emit a conditional access expression: obj?.Property → if obj ~= nil then obj.Property else nil
    /// obj?.Method() → if obj ~= nil then obj:Method() else nil
    /// </summary>
    private string EmitConditionalAccess(ConditionalAccessExpressionSyntax conditionalAccess)
    {
        var target = EmitExpression(conditionalAccess.Expression);
        var binding = EmitConditionalBinding(target, conditionalAccess.WhenNotNull);
        // Wrap in parens so comparison operators bind correctly:
        // (if x ~= nil then x.Y else nil) == Z, not: if x ~= nil then x.Y else (nil == Z)
        return $"(if {target} ~= nil then {binding} else nil)";
    }

    /// <summary>
    /// Emit the WhenNotNull part of a conditional access, resolving MemberBindingExpression
    /// and ElementBindingExpression relative to the target expression.
    /// </summary>
    private string EmitConditionalBinding(string target, ExpressionSyntax whenNotNull)
    {
        return whenNotNull switch
        {
            MemberBindingExpressionSyntax memberBinding =>
                $"{target}.{memberBinding.Name.Identifier.Text}",
            InvocationExpressionSyntax invocation when invocation.Expression is MemberBindingExpressionSyntax methodBinding =>
                EmitConditionalMethodInvocation(target, methodBinding, invocation.ArgumentList.Arguments),
            ElementBindingExpressionSyntax elementBinding =>
                $"{target}[{string.Join(", ", elementBinding.ArgumentList.Arguments.Select(a => EmitExpression(a.Expression)))}]",
            _ => $"{target}.{EmitExpression(whenNotNull)}"
        };
    }

    /// <summary>
    /// Emit a conditional method invocation: obj?.Method(args)
    /// For instance methods uses colon syntax: obj:Method(args)
    /// </summary>
    private string EmitConditionalMethodInvocation(string target, MemberBindingExpressionSyntax methodBinding, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var methodName = methodBinding.Name.Identifier.Text;
        var args = arguments.Select(a => EmitExpression(a.Expression));
        var argStr = string.Join(", ", args);
        return $"{target}:{methodName}({argStr})";
    }

    // ────────────────────────────────────────────────────────────────────
    //  Type mapping helpers
    // ────────────────────────────────────────────────────────────────────

    private static string MapReturnType(TypeSyntax type)
    {
        var typeStr = type.ToString();
        return MapComplexType(typeStr);
    }

    private static string MapTypeNode(TypeSyntax? type)
    {
        if (type == null) return "any";
        var typeStr = type.ToString();
        return MapComplexType(typeStr);
    }

    /// <summary>
    /// Track external type references from a C# type string.
    /// Adds any non-primitive, non-nested type names to ReferencedModules.
    /// </summary>
    private void TrackTypeReferences(string typeStr)
    {
        // Strip nullable
        typeStr = typeStr.TrimEnd('?');

        // Strip array brackets
        if (typeStr.EndsWith("[]"))
            typeStr = typeStr.Substring(0, typeStr.Length - 2);

        // Handle generic types: extract base type and type args
        if (typeStr.Contains('<'))
        {
            var baseType = typeStr.Substring(0, typeStr.IndexOf('<'));
            // Don't track known collection types
            if (baseType is not "List" and not "Dictionary" and not "HashSet"
                and not "IList" and not "IDictionary" and not "IEnumerable"
                and not "Queue" and not "Stack" and not "IReadOnlyList")
            {
                if (IsLikelyEnumOrExternalType(baseType) && baseType != _currentClassName
                    && !_nestedTypeNames.Contains(baseType))
                {
                    ReferencedModules.Add(baseType);
                }
            }

            // Track type arguments
            var genArgs = ExtractGenericArgs(typeStr);
            if (genArgs != null)
            {
                foreach (var arg in genArgs)
                    TrackTypeReferences(arg.Trim());
            }
            return;
        }

        // Handle dot-qualified nested types (e.g., "SimpleTokenizer.TokenInfo")
        // Only track the leaf type name — it will be resolved to its parent module by InsertRequires
        if (typeStr.Contains('.'))
        {
            var parts = typeStr.Split('.');
            var leafType = parts[parts.Length - 1];
            if (TypeMapper.MapType(leafType) == null
                && leafType != _currentClassName
                && !_nestedTypeNames.Contains(leafType)
                && IsLikelyEnumOrExternalType(leafType))
            {
                ReferencedModules.Add(leafType);
            }
            return;
        }

        // Plain type name
        if (TypeMapper.MapType(typeStr) == null
            && typeStr != _currentClassName
            && !_nestedTypeNames.Contains(typeStr)
            && IsLikelyEnumOrExternalType(typeStr))
        {
            ReferencedModules.Add(typeStr);
        }
    }

    /// <summary>
    /// Map a C# type string to a Luau type, handling generics and arrays.
    /// </summary>
    private static string MapComplexType(string typeStr)
    {
        // Handle nullable
        if (typeStr.EndsWith("?"))
        {
            var inner = MapComplexType(typeStr.TrimEnd('?'));
            return $"{inner}?";
        }

        // Handle arrays: T[] → { T }
        if (typeStr.EndsWith("[]"))
        {
            var inner = MapComplexType(typeStr.Substring(0, typeStr.Length - 2));
            return $"{{ {inner} }}";
        }

        // Handle List<T>, IList<T>, etc. → { T }
        if (typeStr.StartsWith("List<") || typeStr.StartsWith("IList<")
            || typeStr.StartsWith("IEnumerable<") || typeStr.StartsWith("IReadOnlyList<"))
        {
            var inner = ExtractGenericArg(typeStr);
            if (inner != null)
                return $"{{ {MapComplexType(inner)} }}";
        }

        // Handle Dictionary<K,V> → { [K]: V }
        if (typeStr.StartsWith("Dictionary<") || typeStr.StartsWith("IDictionary<"))
        {
            var genArgs = ExtractGenericArgs(typeStr);
            if (genArgs != null && genArgs.Length == 2)
                return $"{{ [{MapComplexType(genArgs[0])}]: {MapComplexType(genArgs[1])} }}";
        }

        // Try primitive mapping
        var mapped = TypeMapper.MapType(typeStr);
        if (mapped != null) return mapped;

        // Strip namespace qualifiers
        if (typeStr.Contains('.'))
            typeStr = typeStr.Substring(typeStr.LastIndexOf('.') + 1);

        // Strip generic args for type names used as-is (e.g., HashSet<string> → any)
        if (typeStr.Contains('<'))
            typeStr = typeStr.Substring(0, typeStr.IndexOf('<'));

        // Try mapping the cleaned type
        mapped = TypeMapper.MapType(typeStr);
        if (mapped != null) return mapped;

        return typeStr;
    }

    /// <summary>
    /// Extract the single generic type argument from "List&lt;string&gt;" → "string"
    /// </summary>
    private static string? ExtractGenericArg(string typeStr)
    {
        var start = typeStr.IndexOf('<');
        var end = typeStr.LastIndexOf('>');
        if (start >= 0 && end > start)
            return typeStr.Substring(start + 1, end - start - 1).Trim();
        return null;
    }

    /// <summary>
    /// Extract multiple generic type arguments, respecting nested generics.
    /// </summary>
    private static string[]? ExtractGenericArgs(string typeStr)
    {
        var start = typeStr.IndexOf('<');
        var end = typeStr.LastIndexOf('>');
        if (start < 0 || end <= start) return null;

        var inner = typeStr.Substring(start + 1, end - start - 1);
        var results = new List<string>();
        int depth = 0;
        int segStart = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '<') depth++;
            else if (inner[i] == '>') depth--;
            else if (inner[i] == ',' && depth == 0)
            {
                results.Add(inner.Substring(segStart, i - segStart).Trim());
                segStart = i + 1;
            }
        }
        results.Add(inner.Substring(segStart).Trim());
        return results.ToArray();
    }

    /// <summary>
    /// Heuristic: detect string concatenation (+ with a string literal on either side).
    /// Without semantic analysis, we check for string literal operands or method calls
    /// that are known to return strings (e.g., GetText).
    /// </summary>
    private static bool IsStringConcatenation(BinaryExpressionSyntax binary)
    {
        return IsStringExpression(binary.Left) || IsStringExpression(binary.Right);
    }

    private static bool IsStringExpression(ExpressionSyntax expr)
    {
        // Direct string literal: "hello"
        if (expr is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression)
            return true;

        // Method calls that likely return string
        if (expr is InvocationExpressionSyntax invocation)
        {
            var name = invocation.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => null
            };
            if (name is "GetText" or "ToString" or "Substring" or "Accept"
                or "ToDisplayString" or "ToLower" or "ToUpper" or "Trim")
                return true;
        }

        // Member access ending in string-like property names
        if (expr is MemberAccessExpressionSyntax memberAccess)
        {
            var memberName = memberAccess.Name.Identifier.Text;
            if (memberName is "Text" or "Name" or "Value" or "ReturnType"
                or "TypeName" or "VariableName")
                return true;
        }

        // Another binary + that contains a string
        if (expr is BinaryExpressionSyntax innerBinary && innerBinary.Kind() == SyntaxKind.AddExpression)
            return IsStringConcatenation(innerBinary);

        return false;
    }

    /// <summary>
    /// Deep-scan an entire chain of + binary expressions to detect if ANY leaf
    /// in the chain is a known string expression. This allows us to mark the entire
    /// chain as string concatenation even when the innermost nodes lack string evidence.
    /// </summary>
    private static bool IsStringConcatenationChain(BinaryExpressionSyntax binary)
    {
        return ContainsStringLeaf(binary);
    }

    /// <summary>
    /// Recursively scan all leaves of a + chain for any string expression.
    /// Unlike IsStringConcatenation which only checks direct children,
    /// this walks the full tree of chained + operators.
    /// </summary>
    private static bool ContainsStringLeaf(ExpressionSyntax expr)
    {
        if (expr is BinaryExpressionSyntax binary && binary.Kind() == SyntaxKind.AddExpression)
        {
            return ContainsStringLeaf(binary.Left) || ContainsStringLeaf(binary.Right);
        }
        return IsStringExpression(expr);
    }

    /// <summary>
    /// Heuristic: is this identifier likely an enum/external type used in member access?
    /// Returns true for PascalCase identifiers that aren't our own class.
    /// </summary>
    private static bool IsLikelyEnumOrExternalType(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // PascalCase heuristic: starts with uppercase, has at least 2 chars
        return char.IsUpper(name[0]) && name.Length >= 2;
    }

    /// <summary>
    /// Emit a unified return statement for all top-level types emitted in this file.
    /// Called by the orchestrator when SuppressReturn is true (multi-type files).
    /// </summary>
    public void EmitUnifiedReturn()
    {
        if (EmittedTopLevelTypes.Count == 0) return;

        var entries = EmittedTopLevelTypes.Select(t => $"{t} = {t}");
        AppendLine($"return {{ {string.Join(", ", entries)} }}");
    }

    // ────────────────────────────────────────────────────────────────────
    // ────────────────────────────────────────────────────────────────────
    //  Evaluation order helpers
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Check if an expression subtree contains any invocation (method call, delegate call, etc.)
    /// that could potentially have side effects on local variables.
    /// </summary>
    private static bool ContainsInvocation(ExpressionSyntax expr)
    {
        if (expr is InvocationExpressionSyntax or ObjectCreationExpressionSyntax)
            return true;
        foreach (var child in expr.DescendantNodes())
        {
            if (child is InvocationExpressionSyntax or ObjectCreationExpressionSyntax)
                return true;
        }
        return false;
    }

    //  Output helpers
    // ────────────────────────────────────────────────────────────────────

    public void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
            return;
        }
        _sb.Append(new string('\t', _indent));
        _sb.AppendLine(line);
    }

    /// <summary>
    /// Insert text at a specific position in the output buffer.
    /// Used by the orchestrator to insert require() statements after the header.
    /// </summary>
    public void InsertAfterHeader(string text)
    {
        // Find the end of the header (after the blank line following "-- Do not edit manually")
        var output = _sb.ToString();
        var headerEnd = output.IndexOf("-- Do not edit manually", StringComparison.Ordinal);
        if (headerEnd >= 0)
        {
            // Find the end of that line + the blank line
            var lineEnd = output.IndexOf('\n', headerEnd);
            if (lineEnd >= 0)
            {
                // Skip the blank line after the header comment
                var nextLineEnd = output.IndexOf('\n', lineEnd + 1);
                if (nextLineEnd >= 0)
                {
                    _sb.Insert(nextLineEnd + 1, text);
                    return;
                }
            }
        }
        // Fallback: just prepend after header
        _sb.Insert(0, text);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Interpolated strings: $"..." → `...`
    // ────────────────────────────────────────────────────────────────────

    private string EmitInterpolatedString(InterpolatedStringExpressionSyntax interp)
    {
        var sb = new StringBuilder("`");
        foreach (var content in interp.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                var raw = text.TextToken.Text;
                raw = raw.Replace("\\n", "\n").Replace("\\t", "\t")
                         .Replace("\\r", "\r").Replace("\\\"", "\"");
                sb.Append(raw);
            }
            else if (content is InterpolationSyntax hole)
            {
                sb.Append('{');
                sb.Append(EmitExpression(hole.Expression));
                sb.Append('}');
            }
        }
        sb.Append('`');
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    //  is/as pattern matching
    // ────────────────────────────────────────────────────────────────────

    private string EmitIsPattern(IsPatternExpressionSyntax isPattern)
    {
        var left = EmitExpression(isPattern.Expression);
        return EmitPattern(left, isPattern.Pattern);
    }

    private string EmitPattern(string subject, PatternSyntax pattern)
    {
        return pattern switch
        {
            ConstantPatternSyntax cp => $"{subject} == {EmitExpression(cp.Expression)}",
            DeclarationPatternSyntax dp => EmitDeclarationPattern(subject, dp),
            UnaryPatternSyntax np when np.IsKind(SyntaxKind.NotPattern) =>
                $"not ({EmitPattern(subject, np.Pattern)})",
            BinaryPatternSyntax bp when bp.IsKind(SyntaxKind.OrPattern) =>
                $"({EmitPattern(subject, bp.Left)} or {EmitPattern(subject, bp.Right)})",
            BinaryPatternSyntax bp when bp.IsKind(SyntaxKind.AndPattern) =>
                $"({EmitPattern(subject, bp.Left)} and {EmitPattern(subject, bp.Right)})",
            RelationalPatternSyntax rp => $"{subject} {rp.OperatorToken.Text} {EmitExpression(rp.Expression)}",
            DiscardPatternSyntax => "true",
            TypePatternSyntax tp => EmitTypeCheck(subject, tp.Type.ToString()),
            VarPatternSyntax => "true", // var pattern always matches
            _ => $"--[[TODO: pattern {pattern.Kind()}]] true",
        };
    }

    private string EmitDeclarationPattern(string subject, DeclarationPatternSyntax dp)
    {
        var typeName = dp.Type.ToString();
        // Register pattern variable alias: `m is PropertyInfo p` → p aliases m
        if (dp.Designation is SingleVariableDesignationSyntax svd)
        {
            var varName = EscapeIdentifier(svd.Identifier.Text);
            _patternVarAliases[varName] = subject;
            _currentMethodLocals.Add(svd.Identifier.Text);
            if (varName != svd.Identifier.Text) _currentMethodLocals.Add(varName);
        }
        return EmitTypeCheck(subject, typeName);
    }

    private string EmitTypeCheck(string subject, string typeName)
    {
        return typeName switch
        {
            "string" => $"(type({subject}) == \"string\")",
            "int" or "float" or "double" or "long" or "decimal" or "number" =>
                $"(type({subject}) == \"number\")",
            "bool" or "boolean" => $"(type({subject}) == \"boolean\")",
            _ => $"({subject} ~= nil)" // reference type check
        };
    }

    // ────────────────────────────────────────────────────────────────────
    //  await expression: await expr → direct call or task.wait
    // ────────────────────────────────────────────────────────────────────

    private string EmitAwait(AwaitExpressionSyntax awaitExpr)
    {
        // await Task.Delay(ms) → task.wait(ms / 1000)
        if (awaitExpr.Expression is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax ma)
        {
            var typePart = ma.Expression.ToString();
            var methodName = ma.Name.Identifier.Text;

            if (typePart == "Task" && methodName == "Delay")
            {
                var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
                if (arg != null)
                {
                    var ms = EmitExpression(arg.Expression);
                    return $"task.wait({ms} / 1000)";
                }
            }
            if (typePart == "Task" && methodName == "Yield")
                return "task.wait()";
        }

        // General await → just emit the expression (direct call in Luau)
        return EmitExpression(awaitExpr.Expression);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Initializer expressions: { expr, expr, ... }
    // ────────────────────────────────────────────────────────────────────

    private string EmitInitializerExpression(InitializerExpressionSyntax initExpr)
    {
        var items = initExpr.Expressions.Select(e => EmitExpression(e));
        return $"{{ {string.Join(", ", items)} }}";
    }

    private string EmitDeclarationExpression(DeclarationExpressionSyntax declExpr)
    {
        // `out var x` or `out int x` → just emit the variable name
        // The variable declaration is handled by the surrounding statement
        if (declExpr.Designation is SingleVariableDesignationSyntax svd)
        {
            var varName = svd.Identifier.Text;
            if (varName != "_") // discard
            {
                _currentMethodLocals.Add(varName);
            }
            return varName;
        }
        if (declExpr.Designation is DiscardDesignationSyntax)
            return "_";
        if (declExpr.Designation is ParenthesizedVariableDesignationSyntax parenDesig)
        {
            var names = parenDesig.Variables.Select(v =>
                v is SingleVariableDesignationSyntax s ? s.Identifier.Text : "_");
            return string.Join(", ", names);
        }
        return $"--[[TODO: decl {declExpr.Designation.Kind()}]] nil";
    }

    private string EmitImplicitArrayCreation(ImplicitArrayCreationExpressionSyntax implicitArray)
    {
        // new[] { 1, 2, 3 } → { 1, 2, 3 }
        if (implicitArray.Initializer != null)
        {
            var items = implicitArray.Initializer.Expressions.Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", items)} }}";
        }
        return "{}";
    }

    private string EmitTupleExpression(TupleExpressionSyntax tuple)
    {
        var items = tuple.Arguments.Select(a => EmitExpression(a.Expression));
        return $"{{ {string.Join(", ", items)} }}";
    }

    private string EmitRangeExpression(RangeExpressionSyntax range)
    {
        // Range expressions (x..y) — emit as a table { start, end }
        var left = range.LeftOperand != null ? EmitExpression(range.LeftOperand) : "1";
        var right = range.RightOperand != null ? EmitExpression(range.RightOperand) : "-1";
        return $"{{ {left}, {right} }}";
    }

    private string EmitArrayCreation(StackAllocArrayCreationExpressionSyntax stackAlloc)
    {
        // stackalloc T[n] → table.create(n, default)
        if (stackAlloc.Type is ArrayTypeSyntax arrayType && arrayType.RankSpecifiers.Count > 0)
        {
            var rank = arrayType.RankSpecifiers[0];
            if (rank.Sizes.Count > 0 && rank.Sizes[0] is not OmittedArraySizeExpressionSyntax)
            {
                var size = EmitExpression(rank.Sizes[0]);
                return $"table.create({size}, 0)";
            }
        }
        if (stackAlloc.Initializer != null)
        {
            var items = stackAlloc.Initializer.Expressions.Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", items)} }}";
        }
        return "{}";
    }

    private string EmitAnonymousObjectCreation(AnonymousObjectCreationExpressionSyntax anonObj)
    {
        // new { X = 1, Y = 2 } → { X = 1, Y = 2 }
        var parts = new List<string>();
        foreach (var init in anonObj.Initializers)
        {
            var name = init.NameEquals?.Name.Identifier.Text ?? EmitExpression(init.Expression);
            var value = EmitExpression(init.Expression);
            if (init.NameEquals != null)
                parts.Add($"{name} = {value}");
            else
                parts.Add(value);
        }
        return $"{{ {string.Join(", ", parts)} }}";
    }

    private string EmitQueryExpression(QueryExpressionSyntax queryExpr)
    {
        // LINQ query syntax → chained __rt calls
        // Simple approach: emit the from clause source and chain operations
        var source = EmitExpression(queryExpr.FromClause.Expression);
        var result = source;

        foreach (var clause in queryExpr.Body.Clauses)
        {
            switch (clause)
            {
                case WhereClauseSyntax where:
                    var pred = EmitExpression(where.Condition);
                    var rangeVar = queryExpr.FromClause.Identifier.Text;
                    result = $"__rt.where({result}, function({rangeVar}) return {pred} end)";
                    NeedsRuntime = true;
                    break;
                case OrderByClauseSyntax orderBy:
                    foreach (var ordering in orderBy.Orderings)
                    {
                        var key = EmitExpression(ordering.Expression);
                        var desc = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                        var fn = desc ? "orderByDescending" : "orderBy";
                        var rv = queryExpr.FromClause.Identifier.Text;
                        result = $"__rt.{fn}({result}, function({rv}) return {key} end)";
                        NeedsRuntime = true;
                    }
                    break;
                case LetClauseSyntax let:
                    // Let clauses create intermediate variables — complex to inline, emit TODO
                    result = $"--[[TODO: let clause]] {result}";
                    break;
                case FromClauseSyntax fromClause:
                    // Additional from = SelectMany
                    result = $"--[[TODO: multiple from]] {result}";
                    break;
            }
        }

        // Handle the select clause
        if (queryExpr.Body.SelectOrGroup is SelectClauseSyntax select)
        {
            var rangeVar = queryExpr.FromClause.Identifier.Text;
            var selectExpr = EmitExpression(select.Expression);
            // If select just returns the range variable, skip the projection
            if (selectExpr != rangeVar)
            {
                result = $"__rt.select({result}, function({rangeVar}) return {selectExpr} end)";
                NeedsRuntime = true;
            }
        }
        else if (queryExpr.Body.SelectOrGroup is GroupClauseSyntax group)
        {
            var rangeVar = queryExpr.FromClause.Identifier.Text;
            var keyExpr = EmitExpression(group.ByExpression);
            result = $"__rt.groupBy({result}, function({rangeVar}) return {keyExpr} end)";
            NeedsRuntime = true;
        }

        return result;
    }

    /// <summary>
    /// Emit variable bindings for pattern variables before the condition is evaluated.
    /// E.g., `if (obj is string s)` → `local s = obj` before the if condition.
    /// </summary>
    private void EmitPatternVariableBindings(ExpressionSyntax condition)
    {
        if (condition is IsPatternExpressionSyntax isPattern
            && isPattern.Pattern is DeclarationPatternSyntax dp
            && dp.Designation is SingleVariableDesignationSyntax svd)
        {
            var subject = EmitExpression(isPattern.Expression);
            var varName = EscapeIdentifier(svd.Identifier.Text);
            var luauType = MapComplexType(dp.Type.ToString());
            AppendLine($"local {varName}: {luauType} = {subject}");
            _currentMethodLocals.Add(svd.Identifier.Text);
            if (varName != svd.Identifier.Text) _currentMethodLocals.Add(varName);
        }
        // Handle logical AND chains: `if (x is Type a && y is Type b)`
        else if (condition is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            EmitPatternVariableBindings(binary.Left);
            EmitPatternVariableBindings(binary.Right);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Relational patterns in switch expressions: > 80, < 0, etc.
    // ────────────────────────────────────────────────────────────────────

    private string EmitSwitchArm(string subject, SwitchExpressionArmSyntax arm)
    {
        if (arm.Pattern is DiscardPatternSyntax)
            return "true"; // default arm

        return EmitPattern(subject, arm.Pattern);
    }
}
