using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpRoslynModule.Transpiler;

public class RoslynToLuau
{
    /// <summary>
    /// Global overload registry: (TypeName, MethodName, ArgCount) → DisambiguatedName.
    /// Pre-populated by PreScan() so cross-type calls resolve overloaded names correctly.
    /// </summary>
    private Dictionary<(string TypeName, string MethodName, int ArgCount), string> _globalOverloadMap = new();

    /// <summary>
    /// Maps each type name to the module file name (without extension) that contains it.
    /// For example, if SyntaxNode.cs defines SyntaxNode, ExpressionSyntax, StatementSyntax,
    /// this maps all three to "SyntaxNode".
    /// Built during PreScan, used by InsertRequires to generate correct require() paths.
    /// </summary>
    private Dictionary<string, string> _typeToModuleMap = new();

    /// <summary>
    /// Roslyn CSharpCompilation built from all source files + BCL references.
    /// Provides SemanticModel per-file for type resolution.
    /// </summary>
    private CSharpCompilation? _compilation;

    /// <summary>
    /// All syntax trees collected during PreScan, keyed by file name.
    /// Used to retrieve the correct tree for SemanticModel lookup in Transpile().
    /// </summary>
    private Dictionary<string, SyntaxTree> _syntaxTrees = new();

    private static List<MetadataReference> GetBclReferences()
    {
        var refs = new List<MetadataReference>();
        var trustedDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var coreLibs = new[]
        {
            "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll",
            "System.Threading.Tasks.dll", "System.Console.dll", "System.Text.RegularExpressions.dll",
            "System.Memory.dll", "System.Collections.Concurrent.dll", "System.Collections.Immutable.dll",
            "System.ComponentModel.dll", "System.ComponentModel.Primitives.dll",
            "System.ObjectModel.dll", "System.Globalization.dll", "System.IO.dll",
            "System.Text.Encoding.dll", "System.Threading.dll", "System.Numerics.Vectors.dll",
            "System.Runtime.Numerics.dll", "System.Runtime.Extensions.dll",
            "System.Private.CoreLib.dll", "netstandard.dll",
        };
        foreach (var lib in coreLibs)
        {
            var path = Path.Combine(trustedDir, lib);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }
        return refs;
    }

    /// <summary>
    /// Pre-scan source files to build a global overload map before emitting any code.
    /// Call this once with all source files before calling Transpile for each file.
    /// </summary>
    public void PreScan(IEnumerable<(string SourceCode, string FileName)> sourceFiles)
    {
        var allTrees = new List<SyntaxTree>();

        foreach (var (sourceCode, fileName) in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
            var root = tree.GetCompilationUnitRoot();

            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (diagnostics.Count > 0) continue;

            allTrees.Add(tree);
            _syntaxTrees[fileName] = tree;

            ScanOverloads(root.Members);

            // Build type-to-module mapping: each type defined in this file maps to the module name
            // This includes nested types (e.g., SimpleTokenizer.TokenInfo → SimpleTokenizer module)
            var moduleName = Path.GetFileNameWithoutExtension(fileName);
            var typeNames = CollectAllTypeNames(root.Members);
            foreach (var typeName in typeNames)
            {
                _typeToModuleMap[typeName] = moduleName;
            }
        }

        // Build CSharpCompilation from all valid trees + BCL references
        if (allTrees.Count > 0)
        {
            _compilation = CSharpCompilation.Create(
                "LUSharpTranspilation",
                syntaxTrees: allTrees,
                references: GetBclReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable)
            );
        }
    }

    private void ScanOverloads(SyntaxList<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    ScanOverloads(ns.Members);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    ScanOverloads(ns.Members);
                    break;
                case ClassDeclarationSyntax classDecl:
                    ScanTypeOverloads(classDecl.Identifier.Text, classDecl.Members);
                    break;
                case StructDeclarationSyntax structDecl:
                    ScanTypeOverloads(structDecl.Identifier.Text, structDecl.Members);
                    break;
            }
        }
    }

    private void ScanTypeOverloads(string typeName, SyntaxList<MemberDeclarationSyntax> members)
    {
        // Count method name occurrences
        var methodNameCounts = new Dictionary<string, int>();
        var methodNameSeen = new Dictionary<string, int>();

        foreach (var member in members)
        {
            if (member is MethodDeclarationSyntax m)
            {
                var name = m.Identifier.Text;
                methodNameCounts[name] = methodNameCounts.GetValueOrDefault(name) + 1;
            }
            // Recurse into nested types
            else if (member is ClassDeclarationSyntax nested)
            {
                ScanTypeOverloads(nested.Identifier.Text, nested.Members);
            }
            else if (member is StructDeclarationSyntax nestedStruct)
            {
                ScanTypeOverloads(nestedStruct.Identifier.Text, nestedStruct.Members);
            }
        }

        foreach (var member in members)
        {
            if (member is not MethodDeclarationSyntax method) continue;

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

            _globalOverloadMap[(typeName, name, paramCount)] = emitName;
        }
    }

    public TranspileResult Transpile(string sourceCode, string fileName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
        var root = tree.GetCompilationUnitRoot();

        var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (diagnostics.Count > 0)
        {
            return new TranspileResult
            {
                Success = false,
                FileName = fileName,
                Errors = diagnostics.Select(d => d.ToString()).ToList()
            };
        }

        // Get SemanticModel from compilation if available
        SemanticModel? model = null;
        if (_compilation != null)
        {
            // Use the pre-scanned tree if available (same SyntaxTree instance needed for SemanticModel)
            if (_syntaxTrees.TryGetValue(fileName, out var preScanTree))
            {
                model = _compilation.GetSemanticModel(preScanTree);
                tree = preScanTree; // Use the same tree instance
                root = tree.GetCompilationUnitRoot();
            }
            else
            {
                // File wasn't in PreScan — add it to compilation on the fly
                _compilation = _compilation.AddSyntaxTrees(tree);
                model = _compilation.GetSemanticModel(tree);
            }
        }

        var emitter = new LuauEmitter(model);
        emitter.GlobalOverloadMap = _globalOverloadMap;
        emitter.EmitHeader();

        // Count top-level type declarations to decide whether to suppress individual returns
        var topLevelTypeCount = CountTopLevelTypes(root.Members);
        emitter.SuppressReturn = topLevelTypeCount > 1;

        foreach (var member in root.Members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                case ClassDeclarationSyntax classDecl:
                    emitter.EmitClass(classDecl);
                    break;
                case StructDeclarationSyntax structDecl:
                    emitter.EmitStruct(structDecl);
                    break;
                default:
                    Console.Error.WriteLine($"Warning: unsupported top-level declaration: {member.Kind()}");
                    break;
            }
        }

        // Emit unified return for multi-type files
        if (emitter.SuppressReturn)
        {
            emitter.EmitUnifiedReturn();
        }

        // Collect type names defined in this file to exclude from requires
        var localTypeNames = CollectTypeNames(root.Members);

        // Post-process: insert require() statements for referenced modules
        InsertRequires(emitter, localTypeNames);

        return new TranspileResult
        {
            Success = true,
            FileName = fileName,
            LuauSource = emitter.GetOutput()
        };
    }

    private void EmitNamespaceMembers(SyntaxList<MemberDeclarationSyntax> members, LuauEmitter emitter)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                case ClassDeclarationSyntax classDecl:
                    emitter.EmitClass(classDecl);
                    break;
                case StructDeclarationSyntax structDecl:
                    emitter.EmitStruct(structDecl);
                    break;
                default:
                    Console.Error.WriteLine($"Warning: unsupported member: {member.Kind()}");
                    break;
            }
        }
    }

    /// <summary>
    /// Count the number of top-level type declarations (classes, structs, enums) in a file.
    /// Used to determine whether to suppress individual return statements.
    /// </summary>
    private int CountTopLevelTypes(SyntaxList<MemberDeclarationSyntax> members)
    {
        int count = 0;
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    count += CountTopLevelTypes(ns.Members);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    count += CountTopLevelTypes(ns.Members);
                    break;
                case ClassDeclarationSyntax:
                case StructDeclarationSyntax:
                case EnumDeclarationSyntax:
                    count++;
                    break;
            }
        }
        return count;
    }

    /// <summary>
    /// Collect all type names defined in a set of members (classes, structs, enums).
    /// Used to exclude same-file types from require() statements.
    /// Does NOT recurse into nested types (only top-level within the namespace).
    /// </summary>
    private HashSet<string> CollectTypeNames(SyntaxList<MemberDeclarationSyntax> members)
    {
        var names = new HashSet<string>();
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    names.UnionWith(CollectTypeNames(ns.Members));
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    names.UnionWith(CollectTypeNames(ns.Members));
                    break;
                case ClassDeclarationSyntax classDecl:
                    names.Add(classDecl.Identifier.Text);
                    break;
                case StructDeclarationSyntax structDecl:
                    names.Add(structDecl.Identifier.Text);
                    break;
                case EnumDeclarationSyntax enumDecl:
                    names.Add(enumDecl.Identifier.Text);
                    break;
            }
        }
        return names;
    }

    /// <summary>
    /// Collect ALL type names including nested types (recursing into class/struct members).
    /// Used for the type-to-module map during PreScan, so nested types like TokenInfo
    /// inside SimpleTokenizer correctly map to the SimpleTokenizer module.
    /// </summary>
    private HashSet<string> CollectAllTypeNames(SyntaxList<MemberDeclarationSyntax> members)
    {
        var names = new HashSet<string>();
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    names.UnionWith(CollectAllTypeNames(ns.Members));
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    names.UnionWith(CollectAllTypeNames(ns.Members));
                    break;
                case ClassDeclarationSyntax classDecl:
                    names.Add(classDecl.Identifier.Text);
                    // Recurse into nested types
                    names.UnionWith(CollectAllTypeNames(classDecl.Members));
                    break;
                case StructDeclarationSyntax structDecl:
                    names.Add(structDecl.Identifier.Text);
                    names.UnionWith(CollectAllTypeNames(structDecl.Members));
                    break;
                case EnumDeclarationSyntax enumDecl:
                    names.Add(enumDecl.Identifier.Text);
                    break;
            }
        }
        return names;
    }

    /// <summary>
    /// After emission, insert require() statements for any external modules referenced.
    /// Uses the type-to-module map (built during PreScan) to resolve the correct module
    /// file for each referenced type. Groups types by module to avoid duplicate requires.
    /// Pattern:
    ///   local _ModuleName = require(script.Parent.ModuleName)
    ///   local TypeA = _ModuleName.TypeA
    ///   local TypeB = _ModuleName.TypeB
    /// Excludes types defined in the same file (localTypeNames).
    /// </summary>
    private void InsertRequires(LuauEmitter emitter, HashSet<string>? localTypeNames = null)
    {
        if (emitter.ReferencedModules.Count == 0) return;

        // Filter out modules that are known not to need requires or are defined in the same file
        var typesToRequire = emitter.ReferencedModules
            .Where(m => !IsIgnoredModule(m))
            .Where(m => localTypeNames == null || !localTypeNames.Contains(m))
            .OrderBy(m => m)
            .ToList();

        if (typesToRequire.Count == 0) return;

        // Group types by their source module (using the type-to-module map)
        var moduleToTypes = new Dictionary<string, List<string>>();
        foreach (var typeName in typesToRequire)
        {
            // Look up which module file this type is defined in
            var moduleName = _typeToModuleMap.GetValueOrDefault(typeName, typeName);
            if (!moduleToTypes.ContainsKey(moduleName))
                moduleToTypes[moduleName] = new List<string>();
            moduleToTypes[moduleName].Add(typeName);
        }

        var requireBlock = new System.Text.StringBuilder();
        foreach (var (moduleName, types) in moduleToTypes.OrderBy(kv => kv.Key))
        {
            requireBlock.AppendLine($"local _{moduleName} = require(script.Parent.{moduleName})");
            foreach (var typeName in types.OrderBy(t => t))
            {
                requireBlock.AppendLine($"local {typeName} = _{moduleName}.{typeName}");
            }
        }
        requireBlock.AppendLine();

        emitter.InsertAfterHeader(requireBlock.ToString());
    }

    /// <summary>
    /// Modules that should not get a require() statement
    /// (e.g., CharUnicodeInfo — external .NET, not a Luau module).
    /// </summary>
    private static bool IsIgnoredModule(string module)
    {
        return module is "CharUnicodeInfo" or "UnicodeCategory"
            or "ArgumentOutOfRangeException" or "Math" or "Console"
            or "Char" or "String" or "Int32" or "Convert";
    }
}

public class TranspileResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = "";
    public string LuauSource { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}
