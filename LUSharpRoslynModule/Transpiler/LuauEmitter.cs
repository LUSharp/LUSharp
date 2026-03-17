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

    /// <summary>Escape a C# identifier if it collides with a Luau reserved word.
    /// Also strips the C# verbatim identifier prefix (@) if present.</summary>
    private static string EscapeIdentifier(string name)
    {
        if (name.Length > 0 && name[0] == '@') name = name.Substring(1);
        return LuauReservedWords.Contains(name) ? name + "_" : name;
    }

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
    private Dictionary<(string Name, int ParamCount, string FirstParamType), string> _overloadMap = new();
    /// <summary>Index-aligned with the methods list for per-method emit name lookup.</summary>
    private List<string> _methodEmitNames = new();

    /// <summary>
    /// Set of type names referenced as member access (e.g., SyntaxKind.X) that need requires.
    /// Populated during emission, consumed by the orchestrator to insert requires.
    /// </summary>
    public HashSet<string> ReferencedModules { get; } = new();

    /// <summary>
    /// Type names used in annotations that are not Luau built-ins and may need 'type X = any' stubs.
    /// Populated by MapComplexType when it returns an unmapped type name.
    /// </summary>
    public HashSet<string> AnnotationTypeNames { get; } = new();

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
    private int _switchDepth = 0;
    private int _loopDepthInSwitch = 0;
    private int _loopDepth = 0;
    private int _hoistCounter = 0;
    private int _tempVarCounter = 0;
    private bool _insidePcallLambda = false;
    private bool _pcallHasContinue = false;
    private bool _pcallHasBreak = false;

    /// <summary>
    /// Out parameters of the current method. When non-empty, return statements
    /// append these as additional return values to emulate C# out parameter semantics.
    /// </summary>
    private List<string> _currentMethodOutParams = new();

    /// <summary>
    /// Incrementor expressions for the enclosing C# for loop, if any.
    /// Must be emitted before every `continue` to match C# for-loop semantics
    /// where the incrementor always runs before re-checking the condition.
    /// </summary>
    private SeparatedSyntaxList<ExpressionSyntax>? _forLoopIncrementors;

    /// <summary>
    /// Module-level deferred static field initializers. Collected from all classes
    /// (including nested) and emitted at the end of the module, just before the return
    /// statement. This ensures all methods from all classes are defined before any
    /// static initializer that references them runs.
    /// </summary>
    private List<(string ClassName, string FieldName, string Value)> _moduleDeferredStatics = new();
    private List<ConstructorDeclarationSyntax> _moduleDeferredStaticCtors = new();

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
        var info = _model.GetSymbolInfo(expr);
        if (info.Symbol != null) return info.Symbol;
        // If resolution failed, try to pick the best candidate by matching argument count
        if (info.CandidateSymbols.Length > 0
            && expr.Parent is InvocationExpressionSyntax invExpr)
        {
            var argCount = invExpr.ArgumentList.Arguments.Count;
            // Prefer exact match, then params match
            var exactMatch = info.CandidateSymbols.OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length == argCount && !m.Parameters.Any(p => p.IsParams));
            if (exactMatch != null) return exactMatch;
            var paramsMatch = info.CandidateSymbols.OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Parameters.Length > 0
                    && m.Parameters[^1].IsParams
                    && argCount >= m.Parameters.Length - 1);
            if (paramsMatch != null) return paramsMatch;
        }
        return info.CandidateSymbols.FirstOrDefault();
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
    public Dictionary<(string TypeName, string MethodName, int ArgCount, string FirstParamType), string> GlobalOverloadMap { get; set; } = new();

    /// <summary>
    /// Full-signature overload map for precise resolution when 4-tuple key is ambiguous.
    /// Key: (TypeName, MethodName, AllParamTypes comma-separated).
    /// </summary>
    public Dictionary<(string TypeName, string MethodName, string AllParamTypes), string> FullSignatureOverloadMap { get; set; } = new();

    /// <summary>
    /// Global base class map: ClassName → BaseClassName.
    /// Populated by PreScan across all files, resolves partial class base types.
    /// </summary>
    public Dictionary<string, string> GlobalBaseClassMap { get; set; } = new();

    /// <summary>
    /// Global set of struct type names (value types with parameterized constructors).
    /// Used to detect default struct construction (new StructType()) which should
    /// zero-initialize instead of calling .new() with nil params.
    /// </summary>
    public HashSet<string> GlobalStructTypes { get; set; } = new();

    /// <summary>
    /// Types known to have a ToString() method (directly or inherited).
    /// Shared across emitter instances so cross-file inheritance chains can propagate __tostring.
    /// </summary>
    public HashSet<string> GlobalTypesWithToString { get; set; } = new();

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
        var resolvedNumericValues = new Dictionary<string, int>();
        int nextAutoValue = 0;
        foreach (var member in enumDecl.Members)
        {
            var memberName = member.Identifier.Text;
            if (member.EqualsValue != null)
            {
                var explicitValue = EmitExpression(member.EqualsValue.Value);
                // Try to parse for auto-increment tracking
                if (int.TryParse(explicitValue, out var parsed))
                {
                    nextAutoValue = parsed + 1;
                    resolvedNumericValues[memberName] = parsed;
                }
                else
                {
                    // For flags enums, try to resolve references to other members
                    // e.g., IgnoreAndPopulate = Ignore | Populate → bit32.bor(1, 2) → 3
                    int? resolved = TryResolveEnumExpression(member.EqualsValue.Value, resolvedNumericValues);
                    if (resolved != null)
                    {
                        explicitValue = resolved.Value.ToString();
                        resolvedNumericValues[memberName] = resolved.Value;
                        nextAutoValue = resolved.Value + 1;
                    }
                    else
                    {
                        nextAutoValue++;
                    }

                }
                memberValues.Add((memberName, explicitValue));
            }
            else
            {
                resolvedNumericValues[memberName] = nextAutoValue;
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

    /// <summary>
    /// Try to resolve an enum member expression to a numeric value by substituting
    /// known member names. Handles patterns like: Ignore | Populate, A | B | C.
    /// </summary>
    private int? TryResolveEnumExpression(ExpressionSyntax expr, Dictionary<string, int> known)
    {
        if (expr is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.NumericLiteralExpression)
        {
            if (int.TryParse(literal.Token.ValueText, out var val))
                return val;
        }
        if (expr is IdentifierNameSyntax ident && known.TryGetValue(ident.Identifier.Text, out var identVal))
            return identVal;
        if (expr is BinaryExpressionSyntax binary)
        {
            var leftVal = TryResolveEnumExpression(binary.Left, known);
            var rightVal = TryResolveEnumExpression(binary.Right, known);
            if (leftVal != null && rightVal != null)
            {
                return binary.Kind() switch
                {
                    SyntaxKind.BitwiseOrExpression => leftVal.Value | rightVal.Value,
                    SyntaxKind.BitwiseAndExpression => leftVal.Value & rightVal.Value,
                    SyntaxKind.AddExpression => leftVal.Value + rightVal.Value,
                    SyntaxKind.LeftShiftExpression => leftVal.Value << rightVal.Value,
                    _ => null
                };
            }
        }
        // Unary: ~expr (bitwise NOT)
        if (expr is PrefixUnaryExpressionSyntax prefix && prefix.Kind() == SyntaxKind.BitwiseNotExpression)
        {
            var operandVal = TryResolveEnumExpression(prefix.Operand, known);
            if (operandVal != null)
                return ~operandVal.Value;
        }
        // MemberAccess like TypeNameHandling.Objects → look up "Objects"
        if (expr is MemberAccessExpressionSyntax ma && known.TryGetValue(ma.Name.Identifier.Text, out var maVal))
            return maVal;
        return null;
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
        var emitNamesUsed = new HashSet<string>();
        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax m && m.ExplicitInterfaceSpecifier == null)
            {
                var name = m.Identifier.Text;
                methodNameCounts[name] = methodNameCounts.GetValueOrDefault(name) + 1;
            }
        }

        // Pre-compute overload map for call-site resolution (same logic as EmitInstanceClass)
        _overloadMap = new Dictionary<(string Name, int ParamCount, string FirstParamType), string>();
        foreach (var member in classDecl.Members)
        {
            if (member is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier == null)
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
                        var isNullableSuffix = suffix.EndsWith("?");
                        suffix = suffix.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                        if (isNullableSuffix) suffix += "_nullable";
                        emitName = $"{name}_{suffix}";
                    }
                }

                if (emitNamesUsed.Contains(emitName))
                {
                    int counter = 2;
                    while (emitNamesUsed.Contains($"{emitName}__{counter}"))
                        counter++;
                    emitName = $"{emitName}__{counter}";
                }
                emitNamesUsed.Add(emitName);

                var fpt = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
                var isNullableFpt = fpt.EndsWith("?");
                fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullableFpt) fpt += "_nullable";
                _overloadMap[(name, paramCount, fpt)] = emitName;
            }
        }

        // Track static/const fields for bare identifier resolution (same as EmitClass)
        _constFields = new HashSet<string>();
        foreach (var member in classDecl.Members)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                bool isStatic = fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword);
                bool isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
                if (isStatic || isConst)
                {
                    foreach (var variable in fieldDecl.Declaration.Variables)
                        _constFields.Add(variable.Identifier.Text);
                }
            }
        }

        // Reset counters for actual emission pass
        methodNameSeen = new Dictionary<string, int>();
        emitNamesUsed = new HashSet<string>();

        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                {
                    // Skip explicit interface implementations
                    if (method.ExplicitInterfaceSpecifier != null)
                        break;

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
                            // Clean up the suffix for Luau identifier; preserve nullable
                            var isNullableSuffix = suffix.EndsWith("?");
                            suffix = suffix.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                            if (isNullableSuffix) suffix += "_nullable";
                            emitName = $"{name}_{suffix}";
                        }
                    }

                    // Collision detection: if emit name already used, append __N
                    if (emitNamesUsed.Contains(emitName))
                    {
                        int counter = 2;
                        while (emitNamesUsed.Contains($"{emitName}__{counter}"))
                            counter++;
                        emitName = $"{emitName}__{counter}";
                    }
                    emitNamesUsed.Add(emitName);

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
                            // Defer complex initializers (function calls) to end of module
                            // to avoid forward references to methods not yet defined
                            if (value.Contains("("))
                            {
                                AppendLine($"{className}.{fieldName} = nil");
                                _moduleDeferredStatics.Add((className, fieldName, value));
                            }
                            else
                            {
                                AppendLine($"{className}.{fieldName} = {value}");
                            }
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
                case EventFieldDeclarationSyntax:
                    // Static class operators/ctors/events — skip silently
                    break;

                case ConstructorDeclarationSyntax ctorDecl:
                    // Static constructors → defer body to after methods
                    if (ctorDecl.Modifiers.Any(SyntaxKind.StaticKeyword) && ctorDecl.Body != null)
                        _moduleDeferredStaticCtors.Add(ctorDecl);
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

            // Emit module-level static initializers inline (no task.delay wrapper).
            // Deferred imports now use lazy metatable proxies, so they resolve on access.
            if (_moduleDeferredStatics.Count > 0)
            {
                AppendLine();
                foreach (var (cls, fieldName, value) in _moduleDeferredStatics)
                {
                    AppendLine($"{cls}.{fieldName} = {value}");
                }
                _moduleDeferredStatics.Clear();
            }

            // Deferred static constructor bodies
            if (_moduleDeferredStaticCtors.Count > 0)
            {
                _isInstanceContext = false;
                _currentMethodParams = new HashSet<string>();
                _currentMethodLocals = new HashSet<string>();
                _patternVarAliases.Clear();
                foreach (var staticCtor in _moduleDeferredStaticCtors)
                {
                    if (staticCtor.Body != null)
                    {
                        foreach (var statement in staticCtor.Body.Statements)
                            EmitStatement(statement);
                    }
                }
                _isInstanceContext = true;
                _moduleDeferredStaticCtors.Clear();
                AppendLine();
            }

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
            // Skip interfaces — only use concrete/abstract class inheritance
            string? baseClassName = null;
            if (classDecl.BaseList != null)
            {
                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var baseTypeName = baseType.Type.ToString();
                    // Strip namespace qualifiers
                    if (baseTypeName.Contains('.'))
                        baseTypeName = baseTypeName.Substring(baseTypeName.LastIndexOf('.') + 1);
                    // Strip generic args
                    if (baseTypeName.Contains('<'))
                        baseTypeName = baseTypeName.Substring(0, baseTypeName.IndexOf('<'));

                    // Check if this is an interface — skip it for base class inheritance
                    bool isInterface = false;
                    if (_model != null)
                    {
                        var symbolInfo = _model.GetSymbolInfo(baseType.Type);
                        if (symbolInfo.Symbol is INamedTypeSymbol namedSym && namedSym.TypeKind == TypeKind.Interface)
                            isInterface = true;
                        var typeInfo = _model.GetTypeInfo(baseType.Type);
                        if (typeInfo.Type is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
                            isInterface = true;
                    }
                    // Always apply heuristic as fallback (I + uppercase = interface convention)
                    if (!isInterface && baseTypeName.Length > 1 && baseTypeName[0] == 'I' && char.IsUpper(baseTypeName[1]))
                    {
                        isInterface = true;
                    }

                    if (!isInterface && baseTypeName != classDecl.Identifier.Text)
                    {
                        baseClassName = baseTypeName;
                        break;
                    }
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
        // Interfaces are type-only (export type) — no runtime value to return
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
        // For BCL runtime base classes, remap _baseClassName to __rt.X so that
        // `base.Method()` calls and class table __index both use the runtime reference.
        if (baseClassName != null && IsNetFrameworkBaseClass(baseClassName))
        {
            if (baseClassName is "Collection" or "ReadOnlyCollection")
            {
                _baseClassName = "__rt.Collection";
                NeedsRuntime = true;
            }
            else if (baseClassName is "KeyedCollection")
            {
                _baseClassName = "__rt.KeyedCollection";
                NeedsRuntime = true;
            }
            else
            {
                // Other BCL base classes (e.g. DynamicMetaObject, ExpressionVisitor) don't have
                // runtime equivalents. Keep the raw name so base.Method() calls don't generate
                // nil.Method() (which is a Luau parse error). The raw name will be nil at runtime
                // but won't prevent the module from loading.
                _baseClassName = baseClassName;
            }
        }
        else
        {
            _baseClassName = baseClassName;
        }

        // Track the base class as a referenced module (so it gets a require())
        // Skip BCL runtime types — they live in __rt, not as project modules
        if (baseClassName != null && !IsNetFrameworkBaseClass(baseClassName))
        {
            ReferencedModules.Add(baseClassName);
        }

        // ── Phase 1: collect all member info ──

        var fields = new List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)>();
        var properties = new List<PropertyDeclarationSyntax>();
        var constructors = new List<ConstructorDeclarationSyntax>();
        ConstructorDeclarationSyntax? staticConstructor = null;
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

                        // Struct-typed fields with no explicit initializer: generate zero-initialized table
                        // to match C# value-type zero-initialization (prevents "attempt to index nil")
                        if (defaultValue == null && _model != null)
                        {
                            var typeSymbol = _model.GetTypeInfo(fieldDecl.Declaration.Type).Type;
                            if (typeSymbol?.IsValueType == true
                                && typeSymbol.TypeKind != TypeKind.Enum
                                && typeSymbol.SpecialType == SpecialType.None
                                && typeSymbol.OriginalDefinition?.SpecialType != SpecialType.System_Nullable_T)
                            {
                                defaultValue = BuildStructDefault(typeSymbol as INamedTypeSymbol);
                            }
                            // Enum-typed fields default to 0 in C# (first enum value)
                            else if (typeSymbol?.TypeKind == TypeKind.Enum)
                            {
                                defaultValue = "0";
                            }
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
                        bool isAbstract = propDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
                        bool isAutoProperty = !isAbstract
                            && propDecl.AccessorList != null
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
                    if (ctorDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                        staticConstructor = ctorDecl;
                    else
                        constructors.Add(ctorDecl);
                    break;
                case MethodDeclarationSyntax methodDecl:
                    // Skip explicit interface implementation methods — they duplicate the class method
                    if (methodDecl.ExplicitInterfaceSpecifier != null)
                        break;
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

        // Build overload disambiguation map (indexed by method position for uniqueness)
        _overloadMap = new Dictionary<(string Name, int ParamCount, string FirstParamType), string>();
        _methodEmitNames = new List<string>();
        var methodNameCounts = new Dictionary<string, int>();
        var methodNameSeen = new Dictionary<string, int>();
        var emitNamesUsed = new HashSet<string>();
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
                    // Preserve nullable distinction: bool? → bool_nullable
                    var isNullableSuffix = suffix.EndsWith("?");
                    suffix = suffix.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                    if (isNullableSuffix) suffix += "_nullable";
                    emitName = $"{name}_{suffix}";
                }
            }

            // Override methods must use the same emit name as their base class declaration.
            // Check GlobalOverloadMap (already fixed by FixOverrideNames) for the correct name.
            if (method.Modifiers.Any(SyntaxKind.OverrideKeyword) && _currentClassName != null)
            {
                var curFpt = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
                var isNullableOverrideFpt = curFpt.EndsWith("?");
                curFpt = curFpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullableOverrideFpt) curFpt += "_nullable";
                var globalKey = (_currentClassName, name, paramCount, curFpt);
                if (GlobalOverloadMap.TryGetValue(globalKey, out var overrideEmitName))
                {
                    emitName = overrideEmitName;
                }
            }

            // If this emit name collides with an already-used name, append __N
            if (emitNamesUsed.Contains(emitName))
            {
                int counter = 2;
                while (emitNamesUsed.Contains($"{emitName}__{counter}"))
                    counter++;
                emitName = $"{emitName}__{counter}";
            }
            emitNamesUsed.Add(emitName);
            _methodEmitNames.Add(emitName);

            var fpt = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
            // Preserve nullable distinction in the overload key
            var isNullableFpt = fpt.EndsWith("?");
            fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
            if (isNullableFpt) fpt += "_nullable";
            _overloadMap[(name, paramCount, fpt)] = emitName;
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
        // Use _baseClassName (which may be __rt.X for BCL types) for the __index chain
        var classTableBase = _baseClassName ?? baseClassName;
        if (classTableBase != null)
        {
            AppendLine($"local {className} = setmetatable({{}}, {{__index = {classTableBase}}})");
        }
        else
        {
            AppendLine($"local {className} = {{}}");
        }
        AppendLine($"{className}.__index = {className}");
        AppendLine($"{className}.__className = \"{className}\"");

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

        // ── Phase 3.6: static fields (simple initializers only) ──
        // Defer fields with function calls (IIFE, .new(), etc.) to after methods are defined
        var deferredStaticFields = new List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)>();
        foreach (var field in staticFields)
        {
            if (field.DefaultValue != null && field.DefaultValue.Contains("("))
            {
                // Complex initializer — defer to after constructor/methods
                AppendLine($"{className}.{field.Name} = nil");
                deferredStaticFields.Add(field);
            }
            else if (field.DefaultValue != null)
                AppendLine($"{className}.{field.Name} = {field.DefaultValue}");
            else
                AppendLine($"{className}.{field.Name} = nil");
        }

        if (constFields.Count > 0 || staticFields.Count > 0)
            AppendLine();

        // ── Phase 4: constructor(s) ──
        _isInstanceContext = true;
        if (constructors.Count > 0)
        {
            if (constructors.Count == 1)
            {
                EmitConstructor(className, constructors[0], instanceFields, baseClassName);
            }
            else
            {
                // Multiple constructors — emit all with disambiguated names
                var ctorNamesUsed = new HashSet<string> { "new" };
                for (int ci = 0; ci < constructors.Count; ci++)
                {
                    var ctor = constructors[ci];
                    string ctorEmitName = "new";
                    if (ci > 0)
                    {
                        var firstParam = ctor.ParameterList.Parameters.FirstOrDefault();
                        var suffix = firstParam?.Type?.ToString() ?? $"_{ci}";
                        var isNullableSuffix = suffix.EndsWith("?");
                        suffix = suffix.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                        if (isNullableSuffix) suffix += "_nullable";
                        ctorEmitName = $"new_{suffix}";
                        // Avoid name collisions (e.g., multiple ctors with same first param type)
                        if (ctorNamesUsed.Contains(ctorEmitName))
                        {
                            int counter = 2;
                            while (ctorNamesUsed.Contains($"{ctorEmitName}__{counter}"))
                                counter++;
                            ctorEmitName = $"{ctorEmitName}__{counter}";
                        }
                    }
                    ctorNamesUsed.Add(ctorEmitName);
                    EmitConstructor(className, ctor, instanceFields, baseClassName, ctorEmitName);

                    // Register in local overload map for call-site resolution
                    var paramCount = ctor.ParameterList.Parameters.Count;
                    var fpt = ctor.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
                    var isNullableFpt = fpt.EndsWith("?");
                    fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                    if (isNullableFpt) fpt += "_nullable";
                    _overloadMap[("new", paramCount, fpt)] = ctorEmitName;
                }
            }
        }
        else
        {
            // Auto-generate parameterless constructor (always emit so derived classes can call Base.new())
            EmitAutoConstructor(className, instanceFields, baseClassName);
        }

        // ── Phase 5: properties (expression-bodied → getter function) ──
        foreach (var prop in properties)
        {
            EmitProperty(className, prop);
        }

        // ── Phase 6: methods (use pre-computed overload map) ──
        for (int methodIdx = 0; methodIdx < methods.Count; methodIdx++)
        {
            var method = methods[methodIdx];
            var name = method.Identifier.Text;
            string emitName = _methodEmitNames[methodIdx];

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

        // ── Phase 6.55: wire __tostring metamethod if class has ToString() ──
        // Metamethods don't chain through __index in Luau, so we must explicitly set __tostring
        // on every class that has ToString — either directly or inherited from a base with it.
        bool hasOwnToString = methods.Any(m =>
            m.Identifier.Text == "ToString"
            && !m.Modifiers.Any(SyntaxKind.StaticKeyword)
            && m.ParameterList.Parameters.Count == 0);
        bool baseHasToString = false;
        if (!hasOwnToString)
        {
            // Walk the base class chain (may span files) to check for ToString
            var cur = baseClassName;
            while (cur != null)
            {
                if (GlobalTypesWithToString.Contains(cur))
                {
                    baseHasToString = true;
                    break;
                }
                GlobalBaseClassMap.TryGetValue(cur, out cur);
            }
        }
        if (hasOwnToString || baseHasToString)
        {
            // Delegate through the class table so __index resolution finds the right ToString
            AppendLine($"{className}.__tostring = function(self) return {className}.ToString(self) end");
            GlobalTypesWithToString.Add(className);
        }

        // ── Phase 6.6: deferred static field initializers ──
        // Collect into module-level list instead of emitting inline.
        // This ensures all methods (including from outer/sibling classes) are defined
        // before any static initializer runs — fixing nested class forward references.
        foreach (var field in deferredStaticFields)
        {
            _moduleDeferredStatics.Add((className, field.Name, field.DefaultValue!));
        }

        // ── Phase 6.7: static constructor body (after all methods defined) ──
        if (staticConstructor?.Body != null)
        {
            _moduleDeferredStaticCtors.Add(staticConstructor);
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

            // ── Phase 7.5: emit all module-level static initializers inline ──
            // Deferred imports now use lazy metatable proxies, so they resolve on access.
            if (_moduleDeferredStatics.Count > 0)
            {
                AppendLine();
                foreach (var (cls, fieldName, value) in _moduleDeferredStatics)
                {
                    AppendLine($"{cls}.{fieldName} = {value}");
                }
                _moduleDeferredStatics.Clear();
            }

            // ── Phase 7.6: deferred static constructor bodies ──
            if (_moduleDeferredStaticCtors.Count > 0)
            {
                _isInstanceContext = false;
                _currentMethodParams = new HashSet<string>();
                _currentMethodLocals = new HashSet<string>();
                _patternVarAliases.Clear();
                foreach (var staticCtor in _moduleDeferredStaticCtors)
                {
                    if (staticCtor.Body != null)
                    {
                        foreach (var statement in staticCtor.Body.Statements)
                            EmitStatement(statement);
                    }
                }
                _isInstanceContext = true;
                _moduleDeferredStaticCtors.Clear();
                AppendLine();
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
        _overloadMap = new Dictionary<(string Name, int ParamCount, string FirstParamType), string>();
        _methodEmitNames = new List<string>();
    }

    /// <summary>
    /// Emit a constructor as ClassName.new(params): ClassName
    /// When baseClassName is provided, handles base() constructor initializer.
    /// </summary>
    private void EmitConstructor(
        string className,
        ConstructorDeclarationSyntax ctor,
        List<(string Name, string LuauType, string? DefaultValue, bool IsConst, bool IsStatic)> instanceFields,
        string? baseClassName = null,
        string emitName = "new")
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
        AppendLine($"function {className}.{emitName}({paramStr}): {className}");
        _indent++;

        // Handle this() constructor chaining: `: this(args)`
        if (ctor.Initializer != null
            && ctor.Initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
        {
            var thisArgs = ctor.Initializer.ArgumentList.Arguments
                .Select(a => EmitExpression(a.Expression));
            var thisArgStr = string.Join(", ", thisArgs);
            // Find the matching constructor overload name
            var targetArgCount = ctor.Initializer.ArgumentList.Arguments.Count;
            string targetName = "new";
            var firstArgType = "";
            if (targetArgCount > 0)
            {
                var firstArg = ctor.Initializer.ArgumentList.Arguments[0].Expression;
                firstArgType = GetExpressionType(firstArg)?.Name ?? firstArg.ToString();
            }
            // Look up overloaded name from GlobalOverloadMap
            foreach (var key in GlobalOverloadMap.Keys)
            {
                if (key.TypeName == className && key.MethodName == "new" && key.ArgCount == targetArgCount)
                {
                    targetName = GlobalOverloadMap[key];
                    break;
                }
            }
            AppendLine($"local self = {className}.{targetName}({thisArgStr})");
            // Emit the constructor body (additional assignments)
            if (ctor.Body != null)
            {
                foreach (var statement in ctor.Body.Statements)
                    EmitStatement(statement);
            }
            else if (ctor.ExpressionBody != null)
            {
                var expr = EmitExpression(ctor.ExpressionBody.Expression);
                AppendLine(expr);
            }
            AppendLine("return self");
            _indent--;
            AppendLine("end");
            return;
        }

        // Handle base constructor initializer: `: base(args)`
        if (baseClassName != null && ctor.Initializer != null
            && ctor.Initializer.IsKind(SyntaxKind.BaseConstructorInitializer))
        {
            // Call parent constructor to get base fields, then overlay child fields
            var baseArgs = ctor.Initializer.ArgumentList.Arguments
                .Select(a => EmitExpression(a.Expression));
            var baseArgStr = string.Join(", ", baseArgs);
            var baseCall = IsNetFrameworkBaseClass(baseClassName)
                ? (baseClassName is "KeyedCollection" ? "__rt.KeyedCollection.new()" : baseClassName is "Collection" or "ReadOnlyCollection" ? "__rt.Collection.new()" : "{}")
                : $"{baseClassName}.new({baseArgStr})";
            AppendLine($"local self = setmetatable({baseCall} :: any, {className})");

            // Exception-derived: store base constructor args as Message/InnerException
            if (IsExceptionBaseClass(baseClassName))
            {
                var baseArgList = ctor.Initializer.ArgumentList.Arguments;
                if (baseArgList.Count >= 1)
                    AppendLine($"self.Message = {EmitExpression(baseArgList[0].Expression)}");
                if (baseArgList.Count >= 2)
                    AppendLine($"self.InnerException = {EmitExpression(baseArgList[1].Expression)}");
            }

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
            var baseCall = IsNetFrameworkBaseClass(baseClassName)
                ? (baseClassName is "KeyedCollection" ? "__rt.KeyedCollection.new()" : baseClassName is "Collection" or "ReadOnlyCollection" ? "__rt.Collection.new()" : "{}")
                : $"{baseClassName}.new()";
            AppendLine($"local self = setmetatable({baseCall} :: any, {className})");

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

        // For struct constructors with parameters: add nil guard for default construction.
        // In C#, `new StructType()` zero-initializes all fields without calling the parameterized ctor.
        // In our Luau emit, .new() is always called, so we need to return early with defaults when
        // the first param is nil (indicating parameterless/default construction).
        if (parameters.Count > 0 && GlobalStructTypes.Contains(className))
        {
            var firstParam = EscapeIdentifier(parameters[0].Identifier.Text);
            AppendLine($"if {firstParam} == nil then return self end");
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
            var baseCall = IsNetFrameworkBaseClass(baseClassName)
                ? (baseClassName is "KeyedCollection" ? "__rt.KeyedCollection.new()" : baseClassName is "Collection" or "ReadOnlyCollection" ? "__rt.Collection.new()" : "{}")
                : $"{baseClassName}.new()";
            AppendLine($"local self = setmetatable({baseCall} :: any, {className})");

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
        // Nullable types always default to nil (matches C# null semantics)
        if (luauType.EndsWith("?"))
            return "nil :: any";
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
    /// Build a zero-initialized struct default by inspecting the struct's fields.
    /// e.g., JsonPosition → {Type = 0, Position = 0, HasIndex = false, PropertyName = nil}
    /// Falls back to {} if the struct has no inspectable fields.
    /// </summary>
    private string BuildStructDefault(INamedTypeSymbol? structType)
    {
        if (structType == null)
            return "{}";

        // If the struct is defined in the project, check if it has a parameterless constructor.
        // If so, call .new(). If not, build a default table with metatable to avoid calling
        // a parameterized constructor with nil args.
        var structName = structType.Name;
        if (GlobalStructTypes.Contains(structName))
        {
            // Check if there's a parameterless constructor
            bool hasParameterlessCtor = structType.InstanceConstructors
                .Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared);
            if (hasParameterlessCtor)
                return $"{structName}.new()";
            // No parameterless ctor — fall through to build default table with metatable
        }


        var fieldDefaults = new List<string>();
        foreach (var member in structType.GetMembers())
        {
            if (member is IFieldSymbol fs && !fs.IsStatic && !fs.IsConst && fs.AssociatedSymbol == null)
            {
                var val = fs.Type.SpecialType switch
                {
                    SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single
                        or SpecialType.System_Double or SpecialType.System_Byte or SpecialType.System_Int16
                        or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 => "0",
                    SpecialType.System_Boolean => "false",
                    SpecialType.System_String => "nil",
                    _ when fs.Type.TypeKind == TypeKind.Enum => "0",
                    _ => "nil"
                };
                fieldDefaults.Add($"{fs.Name} = {val}");
            }
            // Auto-properties (backing field has associated property symbol; use the property instead)
            else if (member is IPropertySymbol ps && !ps.IsStatic && !ps.IsIndexer
                     && ps.GetMethod != null)
            {
                var val = ps.Type.SpecialType switch
                {
                    SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single
                        or SpecialType.System_Double or SpecialType.System_Byte or SpecialType.System_Int16
                        or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 => "0",
                    SpecialType.System_Boolean => "false",
                    SpecialType.System_String => "nil",
                    _ when ps.Type.TypeKind == TypeKind.Enum => "0",
                    _ => "nil"
                };
                fieldDefaults.Add($"{ps.Name} = {val}");
            }
        }

        var fields = fieldDefaults.Count > 0 ? string.Join(", ", fieldDefaults) : "";
        if (GlobalStructTypes.Contains(structName))
            return $"setmetatable({{ {fields} }}, {structName})";
        if (fieldDefaults.Count == 0)
            return "{}";
        return $"{{ {fields} }}";
    }

    /// <summary>
    /// Emit a property as a getter function (and optionally a setter).
    /// Expression-bodied properties become simple getters.
    /// Pure auto-properties ({ get; set; }) are treated as fields and skipped here.
    /// </summary>
    private void EmitProperty(string className, PropertyDeclarationSyntax prop)
    {
        // Skip explicit interface implementation properties (they duplicate the class property)
        if (prop.ExplicitInterfaceSpecifier != null)
            return;

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
                        // Expression-bodied setter: set => _field = value;
                        // Must emit as assignment statement, not expression (which strips to RHS)
                        if (accessor.ExpressionBody.Expression is AssignmentExpressionSyntax setAssign)
                        {
                            EmitAssignmentStatement(setAssign);
                        }
                        else
                        {
                            var expr = EmitExpression(accessor.ExpressionBody.Expression);
                            AppendLine(expr);
                        }
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
        _currentMethodOutParams = new List<string>();

        // Build parameter list: self first, then declared params
        // Track out parameters for multi-return emulation
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
            // Track out/ref parameters
            if (param.Modifiers.Any(SyntaxKind.OutKeyword) || param.Modifiers.Any(SyntaxKind.RefKeyword))
                _currentMethodOutParams.Add(paramName);
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
        _currentMethodOutParams = new List<string>();
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
        _currentMethodOutParams = new List<string>();

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
            if (param.Modifiers.Any(SyntaxKind.OutKeyword) || param.Modifiers.Any(SyntaxKind.RefKeyword))
                _currentMethodOutParams.Add(paramName);
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
        _currentMethodOutParams = new List<string>();
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
        var rawTypeName = convDecl.Type.ToString();
        var isNullable = rawTypeName.EndsWith("?") || convDecl.ParameterList.Parameters[0].Type?.ToString().EndsWith("?") == true;
        var methodName = $"__{direction}_{rawTypeName.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_")}{(isNullable ? "_nullable" : "")}";

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
        // Skip explicit interface implementation indexers (they duplicate the class indexer)
        if (indexerDecl.ExplicitInterfaceSpecifier != null)
            return;

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
        // Hoist local functions to the top of the block (C# allows them anywhere,
        // but Luau requires local functions to be declared before first use).
        foreach (var statement in block.Statements)
        {
            if (statement is LocalFunctionStatementSyntax localFunc)
                EmitLocalFunction(localFunc);
        }
        foreach (var statement in block.Statements)
        {
            if (statement is LocalFunctionStatementSyntax)
                continue; // already emitted above
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
                // Suppress break inside switch-as-if/elseif, but allow it inside loops within switches
                if (_switchDepth == 0 || _loopDepthInSwitch > 0)
                {
                    if (_insidePcallLambda)
                    {
                        _pcallHasBreak = true;
                        AppendLine("__pcall_break = true");
                        AppendLine("return");
                    }
                    else
                    {
                        AppendLine("break");
                    }
                }
                break;
            case ContinueStatementSyntax:
                if (_insidePcallLambda)
                {
                    _pcallHasContinue = true;
                    AppendLine("__pcall_continue = true");
                    AppendLine("return");
                }
                else
                {
                    // Emit for-loop incrementors before continue so C# semantics are preserved
                    // (C# for-loop continue runs the incrementor before re-checking condition)
                    EmitForLoopIncrementorsBeforeContinue();
                    AppendLine("continue");
                }
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
        var outSuffix = _currentMethodOutParams.Count > 0
            ? ", " + string.Join(", ", _currentMethodOutParams)
            : "";

        if (ret.Expression == null)
        {
            if (_currentMethodOutParams.Count > 0)
                AppendLine($"return nil{outSuffix}");
            else
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
            AppendLine($"return {expr}{outSuffix}");
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
        // Check if condition contains out-param invocations that need hoisting.
        // Hoisting emits local declarations before the expression, which would break
        // the elseif chain. Convert to else + nested if to allow the declarations.
        if (ConditionHasOutParams(ifStmt.Condition))
        {
            AppendLine("else");
            _indent++;
            EmitIf(ifStmt);
            _indent--;
            AppendLine("end");
            return;
        }

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
    /// Check if an expression contains any invocations with out arguments,
    /// which would require hoisting that is incompatible with elseif syntax.
    /// </summary>
    private bool ConditionHasOutParams(ExpressionSyntax condition)
    {
        foreach (var node in condition.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                foreach (var arg in invocation.ArgumentList.Arguments)
                {
                    if (arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
                        return true;
                }
            }
        }
        return false;
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
        _pendingAssignmentStatements.Clear();
        var condition = EmitExpression(whileStmt.Condition);
        var hoisted = new List<string>(_pendingAssignmentStatements);
        _pendingAssignmentStatements.Clear();

        // Emit hoisted assignments before the while (first iteration)
        foreach (var stmt in hoisted)
            AppendLine(stmt);

        AppendLine($"while {condition} do");
        _indent++;
        _loopDepth++;
        if (_switchDepth > 0) _loopDepthInSwitch++;
        EmitStatementBody(whileStmt.Statement);
        // Re-emit hoisted assignments at end of loop body (subsequent iterations)
        foreach (var stmt in hoisted)
            AppendLine(stmt);
        if (_switchDepth > 0) _loopDepthInSwitch--;
        _loopDepth--;
        _indent--;
        AppendLine("end");
    }

    private void EmitDoWhile(DoStatementSyntax doStmt)
    {
        _pendingAssignmentStatements.Clear();
        var condition = EmitExpression(doStmt.Condition);
        var hoisted = new List<string>(_pendingAssignmentStatements);
        _pendingAssignmentStatements.Clear();

        AppendLine("repeat");
        _indent++;
        _loopDepth++;
        if (_switchDepth > 0) _loopDepthInSwitch++;
        EmitStatementBody(doStmt.Statement);
        // Emit hoisted assignments before the condition check at end of loop
        foreach (var stmt in hoisted)
            AppendLine(stmt);
        if (_switchDepth > 0) _loopDepthInSwitch--;
        _loopDepth--;
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

        // Check if the try block contains continue/break that would cross the pcall boundary.
        // Only count continue/break that are NOT inside a nested loop within the try block.
        bool tryHasContinue = tryStmt.Block.DescendantNodes().OfType<ContinueStatementSyntax>()
            .Any(c => !c.Ancestors().TakeWhile(a => a != tryStmt.Block)
                .Any(a => a is WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or DoStatementSyntax));
        bool tryHasBreak = tryStmt.Block.DescendantNodes().OfType<BreakStatementSyntax>()
            .Any(b => !b.Ancestors().TakeWhile(a => a != tryStmt.Block)
                .Any(a => a is WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or DoStatementSyntax));

        // Only emit break/continue flags when the try-catch is inside a loop
        // (break/continue outside loops is invalid Luau)
        if (_loopDepth == 0)
        {
            tryHasContinue = false;
            tryHasBreak = false;
        }

        // Emit flag declarations before pcall if needed
        if (tryHasContinue)
            AppendLine("local __pcall_continue = false");
        if (tryHasBreak)
            AppendLine("local __pcall_break = false");

        // Emit pcall wrapper for the try block
        // Save/restore loop depth: Luau function boundaries hide loops,
        // so break/continue inside pcall lambda are not "inside a loop"
        bool savedInsidePcall = _insidePcallLambda;
        int savedLoopDepth = _loopDepth;
        _insidePcallLambda = true;
        _loopDepth = 0;
        _pcallHasContinue = false;
        _pcallHasBreak = false;
        AppendLine("local __ok, __pcall_ret = pcall(function()");
        _indent++;
        EmitBlock(tryStmt.Block);
        _indent--;
        AppendLine("end)");
        _insidePcallLambda = savedInsidePcall;
        _loopDepth = savedLoopDepth;

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

        // Emit continue/break flag checks after pcall completes
        // These handle C# continue/break inside try blocks (which become pcall lambdas in Luau)
        if (tryHasContinue)
        {
            // Emit for-loop incrementors before continue so C# semantics are preserved
            if (_forLoopIncrementors != null)
            {
                AppendLine("if __pcall_continue then");
                _indent++;
                EmitForLoopIncrementorsBeforeContinue();
                AppendLine("continue");
                _indent--;
                AppendLine("end");
            }
            else
            {
                AppendLine("if __pcall_continue then continue end");
            }
        }
        if (tryHasBreak)
        {
            AppendLine("if __pcall_break then break end");
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
                var alreadyDeclared = _currentMethodLocals.Contains(rawName) || _currentMethodLocals.Contains(name);
                _currentMethodLocals.Add(rawName);
                if (name != rawName) _currentMethodLocals.Add(name);
                var prefix = alreadyDeclared ? "" : "local ";
                if (variable.Initializer != null)
                {
                    var init = EmitExpression(variable.Initializer.Value);
                    AppendLine($"{prefix}{name} = {init}");
                }
                else if (!alreadyDeclared)
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
        _loopDepth++;
        if (_switchDepth > 0) _loopDepthInSwitch++;

        // Track incrementors so continue statements can emit them first
        // (C# for-loop continue executes the incrementor before re-checking condition)
        var previousIncrementors = _forLoopIncrementors;
        _forLoopIncrementors = forStmt.Incrementors.Count > 0 ? forStmt.Incrementors : null;

        EmitStatementBody(forStmt.Statement);

        // Emit incrementors at end of loop body (normal path)
        foreach (var incrementor in forStmt.Incrementors)
        {
            EmitIncrementorStatement(incrementor);
        }

        _forLoopIncrementors = previousIncrementors;
        if (_switchDepth > 0) _loopDepthInSwitch--;
        _loopDepth--;
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

        // Handle compound assignments: i += step, i -= step, etc.
        else if (expr is AssignmentExpressionSyntax assignment)
        {
            EmitAssignmentStatement(assignment);
            return;
        }

        // Fallback: emit as expression statement
        var exprStr = EmitExpression(expr);
        AppendLine(exprStr);
    }

    /// <summary>
    /// Emit the current for-loop incrementors before a continue statement.
    /// In C#, continue in a for loop still executes the incrementor before
    /// re-checking the condition. In Luau's while loop, continue skips to
    /// the condition directly, so we must emit the incrementor explicitly.
    /// </summary>
    private void EmitForLoopIncrementorsBeforeContinue()
    {
        if (_forLoopIncrementors == null) return;
        foreach (var inc in _forLoopIncrementors)
        {
            EmitIncrementorStatement(inc);
        }
    }

    private void EmitForEach(ForEachStatementSyntax foreachStmt)
    {
        var varName = EscapeIdentifier(foreachStmt.Identifier.Text);
        var collection = EmitExpression(foreachStmt.Expression);
        _currentMethodLocals.Add(foreachStmt.Identifier.Text);
        if (varName != foreachStmt.Identifier.Text) _currentMethodLocals.Add(varName);

        AppendLine($"for _, {varName} in {collection} do");
        _indent++;
        _loopDepth++;
        if (_switchDepth > 0) _loopDepthInSwitch++;
        EmitStatementBody(foreachStmt.Statement);
        if (_switchDepth > 0) _loopDepthInSwitch--;
        _loopDepth--;
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
        _pendingAssignmentStatements.Clear();
        var governing = EmitExpression(switchStmt.Expression);
        // If the governing expression produced pending side-effects (e.g., charPos++ in switch(_chars[charPos++])),
        // we must capture the governing value in a temp before draining them, because the drain modifies
        // variables that appear in the governing expression string.
        if (_pendingAssignmentStatements.Count > 0)
        {
            var tmpName = $"__switchVal_{_tempVarCounter++}";
            AppendLine($"local {tmpName} = {governing}");
            foreach (var stmt in _pendingAssignmentStatements)
                AppendLine(stmt);
            _pendingAssignmentStatements.Clear();
            governing = tmpName;
        }

        // Gather sections. Each section can have multiple labels and multiple statements.
        // We convert to if/elseif chains. Track switch depth to suppress break statements.
        _switchDepth++;
        bool isFirst = true;
        SwitchSectionSyntax? defaultSection = null;

        foreach (var section in switchStmt.Sections)
        {
            var caseLabels = section.Labels.OfType<CaseSwitchLabelSyntax>().ToList();
            var patternLabels = section.Labels.OfType<CasePatternSwitchLabelSyntax>().ToList();
            var hasDefault = section.Labels.Any(l => l is DefaultSwitchLabelSyntax);

            if (caseLabels.Count == 0 && patternLabels.Count == 0 && hasDefault)
            {
                defaultSection = section;
                continue;
            }

            // Build condition from value labels: governing == val1 or governing == val2 ...
            var condParts = new List<string>();
            foreach (var c in caseLabels)
                condParts.Add($"{governing} == {EmitExpression(c.Value)}");

            // Build condition from pattern labels: case Type varName: / case var x when ...:
            foreach (var pl in patternLabels)
            {
                var patternCond = EmitPattern(governing, pl.Pattern);
                if (pl.WhenClause != null)
                    patternCond = $"({patternCond} and {EmitExpression(pl.WhenClause.Condition)})";
                condParts.Add(patternCond);
            }

            var condStr = string.Join(" or ", condParts);
            if (condStr.Length == 0)
                condStr = "true"; // fallback — should not happen

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
        _switchDepth--;
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

        // Conditional access as statement: obj?.Method() → if obj ~= nil then obj:Method() end
        if (exprStmt.Expression is ConditionalAccessExpressionSyntax conditionalAccess)
        {
            var target = EmitExpression(conditionalAccess.Expression);
            var binding = EmitConditionalBinding(target, conditionalAccess.WhenNotNull);
            AppendLine($"if {target} ~= nil then {binding} end");
            return;
        }

        // await <expr> as statement: if emitted form is not a call, comment it out
        if (exprStmt.Expression is AwaitExpressionSyntax awaitStmt)
        {
            var awaitedExpr = EmitExpression(awaitStmt.Expression);
            // Ternary as statement → convert to if/else statement
            if (awaitedExpr.StartsWith("(if ") || awaitedExpr.StartsWith("((if "))
            {
                // Strip exactly one layer of wrapping parens at a time
                var inner = awaitedExpr;
                while (inner.StartsWith("(") && inner.EndsWith(")"))
                {
                    var candidate = inner.Substring(1, inner.Length - 2);
                    // Verify the stripped parens were a matching pair (balanced inside)
                    int depth = 0;
                    bool balanced = true;
                    foreach (var ch in candidate)
                    {
                        if (ch == '(') depth++;
                        else if (ch == ')') { depth--; if (depth < 0) { balanced = false; break; } }
                    }
                    if (!balanced || depth != 0) break;
                    inner = candidate;
                    if (inner.StartsWith("if ")) break; // found the if
                }
                AppendLine($"{inner} end");
            }
            // If it's a function call it's a valid statement; if it's just a variable, comment it out
            else if (awaitedExpr.Contains("("))
                AppendLine(awaitedExpr);
            else
                AppendLine($"-- await {awaitedExpr}");
            return;
        }

        var expr = EmitExpression(exprStmt.Expression);
        // Only invocations are valid as bare statements in Luau.
        // Everything else (bare identifiers, ternary expressions, etc.) would be incomplete statements.
        if (exprStmt.Expression is InvocationExpressionSyntax)
        {
            // Unwrap IIFE patterns like (function() sb = sb .. x; return sb end)()
            // These cause "ambiguous syntax" in Luau when preceded by another statement.
            // Extract the body and emit as direct statements instead.
            if (expr.StartsWith("(function() ") && expr.EndsWith(" end)()"))
            {
                var body = expr.Substring("(function() ".Length, expr.Length - "(function() ".Length - " end)()".Length);
                // Split on '; ' to get individual statements, skip 'return ...' parts
                foreach (var stmt in body.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = stmt.Trim();
                    if (!trimmed.StartsWith("return "))
                        AppendLine(trimmed);
                }
            }
            else
            {
                // Suppress bare hoisted variables from TryHoistOutParams — already emitted
                if (!expr.StartsWith("__tryOk_"))
                    AppendLine(expr);
            }
        }
        else
        {
            AppendLine($"-- {expr}");
        }
    }

    /// <summary>
    /// Emit an assignment expression as a statement.
    /// </summary>
    private void EmitAssignmentStatement(AssignmentExpressionSyntax assignment)
    {
        // Property write via bare identifier: Prop = value → self:set_Prop(value) or self.Prop = value
        if (_model != null && assignment.Left is IdentifierNameSyntax identLeft
            && assignment.Kind() == SyntaxKind.SimpleAssignmentExpression
            && _isInstanceContext
            && !_currentMethodParams.Contains(identLeft.Identifier.Text)
            && !_currentMethodLocals.Contains(identLeft.Identifier.Text))
        {
            var sym = GetSymbol(identLeft);
            // Get-only property assigned in constructor → direct field write (self.Prop = value)
            if (sym is IPropertySymbol getOnlyPs && getOnlyPs.SetMethod == null && getOnlyPs.GetMethod != null
                && _instanceFields.Contains(identLeft.Identifier.Text))
            {
                var value = EmitExpression(assignment.Right);
                AppendLine($"self.{identLeft.Identifier.Text} = {value}");
                return;
            }
            if (sym is IPropertySymbol ips && ips.SetMethod != null)
            {
                bool needsSetter = ips.IsAbstract;
                if (!needsSetter)
                {
                    var dr = ips.DeclaringSyntaxReferences.FirstOrDefault();
                    if (dr?.GetSyntax() is PropertyDeclarationSyntax pds)
                    {
                        bool hasBody = pds.ExpressionBody != null
                            || (pds.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                        if (hasBody) needsSetter = true;
                    }
                }
                if (needsSetter)
                {
                    var value = EmitExpression(assignment.Right);
                    var propName = identLeft.Identifier.Text;
                    if (ips.IsStatic)
                    {
                        var ct = ips.ContainingType?.Name;
                        if (ct != null) { AppendLine($"{ct}.set_{propName}({value})"); return; }
                    }
                    else
                    {
                        AppendLine($"self:set_{propName}({value})");
                        return;
                    }
                }
            }
        }

        // Property write → setter call: obj.Prop = value → Type.set_Prop(obj, value)
        if (_model != null && assignment.Left is MemberAccessExpressionSyntax propAccess
            && assignment.Kind() == SyntaxKind.SimpleAssignmentExpression)
        {
            var propSymbol = GetSymbol(propAccess);
            if (propSymbol is IPropertySymbol ps && ps.SetMethod != null)
            {
                bool isAuto = !ps.IsAbstract;
                if (isAuto)
                {
                    var declRef = ps.DeclaringSyntaxReferences.FirstOrDefault();
                    if (declRef?.GetSyntax() is PropertyDeclarationSyntax propSyntax)
                    {
                        bool hasExprBody = propSyntax.ExpressionBody != null;
                        bool hasAccessorBody = propSyntax.AccessorList?.Accessors
                            .Any(a => a.Body != null || a.ExpressionBody != null) ?? false;
                        if (hasExprBody || hasAccessorBody)
                            isAuto = false;
                    }
                }
                if (!isAuto)
                {
                    var receiver = EmitExpression(propAccess.Expression);
                    var value = EmitExpression(assignment.Right);
                    var propName = propAccess.Name.Identifier.Text;
                    if (ps.IsStatic)
                    {
                        var containingType = ps.ContainingType?.Name;
                        if (containingType != null)
                        {
                            AppendLine($"{containingType}.set_{propName}({value})");
                            return;
                        }
                    }
                    else
                    {
                        AppendLine($"{receiver}:set_{propName}({value})");
                        return;
                    }
                }
            }
        }

        // IList<T> interface/implementation indexer assignment: children[index] = value → __rt.ilistSet(children, index, value)
        if (_model != null && assignment.Left is ElementAccessExpressionSyntax ilistElem
            && assignment.Kind() == SyntaxKind.SimpleAssignmentExpression)
        {
            var elemReceiverType = GetExpressionType(ilistElem.Expression);
            bool isIListAssign = false;
            if (elemReceiverType != null && ilistElem.ArgumentList.Arguments.Count == 1)
            {
                isIListAssign = (elemReceiverType.TypeKind == TypeKind.Interface
                    && elemReceiverType.Name is "IList" or "IReadOnlyList")
                    || (elemReceiverType.Name is not "List" and not "Array" and not "Queue" and not "Stack"
                        && elemReceiverType is not IArrayTypeSymbol
                        && elemReceiverType.AllInterfaces.Any(i => i.Name == "IList"));
            }
            if (isIListAssign)
            {
                NeedsRuntime = true;
                var obj = EmitExpression(ilistElem.Expression);
                var idx = EmitExpression(ilistElem.ArgumentList.Arguments[0].Expression);
                var val = EmitExpression(assignment.Right);
                AppendLine($"__rt.ilistSet({obj}, {idx}, {val})");
                return;
            }
        }

        var left = EmitExpression(assignment.Left);
        var right = EmitExpression(assignment.Right);

        // #var (from .Length/.Count) can't be an assignment target in Luau → use .Length property
        if (left.StartsWith("#"))
        {
            var varName = left.Substring(1);
            AppendLine($"{varName}.Length = {right}");
            return;
        }

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
            var alreadyDeclared = _currentMethodLocals.Contains(rawName) || _currentMethodLocals.Contains(name);
            _currentMethodLocals.Add(rawName);
            if (name != rawName) _currentMethodLocals.Add(name);
            var prefix = alreadyDeclared ? "" : "local ";
            if (declarator.Initializer != null)
            {
                var init = EmitExpression(declarator.Initializer.Value);
                AppendLine($"{prefix}{name} = {init}");
            }
            else if (!alreadyDeclared)
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
            TypeOfExpressionSyntax typeOf => EmitTypeOf(typeOf),
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
            // Escape for Lua string, handling all non-printable and non-ASCII chars
            var sb = new System.Text.StringBuilder(strVal.Length + 8);
            foreach (var ch in strVal)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (ch < ' ' || ch > '~')
                        {
                            // Non-printable or non-ASCII → Luau \u{XXXX} escape
                            sb.Append($"\\u{{{(int)ch:X4}}}");
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return $"\"{sb}\"";
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
        // But only when the variable doesn't have its own local declaration (EmitPatternVariableBindings
        // creates a local for `is` pattern vars, so they can be reassigned independently)
        if (_patternVarAliases.TryGetValue(name, out var alias) && !_currentMethodLocals.Contains(name)
            && !_currentMethodLocals.Contains(ident.Identifier.Text))
            return alias;

        // Check for well-known replacements
        if (name == "string") return "string";

        // In instance context, bare identifiers that are instance fields → self.field
        // But non-auto properties need self:get_Name() instead of self.Name
        if (_isInstanceContext && _instanceFields.Contains(name)
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name))
        {
            // Check if this is a non-auto property that needs a getter call
            if (_model != null)
            {
                var fieldSym = GetSymbol(ident);
                if (fieldSym is IPropertySymbol fps && !fps.IsStatic && fps.GetMethod != null)
                {
                    if (fps.IsAbstract || fps.IsVirtual || fps.IsOverride)
                        return $"self:get_{name}()";
                    var fpsDeclRef = fps.DeclaringSyntaxReferences.FirstOrDefault();
                    if (fpsDeclRef?.GetSyntax() is PropertyDeclarationSyntax fpsPropSyn)
                    {
                        bool fpsHasBody = fpsPropSyn.ExpressionBody != null
                            || (fpsPropSyn.AccessorList?.Accessors
                                .Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                        if (fpsHasBody)
                            return $"self:get_{name}()";
                    }
                }
            }
            return $"self.{name}";
        }

        // Const/static fields accessed as bare identifiers → ClassName.Field
        if (_constFields.Contains(name)
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name)
            && _currentClassName != null)
        {
            return $"{_currentClassName}.{name}";
        }

        // SemanticModel: resolve bare identifiers to qualified names
        if (_model != null
            && !_currentMethodParams.Contains(name)
            && !_currentMethodLocals.Contains(name))
        {
            var symbol = GetSymbol(ident);
            if (_isInstanceContext)
            {
                if (symbol is IFieldSymbol fs && !fs.IsStatic && !fs.IsConst)
                    return $"self.{name}";
                if (symbol is IPropertySymbol ps && !ps.IsStatic)
                {
                    // Abstract/non-auto properties need getter call, not direct field access
                    if (ps.IsAbstract)
                        return $"self:get_{name}()";
                    if (ps.GetMethod != null)
                    {
                        var declRef = ps.DeclaringSyntaxReferences.FirstOrDefault();
                        if (declRef?.GetSyntax() is PropertyDeclarationSyntax propSyn)
                        {
                            bool hasBody = propSyn.ExpressionBody != null
                                || (propSyn.AccessorList?.Accessors
                                    .Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                            if (hasBody)
                                return $"self:get_{name}()";
                        }
                    }
                    return $"self.{name}";
                }
                if (symbol is IMethodSymbol ms && !ms.IsStatic && ms.MethodKind == MethodKind.Ordinary)
                {
                    // If used as a value (delegate/method group), wrap in closure to capture self
                    if (ident.Parent is not InvocationExpressionSyntax)
                    {
                        var className = ms.ContainingType?.Name ?? _currentClassName;
                        return $"function(...) return {className}.{name}(self, ...) end";
                    }
                    return $"self.{name}";
                }
            }
            // Static methods used as bare identifiers (method group → delegate) → ClassName.Method
            if (symbol is IMethodSymbol staticMs && staticMs.IsStatic
                && staticMs.MethodKind == MethodKind.Ordinary)
            {
                var containingName = staticMs.ContainingType?.Name;
                if (containingName != null)
                    return $"{containingName}.{name}";
            }
            // Static/const fields accessed as bare identifiers → ClassName.Field
            if (symbol is IFieldSymbol sfs && (sfs.IsStatic || sfs.IsConst))
            {
                var containingName = sfs.ContainingType?.Name;
                if (containingName != null)
                    return $"{containingName}.{name}";
            }
        }

        return name;
    }

    private string EmitMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var memberName = memberAccess.Name.Identifier.Text;

        // Handle `base.field` → `self.field`, `base.Property` → `self:get_Property()`
        if (memberAccess.Expression is BaseExpressionSyntax)
        {
            if (_model != null)
            {
                var baseSym = GetSymbol(memberAccess);
                if (baseSym is IPropertySymbol basePs && !basePs.IsStatic && basePs.GetMethod != null)
                {
                    if (basePs.IsAbstract || basePs.IsVirtual || basePs.IsOverride)
                        return $"self:get_{memberName}()";
                    var baseDeclRef = basePs.DeclaringSyntaxReferences.FirstOrDefault();
                    if (baseDeclRef?.GetSyntax() is PropertyDeclarationSyntax basePropSyn)
                    {
                        bool hasBody = basePropSyn.ExpressionBody != null
                            || (basePropSyn.AccessorList?.Accessors
                                .Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                        if (hasBody)
                            return $"self:get_{memberName}()";
                    }
                }
            }
            return $"self.{memberName}";
        }

        // Handle `this.field` → `self.field`, `this.Property` → `self:get_Property()`
        if (memberAccess.Expression is ThisExpressionSyntax)
        {
            // Check if it's a non-auto property requiring getter call
            if (_model != null)
            {
                var thisSym = GetSymbol(memberAccess);
                if (thisSym is IPropertySymbol thisPs && !thisPs.IsStatic && thisPs.GetMethod != null)
                {
                    if (thisPs.IsAbstract || thisPs.IsVirtual || thisPs.IsOverride)
                        return $"self:get_{memberName}()";
                    var thisDeclRef = thisPs.DeclaringSyntaxReferences.FirstOrDefault();
                    if (thisDeclRef?.GetSyntax() is PropertyDeclarationSyntax thisPropSyn)
                    {
                        bool hasBody = thisPropSyn.ExpressionBody != null
                            || (thisPropSyn.AccessorList?.Accessors
                                .Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                        if (hasBody)
                            return $"self:get_{memberName}()";
                    }
                }
            }
            return $"self.{memberName}";
        }

        // typeof(x).Name/FullName → typeof(x), typeof(x).IsSubclassOf(...) → true
        // Handles both .GetType().Name and typeof(T).Name patterns
        if (memberName is "Name" or "FullName" or "Namespace" or "IsValueType"
            or "IsClass" or "IsInterface" or "IsAbstract" or "IsByRef" or "IsEnum"
            or "BaseType" or "IsGenericType" or "IsArray" or "IsPrimitive")
        {
            // Check if expression is .GetType() invocation
            if (memberAccess.Expression is InvocationExpressionSyntax getTypeCall
                && getTypeCall.Expression is MemberAccessExpressionSyntax getTypeAccess
                && getTypeAccess.Name.Identifier.Text == "GetType"
                && getTypeCall.ArgumentList.Arguments.Count == 0)
            {
                var target = EmitExpression(getTypeAccess.Expression);
                if (memberName is "Name" or "FullName")
                    return $"typeof({target})";
                // Boolean properties — best-effort defaults
                if (memberName is "IsValueType" or "IsPrimitive")
                    return "false";
                if (memberName is "IsClass")
                    return "true";
                return $"typeof({target})";
            }
            // Check if expression is typeof(T) — emitted as "T" string
            if (memberAccess.Expression is TypeOfExpressionSyntax)
            {
                var typeStr2 = EmitExpression(memberAccess.Expression);
                if (memberName is "Name" or "FullName")
                    return typeStr2; // Already a string
                if (memberName is "IsValueType" or "IsPrimitive")
                    return "false";
                if (memberName is "IsClass")
                    return "true";
                return typeStr2;
            }
            // Fallback: Type reflection properties on arbitrary expressions (e.g., field.IsEnum)
            // Only apply when the expression is actually a System.Type value
            bool isTypeExpr = false;
            if (_model != null)
            {
                var exprType = _model.GetTypeInfo(memberAccess.Expression).Type;
                if (exprType != null)
                {
                    var typeName = exprType.ToDisplayString();
                    isTypeExpr = typeName is "System.Type" or "System.Reflection.MemberInfo"
                        || typeName.StartsWith("System.Reflection.")
                        || typeName.EndsWith("Type");
                }
            }
            else
            {
                // No semantic model — check syntactically if it looks like a Type expression
                isTypeExpr = memberAccess.Expression is IdentifierNameSyntax id
                    && (id.Identifier.Text.EndsWith("Type") || id.Identifier.Text == "type");
            }
            if (isTypeExpr)
            {
                var left0 = EmitExpression(memberAccess.Expression);
                if (memberName == "IsEnum")
                {
                    NeedsRuntime = true;
                    return $"__rt.Type_IsEnum({left0})";
                }
                if (memberName is "IsByRef" or "HasElementType")
                    return "false";
                if (memberName is "IsValueType" or "IsPrimitive")
                    return "false";
                if (memberName is "IsClass")
                    return "true";
                if (memberName is "IsInterface" or "IsAbstract" or "IsGenericType" or "IsArray")
                    return "false";
                if (memberName is "Name" or "FullName")
                    return left0;
                if (memberName is "BaseType" or "Namespace")
                    return "nil";
            }
        }
        // typeof(x).IsSubclassOf(T) / IsAssignableFrom(T) → true (can't check at runtime)
        // Handled in EmitInvocation for method calls on typeof() results

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
        // Also handle generic type names: CachedAttributeGetter<T>.Method → CachedAttributeGetter.Method
        string? _extractedTypeStr = memberAccess.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gen => gen.Identifier.Text,
            _ => null
        };
        if (_extractedTypeStr != null)
        {
            var typeStr = _extractedTypeStr;

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

            // Environment.NewLine → "\n"
            if (typeStr == "Environment" && memberName == "NewLine")
                return "\"\\n\"";

            // DBNull.Value → nil (database null singleton)
            if (typeStr == "DBNull" && memberName == "Value")
                return "nil";

            // Environment.TickCount → math.floor(os.clock() * 1000)
            if (typeStr == "Environment" && memberName == "TickCount")
                return "math.floor(os.clock() * 1000)";

            // Type.EmptyTypes → {} (System.Type.EmptyTypes is an empty Type[] array)
            if (typeStr == "Type" && memberName == "EmptyTypes")
                return "{}";

            // Math.PI → math.pi, Math.E → math.exp(1), Math.Tau → 2*math.pi
            if (typeStr == "Math")
            {
                if (memberName == "PI") return "math.pi";
                if (memberName == "E") return "2.718281828459045";
                if (memberName == "Tau") return "(2 * math.pi)";
            }

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

            // .NET BCL enum constants → integer values
            if (typeStr == "StringComparison")
            {
                var result = memberName switch
                {
                    "CurrentCulture" => "0",
                    "CurrentCultureIgnoreCase" => "1",
                    "InvariantCulture" => "2",
                    "InvariantCultureIgnoreCase" => "3",
                    "Ordinal" => "4",
                    "OrdinalIgnoreCase" => "5",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "TypeCode")
            {
                var result = memberName switch
                {
                    "Empty" => "0",
                    "Object" => "1",
                    "DBNull" => "2",
                    "Boolean" => "3",
                    "Char" => "4",
                    "SByte" => "5",
                    "Byte" => "6",
                    "Int16" => "7",
                    "UInt16" => "8",
                    "Int32" => "9",
                    "UInt32" => "10",
                    "Int64" => "11",
                    "UInt64" => "12",
                    "Single" => "13",
                    "Double" => "14",
                    "Decimal" => "15",
                    "DateTime" => "16",
                    "String" => "18",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "DateTimeKind")
            {
                var result = memberName switch
                {
                    "Unspecified" => "0",
                    "Utc" => "1",
                    "Local" => "2",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "MemberTypes")
            {
                var result = memberName switch
                {
                    "Constructor" => "1",
                    "Event" => "2",
                    "Field" => "4",
                    "Method" => "8",
                    "Property" => "16",
                    "TypeInfo" => "32",
                    "Custom" => "64",
                    "NestedType" => "128",
                    "All" => "191",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "BindingFlags")
            {
                var result = memberName switch
                {
                    "Default" => "0",
                    "IgnoreCase" => "1",
                    "DeclaredOnly" => "2",
                    "Instance" => "4",
                    "Static" => "8",
                    "Public" => "16",
                    "NonPublic" => "32",
                    "FlattenHierarchy" => "64",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "DateTimeStyles")
            {
                var result = memberName switch
                {
                    "None" => "0",
                    "AllowLeadingWhite" => "1",
                    "AllowTrailingWhite" => "2",
                    "AllowInnerWhite" => "4",
                    "AllowWhiteSpaces" => "7",
                    "NoCurrentDateDefault" => "8",
                    "AdjustToUniversal" => "16",
                    "AssumeLocal" => "32",
                    "AssumeUniversal" => "64",
                    "RoundtripKind" => "128",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "RegexOptions")
            {
                var result = memberName switch
                {
                    "None" => "0",
                    "IgnoreCase" => "1",
                    "Multiline" => "2",
                    "ExplicitCapture" => "4",
                    "Compiled" => "8",
                    "Singleline" => "16",
                    "IgnorePatternWhitespace" => "32",
                    "RightToLeft" => "64",
                    "CultureInvariant" => "512",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            if (typeStr == "CultureInfo" && memberName is "InvariantCulture" or "CurrentCulture" or "CurrentUICulture")
                return "nil";
            if (typeStr == "TraceLevel")
            {
                var result = memberName switch
                {
                    "Off" => "0",
                    "Error" => "1",
                    "Warning" => "2",
                    "Info" => "3",
                    "Verbose" => "4",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            // System.Diagnostics.TraceEventType enum → integer constants
            if (typeStr == "TraceEventType")
            {
                var result = memberName switch
                {
                    "Critical" => "1",
                    "Error" => "2",
                    "Warning" => "4",
                    "Information" => "8",
                    "Verbose" => "16",
                    "Start" => "256",
                    "Stop" => "512",
                    "Suspend" => "1024",
                    "Resume" => "2048",
                    "Transfer" => "4096",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            // System.Linq.Expressions.ExpressionType enum → integer constants
            if (typeStr == "ExpressionType")
            {
                var result = memberName switch
                {
                    "Add" => "0",
                    "AddAssign" => "63",
                    "And" => "2",
                    "Call" => "6",
                    "Conditional" => "8",
                    "Constant" => "9",
                    "Convert" => "10",
                    "Divide" => "12",
                    "DivideAssign" => "65",
                    "Equal" => "13",
                    "GreaterThan" => "15",
                    "GreaterThanOrEqual" => "16",
                    "Lambda" => "18",
                    "LessThan" => "20",
                    "LessThanOrEqual" => "21",
                    "Multiply" => "26",
                    "MultiplyAssign" => "69",
                    "New" => "31",
                    "NotEqual" => "35",
                    "Parameter" => "38",
                    "Subtract" => "42",
                    "SubtractAssign" => "73",
                    "Throw" => "60",
                    "Assign" => "46",
                    "Block" => "47",
                    _ => (string?)null,
                };
                if (result != null) return result;
            }
            // BigInteger static members
            if (typeStr == "BigInteger")
            {
                if (memberName == "Zero") return "0";
                if (memberName == "One") return "1";
                if (memberName == "MinusOne") return "-1";
            }
            // EqualityComparer.Default → nil (Luau uses == for equality)
            if (typeStr == "EqualityComparer" && memberName == "Default")
                return "nil";
            if (typeStr == "StringComparer")
            {
                // StringComparer.Ordinal/OrdinalIgnoreCase → nil (default string comparison in Luau)
                if (memberName is "Ordinal" or "OrdinalIgnoreCase" or "InvariantCulture"
                    or "InvariantCultureIgnoreCase" or "CurrentCulture" or "CurrentCultureIgnoreCase")
                    return "nil";
            }
            // EventDescriptorCollection.Empty → {}
            if (typeStr == "EventDescriptorCollection" && memberName == "Empty")
                return "{}";
            // PropertyDescriptorCollection.Empty → {}
            if (typeStr == "PropertyDescriptorCollection" && memberName == "Empty")
                return "{}";

            // .Length on a string variable → string.len(var) or #var
            // Only use # for types where it works (string, array, List, etc.)
            // Custom structs/classes with a .Length property must fall through to getter logic.
            if (memberName == "Length")
            {
                bool isLengthSafeForHash = true;
                if (_model != null)
                {
                    var lengthOwner = GetExpressionType(memberAccess.Expression);
                    if (lengthOwner != null
                        && lengthOwner.SpecialType != SpecialType.System_String
                        && lengthOwner is not IArrayTypeSymbol
                        && lengthOwner.Name is not "List" and not "Array" and not "Queue" and not "Stack" and not "StringBuilder")
                    {
                        isLengthSafeForHash = false;
                    }
                }

                if (isLengthSafeForHash)
                {
                    // If typeStr is an instance field, prefix with self
                    if (_isInstanceContext && IsInstanceFieldRef(typeStr, memberAccess.Expression))
                    {
                        return $"#self.{typeStr}";
                    }
                    // Generic .Length → #var (works for strings and tables)
                    if (!IsLikelyEnumOrExternalType(typeStr) || _currentMethodParams.Contains(typeStr) || _currentMethodLocals.Contains(typeStr))
                    {
                        return $"#{typeStr}";
                    }
                }
            }

            // .Count on a List/collection → #var (but IList interface/custom → runtime helper)
            if (memberName == "Count")
            {
                // First resolve the correct receiver expression, handling abstract property getters
                string resolvedReceiver = typeStr;
                if (_model != null && memberAccess.Expression is IdentifierNameSyntax countIdent)
                {
                    var countSym = GetSymbol(countIdent);
                    if (countSym is IPropertySymbol countPs && !countPs.IsStatic
                        && (countPs.IsAbstract || HasPropertyBody(countPs)))
                    {
                        resolvedReceiver = $"self:get_{typeStr}()";
                    }
                    else if (_isInstanceContext && IsInstanceFieldRef(typeStr, memberAccess.Expression))
                    {
                        resolvedReceiver = $"self.{typeStr}";
                    }
                }
                else if (_isInstanceContext && IsInstanceFieldRef(typeStr, memberAccess.Expression))
                {
                    resolvedReceiver = $"self.{typeStr}";
                }

                // Check if the receiver is an IList interface or custom IList implementation
                if (_model != null)
                {
                    var countOwnerType = GetExpressionType(memberAccess.Expression);
                    if (countOwnerType != null)
                    {
                        bool isIListIface = countOwnerType.TypeKind == TypeKind.Interface
                            && countOwnerType.Name is "IList" or "ICollection" or "IReadOnlyList" or "IReadOnlyCollection";
                        bool implIList = !isIListIface
                            && countOwnerType.Name is not "List" and not "Array" and not "Queue" and not "Stack"
                            && (countOwnerType.AllInterfaces.Any(i => i.Name is "IList" or "ICollection"));
                        if (isIListIface || implIList)
                        {
                            NeedsRuntime = true;
                            return $"__rt.ilistCount({resolvedReceiver})";
                        }
                    }
                }
                // Fallback for non-IList: use # with the resolved receiver
                return $"#{resolvedReceiver}";
            }

            // .Position, .Width etc. on instance fields → getter or direct access
            // If accessing a member of an instance field (e.g., _window.Position),
            // and we're in instance context, prefix with self.
            // But if the field is actually a non-auto/virtual property, use getter call.
            if (IsInstanceFieldRef(typeStr, memberAccess.Expression))
            {
                string receiverExpr = $"self.{typeStr}";
                if (_model != null && memberAccess.Expression is IdentifierNameSyntax fieldIdent)
                {
                    var fieldSym = GetSymbol(fieldIdent);
                    if (fieldSym is IPropertySymbol fieldPs && !fieldPs.IsStatic && fieldPs.GetMethod != null)
                    {
                        if (fieldPs.IsAbstract || fieldPs.IsVirtual || fieldPs.IsOverride
                            || HasPropertyBody(fieldPs))
                            receiverExpr = $"self:get_{typeStr}()";
                    }
                }
                // Check if the accessed member is a non-auto property requiring a getter call
                if (_model != null)
                {
                    var memberSym = GetSymbol(memberAccess);
                    if (memberSym is IPropertySymbol memberPs && memberPs.GetMethod != null)
                    {
                        bool memberIsAuto = !memberPs.IsAbstract;
                        var memberDeclRef = memberPs.DeclaringSyntaxReferences.FirstOrDefault();
                        if (memberDeclRef?.GetSyntax() is PropertyDeclarationSyntax memberPropSyn)
                        {
                            bool memberHasBody = memberPropSyn.ExpressionBody != null
                                || (memberPropSyn.AccessorList?.Accessors
                                    .Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
                            if (memberHasBody)
                                memberIsAuto = false;
                        }
                        if (!memberIsAuto)
                            return $"{receiverExpr}:get_{memberName}()";
                    }
                }
                return $"{receiverExpr}.{memberName}";
            }

            // Property access → getter call for non-auto properties (IdentifierNameSyntax path)
            if (_model != null)
            {
                var memberSymbol = GetSymbol(memberAccess);
                if (memberSymbol is IPropertySymbol ps2 && ps2.GetMethod != null)
                {
                    bool isAuto2 = !ps2.IsAbstract;
                    var declRef2 = ps2.DeclaringSyntaxReferences.FirstOrDefault();
                    if (declRef2?.GetSyntax() is PropertyDeclarationSyntax propSyntax2)
                    {
                        bool hasExprBody2 = propSyntax2.ExpressionBody != null;
                        bool hasAccessorBody2 = propSyntax2.AccessorList?.Accessors
                            .Any(a => a.Body != null || a.ExpressionBody != null) ?? false;
                        if (hasExprBody2 || hasAccessorBody2)
                            isAuto2 = false;
                    }
                    if (!isAuto2)
                    {
                        var receiver = (_currentMethodParams.Contains(typeStr) || _currentMethodLocals.Contains(typeStr))
                            ? typeStr
                            : (_isInstanceContext && _instanceFields.Contains(typeStr) ? $"self.{typeStr}" : typeStr);
                        if (ps2.IsStatic)
                        {
                            var containingType2 = ps2.ContainingType?.Name;
                            if (containingType2 != null)
                                return $"{containingType2}.get_{memberName}()";
                        }
                        else
                        {
                            return $"{receiver}:get_{memberName}()";
                        }
                    }
                }
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
                // IList<T>/ICollection<T> interface or concrete custom class implementing IList
                // Use runtime helper to handle custom implementations (e.g. JPropertyList)
                if (ownerType != null)
                {
                    bool isIListInterface = ownerType.TypeKind == TypeKind.Interface
                        && ownerType.Name is "IList" or "ICollection" or "IReadOnlyList" or "IReadOnlyCollection";
                    bool implementsIList = !isIListInterface
                        && ownerType.Name is not "List" and not "Array" and not "Queue" and not "Stack"
                        && (ownerType.AllInterfaces.Any(i => i.Name is "IList" or "ICollection"));
                    if (isIListInterface || implementsIList)
                    {
                        NeedsRuntime = true;
                        return $"__rt.ilistCount({left})";
                    }
                }
            }
            // Fallback: use # only when semantic model is unavailable (no type info).
            // When the model IS available but type didn't match any whitelist above,
            // it's a custom struct/class with a .Length/.Count property — fall through
            // to the property getter logic below.
            if (_model == null)
                return $"#{left}";
        }

        // Property access → getter call for non-auto properties
        // Auto-properties are emitted as fields (no get_ method), so direct access works.
        // Non-auto properties store values in backing fields (_field), so we must call the getter.
        if (_model != null)
        {
            var symbol = GetSymbol(memberAccess);
            if (symbol is IPropertySymbol ps && ps.GetMethod != null)
            {
                // Check if the property is auto-implemented (all accessors have no body)
                // Abstract properties have no backing field — must use getter
                bool isAuto = !ps.IsAbstract;
                var declRef = ps.DeclaringSyntaxReferences.FirstOrDefault();
                if (declRef?.GetSyntax() is PropertyDeclarationSyntax propSyntax)
                {
                    bool hasExprBody = propSyntax.ExpressionBody != null;
                    bool hasAccessorBody = propSyntax.AccessorList?.Accessors
                        .Any(a => a.Body != null || a.ExpressionBody != null) ?? false;
                    if (hasExprBody || hasAccessorBody)
                        isAuto = false;
                }
                if (!isAuto)
                {
                    if (ps.IsStatic)
                    {
                        var containingType = ps.ContainingType?.Name;
                        if (containingType != null)
                            return $"{containingType}.get_{memberName}()";
                    }
                    else
                    {
                        return $"{left}:get_{memberName}()";
                    }
                }
            }
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

        var arguments = invocation.ArgumentList.Arguments;
        var args = arguments.Select(a => EmitExpression(a.Expression)).ToList();

        // ── Handle params arrays for bare-name calls ──
        if (_model != null)
        {
            var calledSym = _model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (calledSym != null && calledSym.Parameters.Length > 0)
            {
                var lastParam = calledSym.Parameters[^1];
                if (lastParam.IsParams && lastParam.Type is IArrayTypeSymbol)
                {
                    var nonParamsCount = calledSym.Parameters.Length - 1;
                    if (arguments.Count > nonParamsCount)
                    {
                        bool needsWrap = true;
                        if (arguments.Count == calledSym.Parameters.Length)
                        {
                            var lastArgType = GetExpressionType(arguments[^1].Expression);
                            if (lastArgType is IArrayTypeSymbol)
                                needsWrap = false;
                        }
                        if (needsWrap)
                        {
                            var paramsArgs = args.Skip(nonParamsCount).ToList();
                            args = args.Take(nonParamsCount).ToList();
                            args.Add("{" + string.Join(", ", paramsArgs) + "}");
                        }
                    }
                    else if (arguments.Count == nonParamsCount)
                    {
                        args.Add("{}");
                    }
                }
            }
        }

        var argStr = string.Join(", ", args);
        int argCount = arguments.Count;

        var callResult = EmitBareNameCall(invocation, arguments, args, argStr, argCount);

        // Apply out-param hoisting for any call with out arguments
        var hoisted = TryHoistOutParams(callResult, arguments, args);
        return hoisted ?? callResult;
    }

    /// <summary>
    /// Core logic for bare-name invocations (no member access).
    /// Called by EmitInvocation which wraps with out-param hoisting.
    /// </summary>
    private string EmitBareNameCall(InvocationExpressionSyntax invocation,
        SeparatedSyntaxList<ArgumentSyntax> arguments, List<string> args, string argStr, int argCount)
    {
        // Handle the expression being called — both plain and generic names
        // e.g. DoSomething(x) or DoSomething<T>(x)
        string? bareMethodName = invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gen => gen.Identifier.Text,
            _ => null
        };
        if (bareMethodName != null)
        {
            var name = bareMethodName;

            // nameof(x) → "x"
            if (name == "nameof")
            {
                var firstArg = arguments[0].Expression.ToString();
                return $"\"{firstArg}\"";
            }

            // ReferenceEquals(a, b) → rawequal(a, b)
            if (name == "ReferenceEquals" && argCount == 2)
                return $"rawequal({args[0]}, {args[1]})";

            // GetType() → __rt.getType(self) — bare call to Object.GetType() in instance context
            if (name == "GetType" && argCount == 0 && _isInstanceContext)
            {
                NeedsRuntime = true;
                return "__rt.getType(self)";
            }

            // Resolve overloaded name via the overload map
            // Use semantic model to determine target overload's first param type for precise resolution
            var firstParamType = "";
            string? fullSigResolvedName = null;
            if (_model != null)
            {
                var symbol = GetSymbol(invocation);
                if (symbol is IMethodSymbol targetMethod)
                {
                    if (targetMethod.Parameters.Length > 0)
                    {
                        var fpt = targetMethod.Parameters[0].Type.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.MinimallyQualifiedFormat);
                        var isNullableFpt = fpt.EndsWith("?");
                        fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                        if (isNullableFpt) fpt += "_nullable";
                        firstParamType = fpt;
                    }
                    // Try full-signature lookup for precise resolution
                    var containingType = targetMethod.ContainingType?.Name ?? _currentClassName ?? "";
                    var allParamSig = string.Join(",", targetMethod.Parameters.Select(p =>
                    {
                        var pt = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        var isNullablePt = pt.EndsWith("?")
                            || (p.Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
                        pt = pt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                            .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                        if (isNullablePt) pt += "_nullable";
                        return pt;
                    }));
                    if (FullSignatureOverloadMap.TryGetValue((containingType, name, allParamSig), out var fsResolved))
                    {
                        fullSigResolvedName = fsResolved;
                    }
                }
            }
            var resolvedName = fullSigResolvedName ?? ResolveOverloadedName(name, argCount, firstParamType);

            // In instance context, calling an instance method by bare name → self dispatch
            if (_isInstanceContext && _instanceMethods.Contains(name)
                && !_currentMethodParams.Contains(name)
                && !_currentMethodLocals.Contains(name))
            {
                if (_model != null)
                {
                    var sym = GetSymbol(invocation.Expression);
                    if (sym is IMethodSymbol mSym)
                    {
                        // Static method called as bare name (e.g., Equals(a, b) → object.Equals)
                        // Don't inject self — dispatch via the containing type
                        if (mSym.IsStatic)
                        {
                            var staticTypeName = mSym.ContainingType?.Name ?? _currentClassName!;
                            // Try MethodMapper first
                            var mapResult = MethodMapper.TryRewrite(mSym, staticTypeName, args.ToArray(), null);
                            if (mapResult != null) return mapResult;
                            // Otherwise emit as Type.Method(args)
                            return $"{staticTypeName}.{resolvedName}({argStr})";
                        }

                        var containingTypeName = mSym.ContainingType?.Name;
                        if (containingTypeName != null && containingTypeName != _currentClassName)
                        {
                            // Resolve disambiguated name from the base class
                            string baseResolvedName = name;
                            var bareFSig1 = GetFullParamSig(mSym);
                            if (FullSignatureOverloadMap.TryGetValue((containingTypeName, name, bareFSig1), out var bareFsResolved1))
                                baseResolvedName = bareFsResolved1;
                            else if (TryResolveGlobalOverload(containingTypeName, name, argCount, out var gResolved, GetFirstParamTypeKey(mSym)))
                                baseResolvedName = gResolved;

                            // Check for true shadowing: current class has a same-named method
                            // that is NOT an override of the resolved method
                            bool hasShadowing = false;
                            if (_model.Compilation.GetTypeByMetadataName(
                                    mSym.ContainingType?.ContainingNamespace?.ToDisplayString() + "." + _currentClassName)
                                is INamedTypeSymbol currentTypeSymbol)
                            {
                                hasShadowing = currentTypeSymbol.GetMembers(name)
                                    .OfType<IMethodSymbol>()
                                    .Any(m => !IsSameOrOverrideOf(m, mSym));
                            }
                            else
                            {
                                // Fallback: if we can't resolve the current type symbol,
                                // assume shadowing if current class has the method
                                hasShadowing = true;
                            }

                            if (hasShadowing)
                            {
                                // Explicit dispatch to base class to avoid Lua finding wrong method
                                var runtimeRef = GetRuntimeRef(containingTypeName);
                                if (runtimeRef == containingTypeName)
                                    ReferencedModules.Add(containingTypeName);
                                var fullArgs = string.IsNullOrEmpty(argStr) ? "self" : $"self, {argStr}";
                                return $"{runtimeRef}.{baseResolvedName}({fullArgs})";
                            }

                            // No shadowing, but name may be disambiguated — use virtual dispatch with correct name
                            if (baseResolvedName != name)
                                resolvedName = baseResolvedName;
                        }

                        // Virtual/abstract/override methods need instance dispatch (self:Method)
                        // for correct polymorphic behavior through the metatable chain
                        if (mSym.IsVirtual || mSym.IsAbstract || mSym.IsOverride)
                        {
                            return string.IsNullOrEmpty(argStr) ? $"self:{resolvedName}()" : $"self:{resolvedName}({argStr})";
                        }
                    }
                }
                // Non-virtual: static dispatch via class table
                if (string.IsNullOrEmpty(argStr))
                    return $"{_currentClassName}.{resolvedName}(self)";
                return $"{_currentClassName}.{resolvedName}(self, {argStr})";
            }

            // SemanticModel: resolve local functions and inherited instance methods
            if (_model != null)
            {
                var symbol = GetSymbol(invocation.Expression);
                if (symbol is IMethodSymbol ms)
                {
                    if (ms.MethodKind == MethodKind.LocalFunction)
                        return $"{resolvedName}({argStr})";

                    // Inherited instance method called by bare name → add self dispatch
                    if (!ms.IsStatic && ms.MethodKind == MethodKind.Ordinary && _currentClassName != null)
                    {
                        var containingTypeName = ms.ContainingType?.Name;
                        if (containingTypeName != null && containingTypeName != _currentClassName)
                        {
                            // Resolve disambiguated name from the base class
                            string baseResolvedName = name;
                            var bareFSig2 = GetFullParamSig(ms);
                            if (FullSignatureOverloadMap.TryGetValue((containingTypeName, name, bareFSig2), out var bareFsResolved2))
                                baseResolvedName = bareFsResolved2;
                            else if (TryResolveGlobalOverload(containingTypeName, name, argCount, out var gResolved, GetFirstParamTypeKey(ms)))
                                baseResolvedName = gResolved;

                            // Check for true shadowing if current class has same-named method
                            if (_instanceMethods.Contains(name))
                            {
                                bool hasShadowing = false;
                                if (_model.Compilation.GetTypeByMetadataName(
                                        ms.ContainingType?.ContainingNamespace?.ToDisplayString() + "." + _currentClassName)
                                    is INamedTypeSymbol currentTypeSymbol)
                                {
                                    hasShadowing = currentTypeSymbol.GetMembers(name)
                                        .OfType<IMethodSymbol>()
                                        .Any(m => !IsSameOrOverrideOf(m, ms));
                                }
                                else
                                {
                                    hasShadowing = true;
                                }

                                if (hasShadowing)
                                {
                                    var runtimeRef = GetRuntimeRef(containingTypeName);
                                    if (runtimeRef == containingTypeName)
                                        ReferencedModules.Add(containingTypeName);
                                    var fullArgs = string.IsNullOrEmpty(argStr) ? "self" : $"self, {argStr}";
                                    return $"{runtimeRef}.{baseResolvedName}({fullArgs})";
                                }
                            }

                            // No shadowing, but name may be disambiguated
                            if (baseResolvedName != name)
                                resolvedName = baseResolvedName;
                        }

                        // Virtual/abstract/override → instance dispatch
                        if (ms.IsVirtual || ms.IsAbstract || ms.IsOverride)
                            return string.IsNullOrEmpty(argStr) ? $"self:{resolvedName}()" : $"self:{resolvedName}({argStr})";
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
    private string ResolveOverloadedName(string name, int argCount, string firstArgType = "")
    {
        // Try exact match with first param type
        if (firstArgType != "" && _overloadMap.TryGetValue((name, argCount, firstArgType), out var exactMatch))
            return exactMatch;
        // Fall back to any matching (name, argCount) entry
        foreach (var kvp in _overloadMap)
        {
            if (kvp.Key.Name == name && kvp.Key.ParamCount == argCount)
                return kvp.Value;
        }
        return name;
    }

    private bool TryResolveGlobalOverload(string typeName, string methodName, int argCount, out string resolvedName, string firstParamType = "")
    {
        // Try exact 4-tuple match first (most precise)
        if (firstParamType != "")
        {
            var exactKey = (typeName, methodName, argCount, firstParamType);
            if (GlobalOverloadMap.TryGetValue(exactKey, out var exactResolved))
            {
                resolvedName = exactResolved;
                return true;
            }
        }
        // Fall back to any matching entry (ignore first param type)
        foreach (var kvp in GlobalOverloadMap)
        {
            if (kvp.Key.TypeName == typeName && kvp.Key.MethodName == methodName && kvp.Key.ArgCount == argCount)
            {
                resolvedName = kvp.Value;
                return true;
            }
        }
        resolvedName = methodName;
        return false;
    }

    /// <summary>
    /// Extract the first parameter type key from an IMethodSymbol,
    /// formatted to match GlobalOverloadMap key conventions.
    /// </summary>
    private static string GetFirstParamTypeKey(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0) return "";
        var fpt = method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var isNullable = fpt.EndsWith("?")
            || (method.Parameters[0].Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
        fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
            .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
        if (isNullable) fpt += "_nullable";
        return fpt;
    }

    /// <summary>
    /// Build the full parameter signature string for FullSignatureOverloadMap lookups.
    /// </summary>
    private static string GetFullParamSig(IMethodSymbol method)
    {
        return string.Join(",", method.Parameters.Select(p =>
        {
            var pt = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var isNullablePt = pt.EndsWith("?")
                || (p.Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
            pt = pt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
            if (isNullablePt) pt += "_nullable";
            return pt;
        }));
    }

    /// <summary>
    /// Check if <paramref name="method"/> is the same as or an override of <paramref name="target"/>
    /// by walking up the override chain.
    /// </summary>
    private static bool IsSameOrOverrideOf(IMethodSymbol method, IMethodSymbol target)
    {
        var current = method;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
            // Also check OriginalDefinition for generic methods
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target.OriginalDefinition)) return true;
            current = current.OverriddenMethod;
        }
        return false;
    }

    /// <summary>
    /// Resolve cross-inheritance method dispatch for an instance method call.
    /// Returns the correct call expression, or null if default colon dispatch with the
    /// original memberName is correct.
    /// Handles two cases:
    /// 1. Name disambiguation (e.g., WriteTo → WriteTo_JsonWriter) — returns colon syntax with fixed name
    /// 2. True shadowing (different method with same name in derived class) — returns explicit dispatch
    /// </summary>
    private string? TryResolveCrossInheritanceCall(
        IMethodSymbol resolvedMethod, string memberName, string receiver, string argStr, int argCount,
        ExpressionSyntax receiverExpression)
    {
        var containingType = resolvedMethod.ContainingType;
        if (containingType == null) return null;

        // Resolve disambiguated name — try FullSignatureOverloadMap first, then GlobalOverloadMap
        string resolvedName = memberName;
        var fullSig = GetFullParamSig(resolvedMethod);
        if (FullSignatureOverloadMap.TryGetValue((containingType.Name, memberName, fullSig), out var fsResolved))
            resolvedName = fsResolved;
        else if (TryResolveGlobalOverload(containingType.Name, memberName, argCount, out var globalResolved, GetFirstParamTypeKey(resolvedMethod)))
            resolvedName = globalResolved;

        // Check for cross-inheritance shadowing
        var receiverType = _model!.GetTypeInfo(receiverExpression).Type as INamedTypeSymbol;
        if (receiverType != null && !SymbolEqualityComparer.Default.Equals(containingType, receiverType))
        {
            // Check if receiver type has a same-named method that truly shadows
            // (not an override of the resolved method)
            bool hasShadowingMethod = receiverType.GetMembers(memberName)
                .OfType<IMethodSymbol>()
                .Any(m => !IsSameOrOverrideOf(m, resolvedMethod));

            if (hasShadowingMethod)
            {
                // Explicit dispatch needed — Lua __index would find the wrong method
                ReferencedModules.Add(containingType.Name);
                var fullArgs = string.IsNullOrEmpty(argStr) ? receiver : $"{receiver}, {argStr}";
                return $"{containingType.Name}.{resolvedName}({fullArgs})";
            }
        }

        // If name was disambiguated but no shadowing, use colon syntax with disambiguated name
        if (resolvedName != memberName)
        {
            return $"{receiver}:{resolvedName}({argStr})";
        }

        return null; // default dispatch is correct
    }

    /// <summary>
    /// Handle method calls via member access: obj.Method(args), Type.Method(args), etc.
    /// This is the central place for .NET → Luau method mapping.
    /// </summary>
    private string EmitMemberInvocation(MemberAccessExpressionSyntax memberAccess, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var memberName = memberAccess.Name.Identifier.Text;

        // ── Strip no-op .NET methods — identity in Luau ──
        if (memberName is "ConfigureAwait" or "Cast" or "AsEnumerable"
            or "ToLocalTime" or "ToUniversalTime")
            return EmitExpression(memberAccess.Expression);

        // ── typeof(T).Method() / "T".Method() — reflection methods on type objects ──
        // In Luau there's no runtime type system, so these are best-effort constants
        if (memberAccess.Expression is TypeOfExpressionSyntax typeOfExpr)
        {
            var typeStr = typeOfExpr.Type.ToString().Replace(",", "").Replace(" ", "").Replace("<", "_").Replace(">", "");
            if (memberName is "IsAssignableFrom" or "IsSubclassOf" or "IsInstanceOfType")
                return "false --[[typeof reflection]]";
            if (memberName is "MakeGenericType" or "GetGenericTypeDefinition")
                return $"\"{typeStr}\" --[[typeof reflection]]";
            if (memberName is "GetMethod" or "GetField" or "GetProperty" or "GetMember"
                or "GetConstructor" or "GetMethods" or "GetFields" or "GetProperties"
                or "GetInterfaces" or "GetElementType" or "GetGenericArguments"
                or "GetCustomAttributes" or "GetCustomAttribute")
                return $"nil --[[typeof({typeStr}).{memberName}]]";
            if (memberName is "Equals")
            {
                if (arguments.Count == 1)
                    return $"(\"{typeStr}\" == {EmitExpression(arguments[0].Expression)})";
                return "false";
            }
            // Fallback: emit as string (type name) — better than broken method call
            return $"\"{typeStr}\"";
        }

        // ── Nullable<T>.GetValueOrDefault() → (x or 0) / (x or default) ──
        if (memberName == "GetValueOrDefault")
        {
            var receiver = EmitExpression(memberAccess.Expression);
            if (arguments.Count == 1)
                return $"({receiver} or {EmitExpression(arguments[0].Expression)})";
            // No args: determine default based on underlying type
            var defaultVal = "0";
            if (_model != null)
            {
                var receiverType = GetExpressionType(memberAccess.Expression);
                if (receiverType is INamedTypeSymbol nts
                    && nts.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
                    && nts.TypeArguments.Length > 0)
                {
                    var underlying = nts.TypeArguments[0];
                    if (underlying.SpecialType == SpecialType.System_Boolean)
                        defaultVal = "false";
                }
                else if (receiverType?.SpecialType == SpecialType.System_Boolean)
                    defaultVal = "false";
            }
            return $"({receiver} or {defaultVal})";
        }

        // ── String case methods — work on any expression receiver ──
        if (memberName is "ToLower" or "ToLowerInvariant" && arguments.Count == 0)
            return $"string.lower({EmitExpression(memberAccess.Expression)})";
        if (memberName is "ToUpper" or "ToUpperInvariant" && arguments.Count == 0)
            return $"string.upper({EmitExpression(memberAccess.Expression)})";

        // String.CopyTo(srcIdx, dest, destIdx, count) → inline char copy loop
        // Emits as a statement via helper: __rt.stringCopyTo(src, srcIdx, dest, destIdx, count)
        if (memberName == "CopyTo" && arguments.Count == 4)
        {
            var src = EmitExpression(memberAccess.Expression);
            var srcIdx = EmitExpression(arguments[0].Expression);
            var dest = EmitExpression(arguments[1].Expression);
            var destIdx = EmitExpression(arguments[2].Expression);
            var count = EmitExpression(arguments[3].Expression);
            return $"__rt.stringCopyTo({src}, {srcIdx}, {dest}, {destIdx}, {count})";
        }

        var args = arguments.Select(a => EmitExpression(a.Expression)).ToList();

        // ── Handle params arrays: wrap expanded arguments into a table constructor ──
        // In C#, `params T[]` allows passing individual values that get auto-wrapped into an array.
        // In Luau, we need to explicitly wrap them in { ... }.
        if (_model != null)
        {
            var calledSym = _model.GetSymbolInfo(memberAccess.Parent!).Symbol as IMethodSymbol;
            if (calledSym != null && calledSym.Parameters.Length > 0)
            {
                var lastParam = calledSym.Parameters[^1];
                if (lastParam.IsParams && lastParam.Type is IArrayTypeSymbol)
                {
                    // Check if the caller is passing expanded args (not already an array)
                    // This is true when we have more arguments than non-params parameters
                    var nonParamsCount = calledSym.Parameters.Length - 1;
                    if (arguments.Count > nonParamsCount)
                    {
                        // Check that the args aren't already passed as an array
                        // If exactly one arg at the params position and it matches the array type, don't wrap
                        bool needsWrap = true;
                        if (arguments.Count == calledSym.Parameters.Length)
                        {
                            var lastArgType = GetExpressionType(arguments[^1].Expression);
                            if (lastArgType is IArrayTypeSymbol)
                                needsWrap = false;
                        }
                        if (needsWrap)
                        {
                            var paramsArgs = args.Skip(nonParamsCount).ToList();
                            args = args.Take(nonParamsCount).ToList();
                            args.Add("{" + string.Join(", ", paramsArgs) + "}");
                        }
                    }
                    else if (arguments.Count == nonParamsCount)
                    {
                        // No params args passed — add empty table
                        args.Add("{}");
                    }
                }
            }
        }

        var argStr = string.Join(", ", args);

        // ── Handle base.Method(args) → ParentClass.Method(self, args) ──
        if (memberAccess.Expression is BaseExpressionSyntax && _baseClassName != null)
        {
            // base.GetType() → __rt.getType(self)
            if (memberName == "GetType" && arguments.Count == 0)
            {
                NeedsRuntime = true;
                return $"__rt.getType(self)";
            }
            // Resolve overloaded method name for base class
            var baseResolvedName = memberName;
            if (_model != null)
            {
                var baseSym = _model.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
                if (baseSym != null)
                {
                    var fullSig = GetFullParamSig(baseSym);
                    if (FullSignatureOverloadMap.TryGetValue((_baseClassName, memberName, fullSig), out var fsName))
                        baseResolvedName = fsName;
                    else
                    {
                        var fpt = GetFirstParamTypeKey(baseSym);
                        TryResolveGlobalOverload(_baseClassName, memberName, baseSym.Parameters.Length, out baseResolvedName, fpt);
                    }
                }
            }
            if (string.IsNullOrEmpty(argStr))
                return $"{_baseClassName}.{baseResolvedName}(self)";
            return $"{_baseClassName}.{baseResolvedName}(self, {argStr})";
        }

        // ── Handle this.Method(args) → ClassName.Method(self, args) ──
        if (memberAccess.Expression is ThisExpressionSyntax)
        {
            // this.GetType() → __rt.getType(self)
            if (memberName == "GetType" && arguments.Count == 0)
            {
                NeedsRuntime = true;
                return $"__rt.getType(self)";
            }

            // Virtual/abstract/override → instance dispatch for polymorphism
            if (_model != null)
            {
                var sym = GetSymbol(memberAccess);
                if (sym is IMethodSymbol mSym && (mSym.IsVirtual || mSym.IsAbstract || mSym.IsOverride))
                {
                    return string.IsNullOrEmpty(argStr) ? $"self:{memberName}()" : $"self:{memberName}({argStr})";
                }
            }

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
                    var hoisted = TryHoistOutParams(result, arguments, args);
                    return hoisted ?? result;
                }

                // Extension methods: emit as static call ContainingType.Method(receiver, args)
                // e.g., bindingAttr.RemoveFlag(16) → ReflectionUtils.RemoveFlag(bindingAttr, 16)
                if (symbol.IsExtensionMethod)
                {
                    var containingType = symbol.ContainingType?.Name;
                    if (containingType != null)
                    {
                        var allArgs = new[] { obj }.Concat(args);
                        var callResult = $"{containingType}.{memberName}({string.Join(", ", allArgs)})";
                        var hoisted = TryHoistOutParams(callResult, arguments, args);
                        return hoisted ?? callResult;
                    }
                }

                // Static method calls to other types: resolve overloaded names via GlobalOverloadMap
                // e.g., JsonConvert.ToString(value, ...) → JsonConvert.ToString_float__2(value, ...)
                if (symbol.IsStatic)
                {
                    var containingTypeName = symbol.ContainingType?.Name;
                    if (containingTypeName != null)
                    {
                        var resolvedName = memberName;
                        // Use MinimallyQualifiedFormat to get C# keyword names (float, bool, etc.)
                        // which matches the syntax-based keys in GlobalOverloadMap
                        var paramType = symbol.Parameters.FirstOrDefault()?.Type;
                        var firstParamType = paramType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "";
                        // Match the scanner's nullable convention: strip ?, append _nullable
                        var isNullableParam = firstParamType.EndsWith("?")
                            || (paramType is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
                        firstParamType = firstParamType.Replace("?", "").Replace("[]", "_Array")
                            .Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                        if (isNullableParam) firstParamType += "_nullable";

                        // Use symbol.Parameters.Length for arg count (matches how the map was populated)
                        var mapArgCount = symbol.Parameters.Length;

                        // Try full-signature map first (most precise — handles ambiguous first-param keys)
                        var allParamSig = string.Join(",", symbol.Parameters.Select(p =>
                        {
                            var pt = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            var isNullablePt = pt.EndsWith("?")
                                || (p.Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
                            pt = pt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                                .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                            if (isNullablePt) pt += "_nullable";
                            return pt;
                        }));
                        var fullKey = (containingTypeName, memberName, allParamSig);
                        if (FullSignatureOverloadMap.TryGetValue(fullKey, out var fullResolved))
                            resolvedName = fullResolved;

                        // Fall back to simple 4-tuple key
                        if (resolvedName == memberName)
                        {
                            var preciseKey = (containingTypeName, memberName, mapArgCount, firstParamType);
                            if (GlobalOverloadMap.TryGetValue(preciseKey, out var preciseResolved))
                                resolvedName = preciseResolved;
                            else if (TryResolveGlobalOverload(containingTypeName, memberName, mapArgCount, out var gResolved))
                                resolvedName = gResolved;
                        }

                        if (resolvedName != memberName)
                        {
                            ReferencedModules.Add(containingTypeName);
                            var callResult = $"{obj}.{resolvedName}({argStr})";
                            var hoisted = TryHoistOutParams(callResult, arguments, args);
                            return hoisted ?? callResult;
                        }
                    }
                }
            }
        }

        // ── Handle nested member access: System.Convert.Method(...) → flatten to Convert.Method ──
        if (memberAccess.Expression is MemberAccessExpressionSyntax nestedMember
            && nestedMember.Expression is IdentifierNameSyntax nsIdent
            && nsIdent.Identifier.Text == "System")
        {
            // Rewrite: treat System.Convert.X as Convert.X
            var innerType = nestedMember.Name.Identifier.Text;
            if (innerType is "Convert")
            {
                if (memberName == "ChangeType" && arguments.Count >= 2)
                    return EmitExpression(arguments[0].Expression);
                if (memberName == "FromBase64String" && arguments.Count >= 1)
                {
                    NeedsRuntime = true;
                    return $"__rt.fromBase64({EmitExpression(arguments[0].Expression)})";
                }
                if (memberName == "ToDateTime" && arguments.Count >= 1)
                    return EmitExpression(arguments[0].Expression);
                if (memberName == "ToChar" && arguments.Count >= 1)
                    return $"string.char({EmitExpression(arguments[0].Expression)})";
                if (memberName == "ToString" && arguments.Count >= 1)
                    return $"tostring({EmitExpression(arguments[0].Expression)})";
            }
        }

        // ── Handle 3-level nested: System.Numerics.BigInteger.Parse(...) ──
        if (memberAccess.Expression is MemberAccessExpressionSyntax nestedMember2
            && nestedMember2.Expression is MemberAccessExpressionSyntax deepNested
            && deepNested.Expression is IdentifierNameSyntax deepNsIdent
            && deepNsIdent.Identifier.Text == "System")
        {
            var midNs = deepNested.Name.Identifier.Text;
            var innerType2 = nestedMember2.Name.Identifier.Text;
            if (midNs == "Numerics" && innerType2 == "BigInteger")
            {
                if (memberName == "Parse" && arguments.Count >= 1)
                    return $"tonumber({EmitExpression(arguments[0].Expression)})";
            }
        }

        // ── Handle Comparer<T>.Default.Compare(a, b) → inline ternary comparison ──
        if (memberAccess.Expression is MemberAccessExpressionSyntax comparerMember
            && comparerMember.Name.Identifier.Text == "Default"
            && memberName == "Compare" && arguments.Count == 2)
        {
            var comparerName = comparerMember.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                GenericNameSyntax gen => gen.Identifier.Text,
                _ => null
            };
            if (comparerName == "Comparer")
            {
                var a = EmitExpression(arguments[0].Expression);
                var b = EmitExpression(arguments[1].Expression);
                return $"(if {a} < {b} then -1 elseif {a} > {b} then 1 else 0)";
            }
        }

        // ── Handle calls on identifier targets (including generic names like CachedAttributeGetter<T>) ──
        string? _ownerNameExtracted = memberAccess.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax gen => gen.Identifier.Text,
            _ => null
        };
        if (_ownerNameExtracted != null)
        {
            var ownerName = _ownerNameExtracted;

            // Console.WriteLine(x) → print(x)
            if (ownerName == "Console" && memberName is "WriteLine" or "Write")
            {
                return $"print({argStr})";
            }

            // Array.Empty<T>() → {}
            if (ownerName == "Array" && memberName == "Empty")
                return "{}";

            // Array.IndexOf(array, value) → (table.find(array, value) or 0) - 1
            // Array.IndexOf with 3-4 args also maps (startIndex/count ignored for simplicity)
            if (ownerName == "Array" && memberName == "IndexOf" && arguments.Count >= 2)
            {
                var arr = EmitExpression(arguments[0].Expression);
                var val = EmitExpression(arguments[1].Expression);
                return $"((table.find({arr}, {val}) or 0) - 1)";
            }

            // Array.BinarySearch(array, value) → (table.find(array, value) or 0) - 1
            // Not a true binary search but semantically equivalent for sorted arrays
            if (ownerName == "Array" && memberName == "BinarySearch" && arguments.Count >= 2)
            {
                var arr = EmitExpression(arguments[0].Expression);
                var val = EmitExpression(arguments[1].Expression);
                return $"((table.find({arr}, {val}) or 0) - 1)";
            }

            // Array.Copy(src, dst, length) → table.move(src, 1, length, 1, dst)
            if (ownerName == "Array" && memberName == "Copy" && arguments.Count == 3)
            {
                var src = EmitExpression(arguments[0].Expression);
                var dst = EmitExpression(arguments[1].Expression);
                var len = EmitExpression(arguments[2].Expression);
                return $"table.move({src}, 1, {len}, 1, {dst})";
            }

            // Array.Reverse(array) → __rt.reverse(array)
            if (ownerName == "Array" && memberName == "Reverse" && arguments.Count == 1)
            {
                NeedsRuntime = true;
                return $"__rt.reverse({EmitExpression(arguments[0].Expression)})";
            }

            // Array.CreateInstance(type, size) → table.create(size, 0)
            if (ownerName == "Array" && memberName == "CreateInstance" && arguments.Count == 2)
            {
                var size = EmitExpression(arguments[1].Expression);
                return $"table.create({size}, 0)";
            }

            // Array.Sort(array) → table.sort(array)
            if (ownerName == "Array" && memberName == "Sort" && arguments.Count >= 1)
            {
                return $"table.sort({EmitExpression(arguments[0].Expression)})";
            }

            // Convert.ToInt32/ToInt64/ToByte(x, ...) → math.floor(tonumber(x))
            // Convert.ToDouble/ToSingle/ToDecimal(x, ...) → tonumber(x)
            // Convert.ToBoolean(x) → (x and true or false) — truthy coercion
            // Convert.ToString(x, ...) → tostring(x)
            // Convert.ToBase64String(x) → __rt.toBase64(x)
            // Convert.FromBase64String(x) → __rt.fromBase64(x)
            if (ownerName is "Convert" or "System")
            {
                if (memberName is "ToInt32" or "ToInt64" or "ToByte" or "ToInt16" or "ToUInt16" or "ToUInt32" or "ToUInt64" or "ToSByte")
                {
                    var val = EmitExpression(arguments[0].Expression);
                    return $"math.floor(tonumber({val}))";
                }
                if (memberName is "ToDouble" or "ToSingle" or "ToDecimal")
                {
                    var val = EmitExpression(arguments[0].Expression);
                    return $"tonumber({val})";
                }
                if (memberName == "ToBoolean" && arguments.Count >= 1)
                {
                    var val = EmitExpression(arguments[0].Expression);
                    return $"(not not {val})";
                }
                if (memberName == "ToString" && arguments.Count >= 1 && ownerName == "Convert")
                {
                    var val = EmitExpression(arguments[0].Expression);
                    return $"tostring({val})";
                }
                if (memberName == "ToBase64String" && arguments.Count >= 1)
                {
                    NeedsRuntime = true;
                    return $"__rt.toBase64({EmitExpression(arguments[0].Expression)})";
                }
                if (memberName == "FromBase64String" && arguments.Count >= 1)
                {
                    NeedsRuntime = true;
                    return $"__rt.fromBase64({EmitExpression(arguments[0].Expression)})";
                }
                if (memberName == "FromBase64CharArray" && arguments.Count >= 3)
                {
                    NeedsRuntime = true;
                    // FromBase64CharArray(chars, offset, length) → fromBase64(string.sub(chars, offset+1, offset+length))
                    var chars = EmitExpression(arguments[0].Expression);
                    var offset = EmitExpression(arguments[1].Expression);
                    var length = EmitExpression(arguments[2].Expression);
                    return $"__rt.fromBase64(string.sub({chars}, {offset} + 1, {offset} + {length}))";
                }
                if (memberName == "ToChar" && arguments.Count >= 1)
                {
                    var val = EmitExpression(arguments[0].Expression);
                    return $"string.char({val})";
                }
                if (memberName == "ToDateTime" && arguments.Count >= 1)
                {
                    // Pass-through: DateTime is a value type, Convert.ToDateTime is mostly identity
                    return EmitExpression(arguments[0].Expression);
                }
                if (memberName == "ChangeType" && arguments.Count >= 2)
                {
                    // Best-effort: pass through the value (type coercion not available in Luau)
                    return EmitExpression(arguments[0].Expression);
                }
            }

            // Activator.CreateInstance(type) → {} (empty table stub)
            if (ownerName == "Activator" && memberName == "CreateInstance" && arguments.Count >= 1)
                return "{}";

            // Volatile.Read(x) → x, Volatile.Write(x, v) → assignment handled at statement level
            if (ownerName == "Volatile")
            {
                if (memberName == "Read" && arguments.Count == 1)
                    return EmitExpression(arguments[0].Expression);
                if (memberName == "Write" && arguments.Count == 2)
                {
                    // Volatile.Write(ref x, v) — in single-threaded Luau, just assign
                    var target = EmitExpression(arguments[0].Expression);
                    var value = EmitExpression(arguments[1].Expression);
                    return $"(function() {target} = {value} end)()";
                }
            }

            // Attribute.GetCustomAttributes(...) → {} (no reflection in Luau)
            if (ownerName == "Attribute" && memberName == "GetCustomAttributes")
                return "{}";

            // Assembly.LoadWithPartialName(name) → nil (no assembly loading in Luau)
            if (ownerName == "Assembly" && memberName == "LoadWithPartialName")
                return "nil";

            // BigInteger.Parse(str, ...) → tonumber(str)
            if (ownerName == "BigInteger" && memberName == "Parse" && arguments.Count >= 1)
                return $"tonumber({EmitExpression(arguments[0].Expression)})";

            // object.ReferenceEquals(a, b) → rawequal(a, b)
            if (memberName == "ReferenceEquals" && arguments.Count == 2)
                return $"rawequal({EmitExpression(arguments[0].Expression)}, {EmitExpression(arguments[1].Expression)})";

            // Interlocked.CompareExchange(ref location, value, comparand) → location = value (single-threaded Luau)
            if (ownerName == "Interlocked" && memberName == "CompareExchange" && arguments.Count == 3)
            {
                var location = EmitExpression(arguments[0].Expression);
                var value = EmitExpression(arguments[1].Expression);
                // In single-threaded Luau, CAS is just: location = location or value
                return $"{location} = {location} or {value}";
            }

            // GC.SuppressFinalize / GC.Collect → no-op in Luau (no GC control)
            if (ownerName == "GC")
                return "-- GC (no-op)";

            // Encoding.GetBytes(s) / GetByteCount(s) → runtime UTF-8 helpers
            if (ownerName == "Encoding")
            {
                NeedsRuntime = true;
                if (memberName == "GetBytes") return $"__rt.Encoding.UTF8.GetBytes({argStr})";
                if (memberName == "GetByteCount") return $"__rt.Encoding.UTF8.GetByteCount({argStr})";
                if (memberName == "GetChars") return $"__rt.Encoding.UTF8.GetChars({argStr})";
                if (memberName == "GetMaxCharCount") return $"__rt.Encoding.UTF8.GetMaxCharCount({argStr})";
                if (memberName == "GetString") return $"__rt.Encoding.UTF8.GetString({argStr})";
            }

            // Expression.* → runtime stubs for expression tree construction (only known factory methods)
            if (ownerName == "Expression" && memberName is "Constant" or "Convert" or "Call"
                or "New" or "Parameter" or "Variable" or "Assign" or "ArrayIndex"
                or "Block" or "Lambda" or "Condition" or "Throw" or "TypeAs"
                or "MakeBinary" or "MakeUnary" or "Property" or "Field"
                or "Invoke" or "NewArrayInit" or "NewArrayBounds" or "ListInit"
                or "Bind" or "MemberInit" or "TypeIs" or "Coalesce" or "Quote"
                or "TryCatch" or "TryFinally" or "IfThen" or "IfThenElse" or "Loop"
                or "Break" or "Continue" or "Return" or "Label" or "Goto"
                or "Switch" or "SwitchCase" or "Default" or "Empty" or "Not"
                or "Negate" or "UnaryPlus" or "OnesComplement" or "Power"
                or "Add" or "Subtract" or "Multiply" or "Divide" or "Modulo"
                or "And" or "Or" or "ExclusiveOr" or "LeftShift" or "RightShift"
                or "Equal" or "NotEqual" or "LessThan" or "GreaterThan"
                or "LessThanOrEqual" or "GreaterThanOrEqual" or "AndAlso" or "OrElse"
                or "MakeMemberAccess" or "GetFuncType" or "GetDelegateType"
                or "ReferenceEqual" or "ReferenceNotEqual")
            {
                NeedsRuntime = true;
                return $"__rt.Expression.{memberName}({argStr})";
            }

            // Debug.Assert → assert()
            if (ownerName == "Debug" && memberName == "Assert")
            {
                if (arguments.Count >= 2)
                    return $"assert({EmitExpression(arguments[0].Expression)}, {EmitExpression(arguments[1].Expression)})";
                if (arguments.Count == 1)
                    return $"assert({EmitExpression(arguments[0].Expression)})";
                return "-- Debug.Assert()";
            }

            // System.Enum static methods → runtime helpers
            if (ownerName == "Enum")
            {
                NeedsRuntime = true;
                if (memberName == "GetNames" && arguments.Count == 1)
                    return $"__rt.Enum_GetNames({args[0]})";
                if (memberName == "GetValues" && arguments.Count == 1)
                    return $"__rt.Enum_GetValues({args[0]})";
                if (memberName == "GetName" && arguments.Count == 2)
                    return $"__rt.Enum_GetName({args[0]}, {args[1]})";
                if (memberName == "IsDefined" && arguments.Count == 2)
                    return $"__rt.Enum_IsDefined({args[0]}, {args[1]})";
                if (memberName == "Parse" && arguments.Count >= 2)
                    return $"__rt.Enum_Parse({args[0]}, {args[1]})";
                if (memberName == "ToObject")
                    return args.Count > 1 ? args[1] : args[0];
                return $"nil --[[Enum.{memberName}]]";
            }

            // Known external calls that we can't transpile — emit as TODO
            if (ownerName is "CharUnicodeInfo" or "Char")
            {
                return $"--[[TODO: {ownerName}.{memberName}]] nil";
            }

            // ── .NET string/collection method rewrites ──

            // Resolve the actual Luau name for the owner
            string luauOwner;
            if (_isInstanceContext && IsInstanceFieldRef(ownerName, memberAccess.Expression)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName))
            {
                luauOwner = $"self.{ownerName}";
            }
            else if (_constFields.Contains(ownerName)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName)
                && _currentClassName != null)
            {
                luauOwner = $"{_currentClassName}.{ownerName}";
            }
            else
            {
                luauOwner = ownerName;
            }

            // List<T>.Add(item) → table.insert(list, item)
            // Dictionary<K,V>.Add(key, value) → dict[key] = value
            // But NOT DateTime.Add(TimeSpan) or other non-collection Add methods
            if (memberName == "Add")
            {
                // Check if the Add method returns void (collection Add) vs a value (DateTime.Add, etc.)
                bool isCollectionAdd = true;
                if (_model != null)
                {
                    var symbol = GetSymbol(memberAccess);
                    if (symbol is IMethodSymbol addMethod && !addMethod.ReturnsVoid)
                        isCollectionAdd = false;
                }

                if (isCollectionAdd)
                {
                    if (arguments.Count == 2)
                    {
                        var key = EmitExpression(arguments[0].Expression);
                        var val = EmitExpression(arguments[1].Expression);
                        return $"{luauOwner}[{key}] = {val}";
                    }
                    return $"table.insert({luauOwner}, {argStr})";
                }
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

            // .Contains(x) — string vs collection
            if (memberName == "Contains" && args.Count == 1)
            {
                // Use semantic model to distinguish string.Contains from collection.Contains
                bool isString = IsStringType(memberAccess.Expression);
                if (isString)
                    return $"(string.find({luauOwner}, {args[0]}, 1, true) ~= nil)";
                else
                    return $"(table.find({luauOwner}, {args[0]}) ~= nil)";
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

            // .GetType() → __rt.getType(obj)
            if (memberName == "GetType" && args.Count == 0)
            {
                NeedsRuntime = true;
                return $"__rt.getType({luauOwner})";
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
                    && TryResolveGlobalOverload(fieldType, memberName, args.Count, out var resolvedName))
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

            // Calls on static/const fields holding object instances → colon syntax for instance dispatch
            if (_constFields.Contains(ownerName)
                && !_currentMethodParams.Contains(ownerName)
                && !_currentMethodLocals.Contains(ownerName))
            {
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

            // .NET Type/reflection method calls that can't dispatch via colon syntax
            // (because the receiver may be a string or table, not a class with these methods)
            if (memberName == "IsEnum")
            {
                NeedsRuntime = true;
                return $"__rt.Type_IsEnum({luauOwner})";
            }
            if (memberName is "IsValueType" or "IsClass" or "IsInterface"
                or "IsAbstract" or "IsArray" or "IsGenericType" or "IsPrimitive"
                or "IsSealed" or "IsSerializable" or "IsPublic")
            {
                return "false --[[reflection stub]]";
            }
            if (memberName is "GetField" or "GetFields" && args.Count >= 1)
            {
                NeedsRuntime = true;
                return $"__rt.Type_GetField({luauOwner}, {args[0]})";
            }
            if (memberName is "GetCustomAttributes" or "GetCustomAttribute")
                return "{}";
            if (memberName == "IsDefined")
                return "false";

            // Try MethodMapper name-based fallback (for static calls like TypeDescriptor.GetConverter)
            var mappedByName = MethodMapper.TryRewriteByName(ownerName, memberName, luauOwner, args.Select(a => a).ToArray());
            if (mappedByName != null)
            {
                if (mappedByName.Contains("__rt.")) NeedsRuntime = true;
                var hoisted = TryHoistOutParams(mappedByName, arguments, args);
                return hoisted ?? mappedByName;
            }

            // If the owner is a parameter or local variable (not a static/enum type),
            // use colon syntax for instance method dispatch.
            // Use semantic model to resolve overloaded method names across modules.
            if (_currentMethodParams.Contains(ownerName) || _currentMethodLocals.Contains(ownerName))
            {
                var resolvedMemberName = memberName;
                if (_model != null)
                {
                    var symbolInfo = _model.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol calledMethod && !calledMethod.IsStatic)
                    {
                        // Use cross-inheritance resolution (handles shadowing + name disambiguation)
                        var crossResult = TryResolveCrossInheritanceCall(
                            calledMethod, memberName, luauOwner, argStr, args.Count, memberAccess.Expression);
                        if (crossResult != null)
                        {
                            var hoisted = TryHoistOutParams(crossResult, arguments, args);
                            return hoisted ?? crossResult;
                        }

                        // Fallback: resolve within-type overloads
                        var containingType = calledMethod.ContainingType;
                        if (containingType != null)
                        {
                            var sameNameMethods = containingType.GetMembers(memberName)
                                .OfType<IMethodSymbol>()
                                .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary)
                                .ToList();
                            if (sameNameMethods.Count > 1)
                            {
                                var typeName = containingType.Name;
                                // Try FullSignatureOverloadMap first, then GlobalOverloadMap with firstParamType
                                var withinFullSig = GetFullParamSig(calledMethod);
                                if (FullSignatureOverloadMap.TryGetValue((typeName, memberName, withinFullSig), out var withinFsResolved))
                                {
                                    resolvedMemberName = withinFsResolved;
                                }
                                else if (TryResolveGlobalOverload(typeName, memberName, args.Count, out var globalResolved, GetFirstParamTypeKey(calledMethod)))
                                {
                                    resolvedMemberName = globalResolved;
                                }
                                else
                                {
                                    var firstParam = calledMethod.Parameters.FirstOrDefault();
                                    if (firstParam != null)
                                    {
                                        var suffix = firstParam.Type.Name;
                                        resolvedMemberName = $"{memberName}_{suffix}";
                                    }
                                }
                            }
                        }
                    }
                }
                {
                    var callResult = $"{luauOwner}:{resolvedMemberName}({argStr})";
                    var hoisted = TryHoistOutParams(callResult, arguments, args);
                    return hoisted ?? callResult;
                }
            }

            // Type reflection method stubs — only for static-looking calls on type names
            // (NOT on params/locals, which are handled above with colon syntax)
            if (memberName is "GetMethods" or "GetConstructors" or "GetProperties"
                or "GetFields" or "GetInterfaces" or "GetGenericArguments" or "GetMember")
                return "{}";
            if (memberName is "GetMethod" or "GetConstructor" or "GetProperty"
                or "GetConverter")
                return "nil";

            // Instance field method calls → colon syntax for instance dispatch
            // e.g., self.Serializer:GetReferenceResolver()
            if (luauOwner.StartsWith("self."))
            {
                // Check for cross-inheritance shadowing/disambiguation on field receivers
                if (_model != null)
                {
                    var symbolInfo = _model.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol fieldCalledMethod && !fieldCalledMethod.IsStatic)
                    {
                        var crossResult = TryResolveCrossInheritanceCall(
                            fieldCalledMethod, memberName, luauOwner, argStr, args.Count, memberAccess.Expression);
                        if (crossResult != null)
                        {
                            var hoisted = TryHoistOutParams(crossResult, arguments, args);
                            return hoisted ?? crossResult;
                        }
                    }
                }
                var callResult2 = $"{luauOwner}:{memberName}({argStr})";
                var hoisted2 = TryHoistOutParams(callResult2, arguments, args);
                return hoisted2 ?? callResult2;
            }

            {
                var callResult = $"{luauOwner}.{memberName}({argStr})";
                var hoisted = TryHoistOutParams(callResult, arguments, args);
                return hoisted ?? callResult;
            }
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
                var hoisted = TryHoistOutParams(mapped, arguments, args);
                return hoisted ?? mapped;
            }

            return $"--[[TODO: {typeStr}.{memberName}]] nil";
        }

        // ── Handle nested member access calls ──
        var left = EmitExpression(memberAccess.Expression);

        // Use semantic model to determine instance vs static method dispatch
        if (_model != null)
        {
            var methodSymbol = _model.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol
                ?? _model.GetSymbolInfo(memberAccess.Parent!).Symbol as IMethodSymbol;
            if (methodSymbol != null && !methodSymbol.IsStatic)
            {
                // Cross-inheritance shadowing/disambiguation check
                var crossResult = TryResolveCrossInheritanceCall(
                    methodSymbol, memberName, left, argStr, args.Count, memberAccess.Expression);
                if (crossResult != null)
                {
                    var hoisted = TryHoistOutParams(crossResult, arguments, args);
                    return hoisted ?? crossResult;
                }

                var callResult2 = $"{left}:{memberName}({argStr})";
                var hoisted2 = TryHoistOutParams(callResult2, arguments, args);
                return hoisted2 ?? callResult2;
            }
            if (methodSymbol != null && methodSymbol.IsStatic)
            {
                var callResult = $"{left}.{memberName}({argStr})";
                var hoisted = TryHoistOutParams(callResult, arguments, args);
                return hoisted ?? callResult;
            }
        }

        // Fallback without semantic model:
        // For method calls on element access or complex expressions (non-type),
        // use colon syntax for instance method dispatch (e.g., arr[i]:Method())
        if (memberAccess.Expression is ElementAccessExpressionSyntax
            || (memberAccess.Expression is MemberAccessExpressionSyntax nestedMA
                && nestedMA.Expression is not PredefinedTypeSyntax
                && !IsLikelyEnumOrExternalType(nestedMA.Expression.ToString())))
        {
            var callResult = $"{left}:{memberName}({argStr})";
            var hoisted = TryHoistOutParams(callResult, arguments, args);
            return hoisted ?? callResult;
        }

        {
            var callResult = $"{left}.{memberName}({argStr})";
            var hoisted = TryHoistOutParams(callResult, arguments, args);
            return hoisted ?? callResult;
        }
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
            // new StringBuilder(capacity) → "" (capacity is just pre-allocation)
            var ctorArgs = objCreate.ArgumentList?.Arguments;
            if (ctorArgs != null && ctorArgs.Value.Count > 0)
            {
                var firstArg = EmitExpression(ctorArgs.Value[0].Expression);
                // Check if argument is numeric (capacity) — StringBuilder(int) allocates but starts empty
                if (_model != null)
                {
                    var argType = _model.GetTypeInfo(ctorArgs.Value[0].Expression).Type;
                    if (argType != null && argType.SpecialType is SpecialType.System_Int32
                        or SpecialType.System_Int64 or SpecialType.System_Int16
                        or SpecialType.System_Byte or SpecialType.System_UInt32)
                        return "\"\"";
                }
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
            || typeName.StartsWith("Stack<") || typeName.StartsWith("ConcurrentDictionary<"))
        {
            // Check for collection initializer
            if (objCreate.Initializer != null)
            {
                // Dictionary initializers: { {key, val}, {key, val} } → {[key] = val, ...}
                bool isDictInit = objCreate.Initializer.Expressions.All(e =>
                    e.Kind() == SyntaxKind.ComplexElementInitializerExpression
                    && ((InitializerExpressionSyntax)e).Expressions.Count == 2);
                if (isDictInit)
                {
                    var entries = objCreate.Initializer.Expressions.Select(e =>
                    {
                        var pair = (InitializerExpressionSyntax)e;
                        var k = EmitExpression(pair.Expressions[0]);
                        var v = EmitExpression(pair.Expressions[1]);
                        return $"[{k}] = {v}";
                    });
                    return $"{{ {string.Join(", ", entries)} }}";
                }
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
            // Detect dictionary initializers: each element is { key, value } (ComplexElementInitializer)
            bool isDictInit = objCreate.Initializer.Expressions.All(e =>
                e.Kind() == SyntaxKind.ComplexElementInitializerExpression
                && ((InitializerExpressionSyntax)e).Expressions.Count == 2);
            if (isDictInit)
            {
                var entries = objCreate.Initializer.Expressions.Select(e =>
                {
                    var pair = (InitializerExpressionSyntax)e;
                    var k = EmitExpression(pair.Expressions[0]);
                    var v = EmitExpression(pair.Expressions[1]);
                    return $"[{k}] = {v}";
                });
                return $"{{ {string.Join(", ", entries)} }}";
            }
            var elements = objCreate.Initializer.Expressions
                .Select(e => EmitExpression(e));
            return $"{{ {string.Join(", ", elements)} }}";
        }

        // ── .NET BCL type constructors ──
        if (cleanType == "KeyValuePair")
        {
            var argList = objCreate.ArgumentList?.Arguments;
            if (argList != null && argList.Value.Count == 2)
            {
                var k = EmitExpression(argList.Value[0].Expression);
                var v = EmitExpression(argList.Value[1].Expression);
                return $"{{ Key = {k}, Value = {v} }}";
            }
            return "{ Key = nil, Value = nil }";
        }
        if (cleanType == "StringWriter")
        {
            NeedsRuntime = true;
            return $"__rt.StringWriter.new({argStr})";
        }
        if (cleanType == "StringReader")
        {
            NeedsRuntime = true;
            return $"__rt.StringReader.new({argStr})";
        }
        if (cleanType == "StringBuilder")
        {
            if (objCreate.ArgumentList?.Arguments.Count > 0)
            {
                var firstArg = objCreate.ArgumentList.Arguments[0].Expression;
                if (_model != null)
                {
                    var typeInfo = _model.GetTypeInfo(firstArg);
                    if (typeInfo.Type?.SpecialType == SpecialType.System_String)
                        return EmitExpression(firstArg);
                }
                // Fallback: if it looks like a string arg, use it
                if (firstArg is LiteralExpressionSyntax { Token.Value: string })
                    return EmitExpression(firstArg);
            }
            return "\"\"";
        }
        if (cleanType is "ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException"
            or "InvalidOperationException" or "NotSupportedException" or "NotImplementedException"
            or "FormatException" or "OverflowException" or "IndexOutOfRangeException")
        {
            // Exception constructors: new XException("message") → just the message string for error()
            // But since these are used in `throw new ...` which becomes `error(...)`,
            // we construct a table so the throw site can use it
            if (objCreate.ArgumentList?.Arguments.Count > 0)
            {
                var msg = EmitExpression(objCreate.ArgumentList.Arguments[0].Expression);
                return $"{{ Message = {msg} }}";
            }
            return $"{{ Message = \"{cleanType}\" }}";
        }
        if (cleanType == "Regex")
        {
            // new Regex(pattern, options?) → { Pattern = pattern, Options = options }
            if (objCreate.ArgumentList?.Arguments.Count >= 1)
            {
                var pattern = EmitExpression(objCreate.ArgumentList.Arguments[0].Expression);
                if (objCreate.ArgumentList.Arguments.Count >= 2)
                {
                    var options = EmitExpression(objCreate.ArgumentList.Arguments[1].Expression);
                    return $"{{ Pattern = {pattern}, Options = {options} }}";
                }
                return $"{{ Pattern = {pattern}, Options = 0 }}";
            }
        }
        if (cleanType == "Uri")
        {
            if (objCreate.ArgumentList?.Arguments.Count >= 1)
                return EmitExpression(objCreate.ArgumentList.Arguments[0].Expression);
        }
        if (cleanType == "Version")
        {
            // new Version(major, minor, ...) → { Major = x, Minor = y, ... }
            if (objCreate.ArgumentList?.Arguments.Count >= 2)
            {
                var fieldNames = new[] { "Major", "Minor", "Build", "Revision" };
                var entries = objCreate.ArgumentList.Arguments
                    .Select((a, i) => $"{(i < fieldNames.Length ? fieldNames[i] : $"_{i}")} = {EmitExpression(a.Expression)}");
                return $"{{ {string.Join(", ", entries)} }}";
            }
        }
        if (cleanType == "Tuple")
        {
            var items = objCreate.ArgumentList?.Arguments.Select(a => EmitExpression(a.Expression)) ?? Enumerable.Empty<string>();
            return $"{{ {string.Join(", ", items)} }}";
        }
        if (cleanType == "CancellationToken")
            return "{ IsCancellationRequested = false }";
        if (cleanType == "DateTime")
        {
            var argList = objCreate.ArgumentList?.Arguments;
            if (argList != null && argList.Value.Count >= 2)
            {
                var ticks = EmitExpression(argList.Value[0].Expression);
                var kind = EmitExpression(argList.Value[1].Expression);
                return $"{{ Ticks = {ticks}, Kind = {kind} }}";
            }
            if (argList != null && argList.Value.Count == 1)
                return $"{{ Ticks = {EmitExpression(argList.Value[0].Expression)}, Kind = 0 }}";
            return "{ Ticks = 0, Kind = 0 }";
        }
        if (cleanType == "DateTimeOffset")
        {
            var argList = objCreate.ArgumentList?.Arguments;
            if (argList != null && argList.Value.Count >= 2)
            {
                var dt = EmitExpression(argList.Value[0].Expression);
                var offset = EmitExpression(argList.Value[1].Expression);
                return $"{{ DateTime = {dt}, Offset = {offset} }}";
            }
            if (argList != null && argList.Value.Count == 1)
                return $"{{ DateTime = {EmitExpression(argList.Value[0].Expression)}, Offset = 0 }}";
            return "{ DateTime = { Ticks = 0, Kind = 0 }, Offset = 0 }";
        }
        if (cleanType == "TimeSpan")
        {
            var argList = objCreate.ArgumentList?.Arguments;
            if (argList != null)
            {
                return argList.Value.Count switch
                {
                    1 => EmitExpression(argList.Value[0].Expression), // TimeSpan(ticks) → ticks
                    3 => $"({EmitExpression(argList.Value[0].Expression)} * 3600 + {EmitExpression(argList.Value[1].Expression)} * 60 + {EmitExpression(argList.Value[2].Expression)})",
                    _ => argStr,
                };
            }
            return "0";
        }
        if (cleanType == "BigInteger" || cleanType == "decimal")
        {
            if (objCreate.ArgumentList?.Arguments.Count >= 1)
                return EmitExpression(objCreate.ArgumentList.Arguments[0].Expression);
            return "0";
        }
        if (cleanType == "Guid")
        {
            if (objCreate.ArgumentList?.Arguments.Count >= 1)
                return EmitExpression(objCreate.ArgumentList.Arguments[0].Expression);
            return "\"00000000-0000-0000-0000-000000000000\"";
        }
        // Collection<T>, ReadOnlyCollection<T> → runtime base class
        if (cleanType is "Collection" or "ReadOnlyCollection")
            return "__rt.Collection.new()";
        if (cleanType is "KeyedCollection")
            return "__rt.KeyedCollection.new()";
        // PropertyDescriptorCollection, NameTable, ConcurrentDictionary → {}
        if (cleanType is "PropertyDescriptorCollection" or "NameTable" or "ConcurrentDictionary")
            return "{}";
        // string(char[], start, length) → __rt.charsToString(chars, start, length)
        // char arrays are stored as number tables, not Luau strings
        if (cleanType == "string")
        {
            var argList = objCreate.ArgumentList?.Arguments;
            if (argList != null && argList.Value.Count == 3)
            {
                var chars = EmitExpression(argList.Value[0].Expression);
                var start = EmitExpression(argList.Value[1].Expression);
                var length = EmitExpression(argList.Value[2].Expression);
                NeedsRuntime = true;
                return $"__rt.charsToString({chars}, {start}, {length})";
            }
            if (argList != null && argList.Value.Count == 2)
            {
                // string(char, count) → string.rep(char, count)
                var ch = EmitExpression(argList.Value[0].Expression);
                var count = EmitExpression(argList.Value[1].Expression);
                return $"string.rep(string.char({ch}), {count})";
            }
            if (argList != null && argList.Value.Count == 1)
            {
                // string(char[]) → table.concat(chars)
                return $"table.concat({EmitExpression(argList.Value[0].Expression)})";
            }
        }
        // UTF8Encoding, TraceEventCache, ExpandoObject → empty table stubs
        if (cleanType is "UTF8Encoding" or "TraceEventCache" or "ExpandoObject")
            return "{}";
        // DynamicMethod → __rt.DynamicMethod.new(...)
        if (cleanType == "DynamicMethod")
            return $"__rt.DynamicMethod.new({argStr})";
        // EventArgs-like types → {} (just data carriers)
        if (cleanType is "NotifyCollectionChangedEventArgs" or "ListChangedEventArgs"
            or "PropertyChangedEventArgs" or "PropertyChangingEventArgs"
            or "AddingNewEventArgs" or "StreamingContext" or "SerializationInfo"
            or "DictionaryEntry" or "FormatterConverter")
            return $"{{ {argStr} }}";

        // Struct default construction: new StructType() with no args
        // In C#, `new StructType()` always creates a zeroed/default instance even when
        // the struct only has parameterized constructors. We must NOT call a parameterized
        // constructor with nil args — instead emit a default table with metatable.
        if (objCreate.ArgumentList == null || objCreate.ArgumentList.Arguments.Count == 0)
        {
            bool isStruct = false;
            bool hasParameterlessCtor = false;
            var fieldDefaults = new List<string>();

            // Try semantic model first
            if (_model != null)
            {
                var typeInfo = _model.GetTypeInfo(objCreate).Type;
                if (typeInfo is INamedTypeSymbol { IsValueType: true, TypeKind: TypeKind.Struct } structSymbol)
                {
                    isStruct = true;
                    hasParameterlessCtor = structSymbol.InstanceConstructors
                        .Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared);
                    if (!hasParameterlessCtor)
                    {
                        foreach (var member in structSymbol.GetMembers())
                        {
                            if (member is IFieldSymbol fs && !fs.IsStatic && !fs.IsConst && fs.AssociatedSymbol == null)
                            {
                                var val = fs.Type.SpecialType switch
                                {
                                    SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single
                                        or SpecialType.System_Double or SpecialType.System_Byte or SpecialType.System_Int16
                                        or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 => "0",
                                    SpecialType.System_Boolean => "false",
                                    SpecialType.System_String => "nil :: any",
                                    _ when fs.Type.TypeKind == TypeKind.Enum => "0",
                                    _ when fs.Type.IsValueType => "0",
                                    _ => "nil :: any"
                                };
                                fieldDefaults.Add($"{fs.Name} = {val}");
                            }
                        }
                    }
                }
            }

            // Fallback: use GlobalStructTypes when semantic model doesn't resolve
            if (!isStruct && GlobalStructTypes.Contains(cleanType))
            {
                isStruct = true;
                // Check if there's a parameterless constructor in the overload map
                hasParameterlessCtor = GlobalOverloadMap.Keys
                    .Any(k => k.TypeName == cleanType && k.MethodName == "new" && k.ArgCount == 0);
            }

            if (isStruct && !hasParameterlessCtor)
            {
                var fields = fieldDefaults.Count > 0 ? string.Join(", ", fieldDefaults) : "";
                if (GlobalStructTypes.Contains(cleanType))
                    return $"setmetatable({{ {fields} }}, {cleanType})";
                return fieldDefaults.Count > 0 ? $"{{ {fields} }}" : "{}";
            }
        }

        // Resolve overloaded constructor name via semantic model
        if (_model != null)
        {
            var ctorSymbol = _model.GetSymbolInfo(objCreate).Symbol as IMethodSymbol;
            if (ctorSymbol != null && ctorSymbol.MethodKind == MethodKind.Constructor)
            {
                var ctorParamCount = objCreate.ArgumentList?.Arguments.Count ?? 0;
                var firstParamType = ctorSymbol.Parameters.FirstOrDefault()?.Type
                    ?.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "";
                var isNullable = firstParamType.EndsWith("?");
                firstParamType = firstParamType.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                    .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullable) firstParamType += "_nullable";

                // Try local overload map (only for constructors of the current class)
                if (cleanType == _currentClassName
                    && _overloadMap.TryGetValue(("new", ctorParamCount, firstParamType), out var localResolved)
                    && localResolved != "new")
                    return $"{cleanType}.{localResolved}({argStr})";

                // Try global overload map
                var globalKey = (cleanType, "new", ctorParamCount, firstParamType);
                if (GlobalOverloadMap.TryGetValue(globalKey, out var globalResolved) && globalResolved != "new")
                    return $"{cleanType}.{globalResolved}({argStr})";
            }
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

        // new Type[size] → table.create(size, default)
        // Must use default value so #table returns the correct length
        // Use appropriate default: 0 for numeric, false for boolean, nil for reference types
        if (arrayCreate.Type.RankSpecifiers.Count > 0)
        {
            var rankSpec = arrayCreate.Type.RankSpecifiers[0];
            if (rankSpec.Sizes.Count > 0 && rankSpec.Sizes[0] is not OmittedArraySizeExpressionSyntax)
            {
                var size = EmitExpression(rankSpec.Sizes[0]);
                var elemType = arrayCreate.Type.ElementType.ToString();
                var defaultVal = elemType switch
                {
                    "bool" or "Boolean" or "boolean" => "false",
                    "string" or "String" => "\"\"",
                    "char" or "byte" or "int" or "long" or "float" or "double" or "decimal"
                        or "short" or "ushort" or "uint" or "ulong" or "sbyte"
                        or "Int32" or "Int64" or "Byte" or "Char" or "Single" or "Double" => "0",
                    _ => (string?)null // reference types default to nil (not 0)
                };
                if (defaultVal == null)
                {
                    // Check semantic model: value types → 0, reference types → nil
                    if (_model != null)
                    {
                        var elemTypeSymbol = _model.GetTypeInfo(arrayCreate.Type.ElementType).Type;
                        if (elemTypeSymbol != null && elemTypeSymbol.IsValueType)
                            defaultVal = "0";
                    }
                }
                return defaultVal != null
                    ? $"table.create({size}, {defaultVal})"
                    : $"table.create({size})";
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

        // If the expression resolved to an empty table literal {} (from stub methods
        // like GetGenericArguments()), indexing it always yields nil and Luau can't
        // parse {}[idx] directly (syntax error).
        if (obj == "{}")
            return "nil";

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

            // IList<T> interface or custom IList implementation indexer — may not be a plain table
            if (receiverType != null)
            {
                bool isIListInterface = receiverType.TypeKind == TypeKind.Interface
                    && receiverType.Name is "IList" or "IReadOnlyList";
                bool implementsIList = !isIListInterface
                    && receiverType.Name is not "List" and not "Array" and not "Queue" and not "Stack"
                    && receiverType is not IArrayTypeSymbol
                    && (receiverType.AllInterfaces.Any(i => i.Name == "IList"));
                if (isIListInterface || implementsIList)
                {
                    NeedsRuntime = true;
                    // Assignment targets need special handling at statement level
                    bool isAssignTarget = elementAccess.Parent is AssignmentExpressionSyntax assignExpr
                        && assignExpr.Left == elementAccess;
                    if (!isAssignTarget)
                        return $"__rt.ilistGet({obj}, {index})";
                    // Fall through for assignment targets — handled in EmitAssignmentStatement
                }
            }

            // char[] access: out-of-bounds returns nil in Luau but \0 in C#
            // Only wrap reads — skip when used as assignment target (LHS)
            if (receiverType is IArrayTypeSymbol arrayType
                && arrayType.ElementType.SpecialType == SpecialType.System_Char)
            {
                bool isAssignTarget = elementAccess.Parent is AssignmentExpressionSyntax assign
                    && assign.Left == elementAccess;
                if (!isAssignTarget)
                    return $"({obj}[{index} + 1] or 0)";
            }
        }
        else if (IsLikelyStringAccess(elementAccess.Expression))
        {
            return $"string.byte({obj}, {index} + 1)";
        }

        // Default: array/list access with 0→1 index adjustment
        return $"{obj}[{index} + 1]";
    }

    /// <summary>
    /// Check if an identifier is an instance field reference in instance context.
    /// Uses both syntax-based tracking and semantic model for partial class / base class fields.
    /// </summary>
    private static bool HasPropertyBody(IPropertySymbol ps)
    {
        var declRef = ps.DeclaringSyntaxReferences.FirstOrDefault();
        if (declRef?.GetSyntax() is PropertyDeclarationSyntax propSyn)
        {
            return propSyn.ExpressionBody != null
                || (propSyn.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) ?? false);
        }
        return false;
    }

    private bool IsInstanceFieldRef(string name, ExpressionSyntax? expr)
    {
        if (!_isInstanceContext || _currentMethodParams.Contains(name) || _currentMethodLocals.Contains(name))
            return false;
        // Syntax-based check (fields declared in this file)
        if (_instanceFields.Contains(name))
            return true;
        // Semantic model check (fields from partial classes, base classes)
        if (_model != null && expr != null)
        {
            var symbol = GetSymbol(expr);
            if (symbol is IFieldSymbol fs && !fs.IsStatic && !fs.IsConst)
                return true;
            if (symbol is IPropertySymbol ps && !ps.IsStatic)
                return true;
        }
        return false;
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
    /// Pending pre-statements generated by compound assignments used in expression position.
    /// Callers like EmitWhile drain this list to hoist assignments before the condition.
    /// </summary>
    private List<string> _pendingAssignmentStatements = new();

    /// <summary>
    /// Handle assignment as an expression (rare in C#, but valid).
    /// Luau has no assignment expressions, so we hoist the assignment to a pending
    /// pre-statement and return the variable name (its post-modification value).
    /// </summary>
    private string EmitAssignmentExpression(AssignmentExpressionSyntax assignment)
    {
        var lhs = EmitExpression(assignment.Left);
        var rhs = EmitExpression(assignment.Right);

        if (assignment.Kind() == SyntaxKind.SimpleAssignmentExpression)
        {
            _pendingAssignmentStatements.Add($"{lhs} = {rhs}");
            return lhs;
        }

        // Compound assignment: +=, -=, *=, /=, %=
        var op = assignment.Kind() switch
        {
            SyntaxKind.SubtractAssignmentExpression => "-=",
            SyntaxKind.AddAssignmentExpression => "+=",
            SyntaxKind.MultiplyAssignmentExpression => "*=",
            SyntaxKind.DivideAssignmentExpression => "/=",
            SyntaxKind.ModuloAssignmentExpression => "%=",
            _ => "="
        };
        _pendingAssignmentStatements.Add($"{lhs} {op} {rhs}");
        return lhs;
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
                || binary.Left is ThisExpressionSyntax
                || (binary.Left is MemberAccessExpressionSyntax coalMa
                    && coalMa.Expression is IdentifierNameSyntax or ThisExpressionSyntax);
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
            // Also try heuristic — catches cases where SemanticModel can't resolve external types
            if (!isTopLevelStringConcat)
            {
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
            SyntaxKind.DivideExpression => null, // handled below for integer division
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

        // Integer division: C# int/int truncates toward zero, Luau / is float division
        if (binary.Kind() == SyntaxKind.DivideExpression)
        {
            bool isIntegerDiv = false;
            if (_model != null)
            {
                var leftType = _model.GetTypeInfo(binary.Left).Type;
                var rightType = _model.GetTypeInfo(binary.Right).Type;
                if (leftType != null && rightType != null
                    && leftType.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
                        or SpecialType.System_Int64 or SpecialType.System_UInt64
                        or SpecialType.System_Int16 or SpecialType.System_UInt16
                        or SpecialType.System_Byte or SpecialType.System_SByte
                    && rightType.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
                        or SpecialType.System_Int64 or SpecialType.System_UInt64
                        or SpecialType.System_Int16 or SpecialType.System_UInt16
                        or SpecialType.System_Byte or SpecialType.System_SByte)
                {
                    isIntegerDiv = true;
                }
            }
            return isIntegerDiv ? $"math.floor({left} / {right})" : $"{left} / {right}";
        }

        // Bitwise & and | with boolean operands → logical and/or (non-short-circuit & on bools)
        if (binary.Kind() is SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression && _model != null)
        {
            var leftType = _model.GetTypeInfo(binary.Left).Type;
            var rightType = _model.GetTypeInfo(binary.Right).Type;
            if (leftType?.SpecialType == SpecialType.System_Boolean || rightType?.SpecialType == SpecialType.System_Boolean)
            {
                var logicalOp = binary.Kind() == SyntaxKind.BitwiseAndExpression ? "and" : "or";
                return $"({left} {logicalOp} {right})";
            }
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
            SyntaxKind.PreIncrementExpression => EmitPreIncrement(operand),
            SyntaxKind.PreDecrementExpression => EmitPreDecrement(operand),
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
        if (postfix.Kind() == SyntaxKind.PostIncrementExpression)
        {
            // Post-increment: use current value, then increment
            // Hoist the increment as a pending statement
            _pendingAssignmentStatements.Add($"{operand} += 1");
            return operand;
        }
        if (postfix.Kind() == SyntaxKind.PostDecrementExpression)
        {
            _pendingAssignmentStatements.Add($"{operand} -= 1");
            return operand;
        }
        return postfix.Kind() switch
        {
            SyntaxKind.SuppressNullableWarningExpression => operand, // just drop the !
            _ => $"--[[TODO: postfix {postfix.Kind()}]] {operand}"
        };
    }

    private string EmitPreIncrement(string operand)
    {
        // Emit the increment immediately so subsequent uses see the updated value
        AppendLine($"{operand} += 1");
        return operand;
    }

    private string EmitPreDecrement(string operand)
    {
        // Emit the decrement immediately so subsequent uses see the updated value
        AppendLine($"{operand} -= 1");
        return operand;
    }

    private string EmitParenthesized(ParenthesizedExpressionSyntax paren)
    {
        // If inner is a cast (erased in Luau), unwrap the redundant parens
        // to avoid "Ambiguous syntax" when result is used as method receiver: (o).Method()
        if (paren.Expression is CastExpressionSyntax)
            return EmitExpression(paren.Expression);
        var inner = EmitExpression(paren.Expression);
        return $"({inner})";
    }

    private string EmitCast(CastExpressionSyntax cast)
    {
        // Cast is erased in Luau — just emit the inner expression.
        // If the inner expression is parenthesized (e.g. ((Array)o) → (o)),
        // unwrap the redundant parens to avoid "Ambiguous syntax" errors
        // when the result is used as a method call receiver like (o).Method().
        if (cast.Expression is ParenthesizedExpressionSyntax innerParen)
            return EmitExpression(innerParen.Expression);
        return EmitExpression(cast.Expression);
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

        // If the subject resolved to the literal nil (e.g., from a stub), the whole
        // conditional access is always nil.  Short-circuit to avoid emitting
        // `nil:Method()` which Luau can't parse inside if-then-else expressions.
        if (target == "nil")
            return "nil";

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
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name.Identifier.Text is "Count" or "Length" =>
                $"#{target}",
            MemberBindingExpressionSyntax memberBinding =>
                $"{target}.{memberBinding.Name.Identifier.Text}",
            InvocationExpressionSyntax invocation when invocation.Expression is MemberBindingExpressionSyntax methodBinding =>
                EmitConditionalMethodInvocation(target, methodBinding, invocation.ArgumentList.Arguments),
            // Handle chained access: ?.Prop.Method(args) — MemberAccess on a MemberBinding
            InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax chainedAccess =>
                ResolveConditionalChainedInvocation(target, chainedAccess, invocation.ArgumentList.Arguments),
            ElementBindingExpressionSyntax elementBinding =>
                $"{target}[{string.Join(", ", elementBinding.ArgumentList.Arguments.Select(a => EmitExpression(a.Expression)))}]",
            _ => $"{target}.{EmitExpression(whenNotNull)}"
        };
    }

    /// <summary>
    /// Resolve a chained invocation inside conditional access: obj?.Prop.Method(args)
    /// Replaces the MemberBindingExpression root with the target expression.
    /// </summary>
    private string ResolveConditionalChainedInvocation(string target, MemberAccessExpressionSyntax memberAccess, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        // Build the full owner by walking up the chain and replacing MemberBinding with target
        var owner = ResolveConditionalChain(target, memberAccess.Expression);
        var methodName = memberAccess.Name.Identifier.Text;
        var args = arguments.Select(a => EmitExpression(a.Expression));
        var argStr = string.Join(", ", args);

        // Apply known method rewrites (Add → table.insert, etc.)
        if (methodName == "Add" && arguments.Count == 1)
            return $"table.insert({owner}, {argStr})";
        if (methodName == "Add" && arguments.Count == 2)
        {
            var key = EmitExpression(arguments[0].Expression);
            var val = EmitExpression(arguments[1].Expression);
            return $"{owner}[{key}] = {val}";
        }

        return $"{owner}:{methodName}({argStr})";
    }

    /// <summary>
    /// Walk a member access chain inside conditional access and replace the MemberBinding root with target.
    /// e.g., .Expressions → target.Expressions
    /// </summary>
    private string ResolveConditionalChain(string target, ExpressionSyntax expr)
    {
        if (expr is MemberBindingExpressionSyntax binding)
            return $"{target}.{binding.Name.Identifier.Text}";
        if (expr is MemberAccessExpressionSyntax access)
            return $"{ResolveConditionalChain(target, access.Expression)}.{access.Name.Identifier.Text}";
        return EmitExpression(expr);
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

        // ?.ToString() → tostring(target)
        if (methodName == "ToString" && arguments.Count == 0)
            return $"tostring({target})";

        // ?.GetType() → typeof(target)
        if (methodName == "GetType" && arguments.Count == 0)
            return $"typeof({target})";

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
    /// Returns true if the given type name is a .NET framework base class
    /// that won't have a transpiled Luau module (e.g. Attribute, Exception, EventArgs).
    /// For these, we emit {} instead of BaseType.new() in constructors.
    /// </summary>
    private static bool IsExceptionBaseClass(string name)
    {
        return name is "Exception" or "SystemException" or "ApplicationException"
            or "ArgumentException" or "InvalidOperationException" or "FormatException"
            or "NotSupportedException" or "NotImplementedException" or "IOException"
            or "JsonException" or "JsonReaderException" or "JsonWriterException"
            or "JsonSerializationException";
    }

    private static bool IsNetFrameworkBaseClass(string name)
    {
        return name is "Attribute" or "Exception" or "EventArgs"
            or "IDisposable" or "IAsyncDisposable" or "IComparable" or "IEquatable"
            or "IFormattable" or "IConvertible" or "ICloneable"
            or "MarshalByRefObject" or "ValueType"
            or "ICollection" or "IList" or "IEnumerable" or "IDictionary"
            or "ISerializable" or "IDeserializationCallback"
            or "IFormatterConverter" or "IEqualityComparer"
            or "Collection" or "ReadOnlyCollection" or "KeyedCollection"
            or "SerializationBinder" or "PropertyDescriptor"
            or "DynamicMetaObject" or "GetMemberBinder" or "SetMemberBinder"
            or "ExpressionVisitor" or "IXmlNode";
    }

    /// <summary>
    /// Map a BCL type name to its Luau runtime reference (e.g., "Collection" → "__rt.Collection").
    /// Returns the input unchanged if not a known runtime BCL type.
    /// </summary>
    private static string GetRuntimeRef(string typeName)
    {
        if (typeName is "Collection" or "ReadOnlyCollection")
            return "__rt.Collection";
        if (typeName is "KeyedCollection")
            return "__rt.KeyedCollection";
        return typeName;
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
        NeedsRuntime = true;
        return typeName switch
        {
            "string" => $"(type({subject}) == \"string\")",
            "int" or "float" or "double" or "long" or "decimal" or "number" =>
                $"(type({subject}) == \"number\")",
            "bool" or "boolean" => $"(type({subject}) == \"boolean\")",
            _ => $"(__rt.isinstance({subject}, \"{typeName}\"))"
        };
    }

    // ────────────────────────────────────────────────────────────────────
    //  await expression: await expr → direct call or task.wait
    // ────────────────────────────────────────────────────────────────────

    private string EmitTypeOf(TypeOfExpressionSyntax typeOf)
    {
        var typeName = typeOf.Type.ToString();
        // If we have a semantic model, check the type kind
        if (_model != null)
        {
            var typeInfo = _model.GetTypeInfo(typeOf.Type);
            var typeSymbol = typeInfo.Type;
            if (typeSymbol?.TypeKind == TypeKind.Enum)
            {
                // Emit the actual enum table reference so reflection helpers can iterate it
                var cleanName = typeName.Replace(",", "").Replace(" ", "").Replace("<", "_").Replace(">", "");
                ReferencedModules.Add(cleanName);
                return cleanName;
            }
            // Generic type parameter (T, TValue, etc.) — can't resolve at transpile time
            if (typeSymbol?.TypeKind == TypeKind.TypeParameter)
                return "nil";
        }
        // Default: emit as string
        return $"\"{typeName}\"";
    }

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

    /// <summary>
    /// Handle out-parameter hoisting for method calls like TryGetValue, TryParse.
    /// When a method call has out arguments and the runtime returns (bool, outVal),
    /// hoist the call as: local __tryOk, outVar = call(...)
    /// Returns the temp bool variable name, or null if no out args found.
    /// </summary>
    private string? TryHoistOutParams(string callResult, SeparatedSyntaxList<ArgumentSyntax> arguments, List<string> emittedArgs)
    {
        var outArgIndices = new List<int>();
        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
                outArgIndices.Add(i);
        }
        if (outArgIndices.Count == 0) return null;

        var outVarNames = outArgIndices.Select(i => emittedArgs[i]).ToList();
        var tempBool = $"__tryOk_{_hoistCounter++}";

        bool allDeclarations = outArgIndices.All(i =>
            arguments[i].Expression is DeclarationExpressionSyntax);

        if (allDeclarations)
        {
            var declList = string.Join(", ", new[] { tempBool }.Concat(outVarNames));
            AppendLine($"local {declList} = {callResult}");
        }
        else
        {
            AppendLine($"local {tempBool}");
            var assignList = string.Join(", ", new[] { tempBool }.Concat(outVarNames));
            AppendLine($"{assignList} = {callResult}");
        }
        return tempBool;
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
