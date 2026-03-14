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
    private Dictionary<(string TypeName, string MethodName, int ArgCount, string FirstParamType), string> _globalOverloadMap = new();

    /// <summary>
    /// Full-signature overload map: (TypeName, MethodName, AllParamTypes) → EmitName.
    /// Used when the simple 4-tuple key is ambiguous (e.g., same first param type + count).
    /// </summary>
    private Dictionary<(string TypeName, string MethodName, string AllParamTypes), string> _fullSignatureMap = new();

    /// <summary>
    /// Maps each type name to the module file name (without extension) that contains it.
    /// For example, if SyntaxNode.cs defines SyntaxNode, ExpressionSyntax, StatementSyntax,
    /// this maps all three to "SyntaxNode".
    /// Built during PreScan, used by InsertRequires to generate correct require() paths.
    /// </summary>
    private Dictionary<string, string> _typeToModuleMap = new();
    /// <summary>
    /// When multiple modules export the same type name, track all alternatives.
    /// </summary>
    private Dictionary<string, List<string>> _typeToModuleCollisions = new();

    /// <summary>
    /// Maps class name → base class name, collected across all files during PreScan.
    /// Handles partial classes where base class is declared in one file but used in another.
    /// </summary>
    private Dictionary<string, string> _baseClassMap = new();

    /// <summary>
    /// Set of struct type names (value types). Used to detect default struct construction
    /// (new StructType()) which zero-initializes rather than calling .new().
    /// </summary>
    private HashSet<string> _structTypes = new();

    /// <summary>
    /// Types known to have a ToString() method. Shared across emitters for __tostring propagation.
    /// </summary>
    private HashSet<string> _typesWithToString = new();

    /// <summary>
    /// Set of (FromModule, ToModule) edges that should use lazy requires to break cycles.
    /// Built by DetectCycles() after PreScan + a quick dependency scan pass.
    /// </summary>
    private HashSet<(string From, string To)> _lazyRequireEdges = new();

    /// <summary>
    /// Per-module dependency map: module name → set of modules it depends on.
    /// Built during TranspileProject-level processing for cycle detection.
    /// </summary>
    private Dictionary<string, HashSet<string>> _moduleDeps = new();

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

    /// <summary>
    /// Preprocessor symbols to define when parsing (e.g., HAVE_ASYNC, HAVE_LINQ).
    /// Set before calling PreScan() to enable conditional compilation blocks.
    /// </summary>
    public List<string> PreprocessorSymbols { get; set; } = new();

    private CSharpParseOptions GetParseOptions()
    {
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        if (PreprocessorSymbols.Count > 0)
            options = options.WithPreprocessorSymbols(PreprocessorSymbols);
        return options;
    }

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
            var tree = CSharpSyntaxTree.ParseText(sourceCode, GetParseOptions(), path: fileName);
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
                if (_typeToModuleMap.ContainsKey(typeName) && _typeToModuleMap[typeName] != moduleName)
                {
                    // Name collision — track all modules that export this type
                    if (!_typeToModuleCollisions.ContainsKey(typeName))
                        _typeToModuleCollisions[typeName] = new List<string> { _typeToModuleMap[typeName] };
                    _typeToModuleCollisions[typeName].Add(moduleName);
                }
                _typeToModuleMap[typeName] = moduleName;
            }

            // Collect base class info (handles partial classes across files)
            ScanBaseClasses(root.Members);
        }

        // Propagate ToString through inheritance: if a type has ToString, all descendants get it too
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (child, parent) in _baseClassMap)
            {
                if (!_typesWithToString.Contains(child) && _typesWithToString.Contains(parent))
                {
                    _typesWithToString.Add(child);
                    changed = true;
                }
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

        // Fix override method names in global overload map now that we have semantic info
        FixOverrideNames();

        // Build dependency graph and detect cycles upfront
        DetectCycles();
    }

    /// <summary>
    /// Post-scan pass: for override methods, ensure the GlobalOverloadMap entry uses
    /// the same disambiguated name as the base class's original declaration.
    /// This prevents naming mismatches like JObject.WriteTo vs JToken.WriteTo_JsonWriter.
    /// Must run after _compilation is built.
    /// </summary>
    private void FixOverrideNames()
    {
        if (_compilation == null) return;

        foreach (var (fileName, tree) in _syntaxTrees)
        {
            var model = _compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;

                var symbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
                if (symbol?.OverriddenMethod == null) continue;

                // Walk up the override chain to find the original declaration
                var baseMethod = symbol.OverriddenMethod;
                while (baseMethod.OverriddenMethod != null)
                    baseMethod = baseMethod.OverriddenMethod;

                var baseTypeName = baseMethod.ContainingType?.Name;
                var currentTypeName = symbol.ContainingType?.Name;
                if (baseTypeName == null || currentTypeName == null || currentTypeName == baseTypeName) continue;

                // Compute the base method's firstParamType key from the semantic model
                var baseFpt = "";
                if (baseMethod.Parameters.Length > 0)
                {
                    baseFpt = baseMethod.Parameters[0].Type.ToDisplayString(
                        Microsoft.CodeAnalysis.SymbolDisplayFormat.MinimallyQualifiedFormat);
                    var isNullableBase = baseFpt.EndsWith("?")
                        || (baseMethod.Parameters[0].Type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated });
                    baseFpt = baseFpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                        .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                    if (isNullableBase) baseFpt += "_nullable";
                }

                // Look up the exact base entry
                var baseKey = (baseTypeName, baseMethod.Name, baseMethod.Parameters.Length, baseFpt);
                if (!_globalOverloadMap.TryGetValue(baseKey, out var baseEmitName))
                    continue;

                // Update the current type's GlobalOverloadMap entry to match
                var curFpt = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
                var isNullableCur = curFpt.EndsWith("?");
                curFpt = curFpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                    .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullableCur) curFpt += "_nullable";
                var curKey = (currentTypeName, method.Identifier.Text, method.ParameterList.Parameters.Count, curFpt);
                // Only update if the override's current emit name differs from the base
                if (_globalOverloadMap.TryGetValue(curKey, out var curEmitName))
                {
                    if (curEmitName != baseEmitName)
                    {
                        _globalOverloadMap[curKey] = baseEmitName;
                        // Also update full-signature map
                        var allParamTypes = string.Join(",", method.ParameterList.Parameters.Select(p =>
                        {
                            var pt = p.Type?.ToString() ?? "";
                            var isNullable = pt.EndsWith("?");
                            pt = pt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                                .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                            if (isNullable) pt += "_nullable";
                            return pt;
                        }));
                        _fullSignatureMap[(currentTypeName, method.Identifier.Text, allParamTypes)] = baseEmitName;
                    }
                }
                else
                {
                    _globalOverloadMap[curKey] = baseEmitName;
                }
            }
        }
    }

    /// <summary>
    /// Builds the full module dependency graph from identifier references in all source files,
    /// then detects cycles via DFS and selects edges to break with lazy requires.
    /// Must be called after PreScan populates _typeToModuleMap and _syntaxTrees.
    /// </summary>
    private void DetectCycles()
    {
        // Build dependency graph: for each module, find which other modules it references
        var depGraph = new Dictionary<string, HashSet<string>>();

        foreach (var (fileName, tree) in _syntaxTrees)
        {
            var moduleName = Path.GetFileNameWithoutExtension(fileName);
            var deps = new HashSet<string>();

            // Walk all identifier names in the syntax tree
            var root = tree.GetCompilationUnitRoot();
            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var name = identifier.Identifier.Text;
                if (_typeToModuleMap.TryGetValue(name, out var depModule) && depModule != moduleName)
                {
                    deps.Add(depModule);
                }
            }

            depGraph[moduleName] = deps;
        }

        // Store in _moduleDeps for use by InsertRequires
        _moduleDeps = depGraph;

        // Build module-level inheritance map: childModule → baseModule
        var moduleInheritance = new Dictionary<string, string>();
        foreach (var (childType, baseType) in _baseClassMap)
        {
            if (_typeToModuleMap.TryGetValue(childType, out var childMod) &&
                _typeToModuleMap.TryGetValue(baseType, out var baseMod) &&
                childMod != baseMod)
            {
                moduleInheritance[childMod] = baseMod;
            }
        }

        // Build set of inheritance edges that MUST remain non-deferred
        var inheritanceEdges = new HashSet<(string, string)>();
        foreach (var (child, baseMod) in moduleInheritance)
        {
            if (depGraph.ContainsKey(child) && depGraph[child].Contains(baseMod))
                inheritanceEdges.Add((child, baseMod));
        }

        // Use Tarjan's SCC to find all strongly connected components,
        // then break cycles within each SCC by deferring edges.
        // This is O(V+E) and handles all cycles in one pass.
        var lazyEdges = new HashSet<(string, string)>();

        // Tarjan's SCC
        var index = 0;
        var stack = new List<string>();
        var onStack = new HashSet<string>();
        var indices = new Dictionary<string, int>();
        var lowlinks = new Dictionary<string, int>();
        var sccs = new List<List<string>>();

        void Strongconnect(string v)
        {
            indices[v] = index;
            lowlinks[v] = index;
            index++;
            stack.Add(v);
            onStack.Add(v);

            if (depGraph.TryGetValue(v, out var neighbors))
            {
                foreach (var w in neighbors)
                {
                    if (!depGraph.ContainsKey(w)) continue; // skip external modules
                    if (!indices.ContainsKey(w))
                    {
                        Strongconnect(w);
                        lowlinks[v] = Math.Min(lowlinks[v], lowlinks[w]);
                    }
                    else if (onStack.Contains(w))
                    {
                        lowlinks[v] = Math.Min(lowlinks[v], indices[w]);
                    }
                }
            }

            if (lowlinks[v] == indices[v])
            {
                var scc = new List<string>();
                while (true)
                {
                    var w = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    onStack.Remove(w);
                    scc.Add(w);
                    if (w == v) break;
                }
                if (scc.Count > 1)
                    sccs.Add(scc);
            }
        }

        foreach (var module in depGraph.Keys.OrderBy(k => k))
        {
            if (!indices.ContainsKey(module))
                Strongconnect(module);
        }

        // For each SCC, find the minimum set of back edges to defer using DFS.
        // Prefer deferring non-inheritance edges; only defer inheritance edges as last resort.
        foreach (var scc in sccs)
        {
            var sccSet = new HashSet<string>(scc);

            // Build the SCC-internal subgraph
            var sccGraph = new Dictionary<string, HashSet<string>>();
            var internalEdgeCount = 0;
            foreach (var mod in scc)
            {
                var sccDeps = new HashSet<string>();
                if (depGraph.TryGetValue(mod, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        if (sccSet.Contains(dep))
                        {
                            sccDeps.Add(dep);
                            internalEdgeCount++;
                        }
                    }
                }
                sccGraph[mod] = sccDeps;
            }

            // DFS on the SCC subgraph to find back edges (these are the edges to defer).
            // We use iterative DFS to avoid stack overflow on large SCCs.
            var sccWhite = new HashSet<string>(scc);
            var sccGray = new HashSet<string>();
            var sccBlack = new HashSet<string>();
            var backEdges = new List<(string From, string To)>();

            // Use explicit stack for iterative DFS
            var dfsStack = new Stack<(string Node, IEnumerator<string> Neighbors)>();

            foreach (var startNode in scc.OrderBy(k => k))
            {
                if (!sccWhite.Contains(startNode)) continue;

                sccWhite.Remove(startNode);
                sccGray.Add(startNode);
                dfsStack.Push((startNode, sccGraph[startNode].GetEnumerator()));

                while (dfsStack.Count > 0)
                {
                    var (node, neighbors) = dfsStack.Peek();
                    var advanced = false;

                    while (neighbors.MoveNext())
                    {
                        var dep = neighbors.Current;
                        if (lazyEdges.Contains((node, dep))) continue; // already deferred

                        if (sccGray.Contains(dep))
                        {
                            // Back edge found — this creates a cycle
                            backEdges.Add((node, dep));
                        }
                        else if (sccWhite.Contains(dep))
                        {
                            sccWhite.Remove(dep);
                            sccGray.Add(dep);
                            dfsStack.Push((dep, sccGraph[dep].GetEnumerator()));
                            advanced = true;
                            break;
                        }
                        // If black, it's a cross/forward edge — ignore
                    }

                    if (!advanced)
                    {
                        dfsStack.Pop();
                        sccGray.Remove(node);
                        sccBlack.Add(node);
                    }
                }
            }

            // Defer back edges, preferring non-inheritance edges.
            // For inheritance back edges, try to defer a different edge in the cycle instead.
            var deferredCount = 0;
            var inheritanceDeferred = 0;
            foreach (var (from, to) in backEdges)
            {
                if (inheritanceEdges.Contains((from, to)))
                {
                    // This is an inheritance edge — try to flip: defer (to, from) if that edge exists
                    if (sccGraph.ContainsKey(to) && sccGraph[to].Contains(from) && !inheritanceEdges.Contains((to, from)))
                    {
                        lazyEdges.Add((to, from));
                        deferredCount++;
                    }
                    else
                    {
                        // No good alternative — defer the inheritance edge temporarily (post-pass will un-defer)
                        lazyEdges.Add((from, to));
                        deferredCount++;
                        inheritanceDeferred++;
                    }
                }
                else
                {
                    lazyEdges.Add((from, to));
                    deferredCount++;
                }
            }

            if (internalEdgeCount > 0)
            {
                Console.Error.WriteLine($"  SCC ({scc.Count} modules): {string.Join(", ", scc.Take(8))}{(scc.Count > 8 ? "..." : "")}");
                Console.Error.WriteLine($"    Edges: {internalEdgeCount} total, {backEdges.Count} back edges found, {deferredCount} deferred");
            }
        }

        // Post-process: un-defer any inheritance edges that got deferred in the SCC pass.
        // The fallback DFS below will find alternative edges to break the reintroduced cycles.
        var undeferredInheritance = new List<(string, string)>();
        foreach (var edge in lazyEdges.ToList())
        {
            if (inheritanceEdges.Contains(edge))
            {
                lazyEdges.Remove(edge);
                undeferredInheritance.Add(edge);
            }
        }
        if (undeferredInheritance.Count > 0)
        {
            Console.Error.WriteLine($"  Un-deferred {undeferredInheritance.Count} inheritance edges for fallback resolution");
        }

        // Verify: check remaining graph is acyclic
        var remainingCycles = false;
        {
            var visited = new HashSet<string>();
            var inProgress = new HashSet<string>();

            bool HasCycle(string node)
            {
                if (inProgress.Contains(node)) return true;
                if (visited.Contains(node)) return false;
                inProgress.Add(node);

                if (depGraph.TryGetValue(node, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        if (!depGraph.ContainsKey(dep)) continue;
                        if (lazyEdges.Contains((node, dep))) continue;
                        if (HasCycle(dep)) return true;
                    }
                }

                inProgress.Remove(node);
                visited.Add(node);
                return false;
            }

            foreach (var mod in depGraph.Keys)
            {
                if (HasCycle(mod))
                {
                    remainingCycles = true;
                    break;
                }
            }
        }

        if (remainingCycles)
        {
            Console.Error.WriteLine("  WARNING: Remaining cycles detected after SCC-based edge breaking.");
            Console.Error.WriteLine("  Falling back to iterative DFS to break remaining cycles.");

            // Iterative fallback: keep breaking one back edge at a time
            // Track the DFS path so we can find the cycle and pick a non-inheritance edge to defer
            for (int iteration = 0; iteration < 500; iteration++)
            {
                var white = new HashSet<string>(depGraph.Keys);
                var gray = new HashSet<string>();
                var dfsPath = new List<string>();
                (string From, string To)? foundBackEdge = null;

                void FallbackDfs(string node)
                {
                    if (foundBackEdge != null) return;
                    white.Remove(node);
                    gray.Add(node);
                    dfsPath.Add(node);

                    if (depGraph.TryGetValue(node, out var neighbors))
                    {
                        foreach (var dep in neighbors)
                        {
                            if (foundBackEdge != null) return;
                            if (lazyEdges.Contains((node, dep))) continue;
                            if (!depGraph.ContainsKey(dep)) continue;
                            if (gray.Contains(dep))
                            {
                                foundBackEdge = (node, dep);
                                return;
                            }
                            else if (white.Contains(dep))
                            {
                                FallbackDfs(dep);
                            }
                        }
                    }

                    if (foundBackEdge == null)
                    {
                        dfsPath.RemoveAt(dfsPath.Count - 1);
                        gray.Remove(node);
                    }
                }

                foreach (var module in depGraph.Keys.OrderBy(k => k))
                {
                    if (foundBackEdge != null) break;
                    if (white.Contains(module))
                        FallbackDfs(module);
                }

                if (foundBackEdge == null) break;

                var (from, to) = foundBackEdge.Value;

                // Extract the cycle from the DFS path
                // The cycle is: to → ... → from → to
                var cycleStart = dfsPath.IndexOf(to);
                var edgeToDefer = (from, to); // default: defer the back edge

                if (inheritanceEdges.Contains((from, to)) && cycleStart >= 0)
                {
                    // This is an inheritance edge — find a non-inheritance edge in the cycle to defer instead
                    bool foundAlternative = false;
                    for (int ci = cycleStart; ci < dfsPath.Count - 1; ci++)
                    {
                        var edgeFrom = dfsPath[ci];
                        var edgeTo = dfsPath[ci + 1];
                        if (!inheritanceEdges.Contains((edgeFrom, edgeTo)))
                        {
                            edgeToDefer = (edgeFrom, edgeTo);
                            foundAlternative = true;
                            break;
                        }
                    }
                    if (foundAlternative)
                        Console.Error.WriteLine($"  Fallback cycle {iteration}: protected inheritance ({from} -> {to}), deferred ({edgeToDefer.Item1} -> {edgeToDefer.Item2})");
                    else
                        Console.Error.WriteLine($"  Fallback cycle {iteration}: ({from} -> {to}) [inheritance, no alternative]");
                }
                else
                {
                    Console.Error.WriteLine($"  Fallback cycle {iteration}: ({from} -> {to})");
                }

                lazyEdges.Add(edgeToDefer);
            }
        }

        Console.Error.WriteLine($"  Total deferred edges: {lazyEdges.Count}");
        _lazyRequireEdges = lazyEdges;
    }

    private void ScanBaseClasses(SyntaxList<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    ScanBaseClasses(ns.Members);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    ScanBaseClasses(ns.Members);
                    break;
                case ClassDeclarationSyntax classDecl:
                    if (classDecl.BaseList != null)
                    {
                        foreach (var bt in classDecl.BaseList.Types)
                        {
                            var baseTypeName = bt.Type.ToString();
                            if (baseTypeName.Contains('.'))
                                baseTypeName = baseTypeName.Substring(baseTypeName.LastIndexOf('.') + 1);
                            if (baseTypeName.Contains('<'))
                                baseTypeName = baseTypeName.Substring(0, baseTypeName.IndexOf('<'));
                            // Skip interfaces (I + uppercase convention) and self-references (generic stripping)
                            if (baseTypeName.Length > 1 && baseTypeName[0] == 'I' && char.IsUpper(baseTypeName[1]))
                                continue;
                            if (baseTypeName == classDecl.Identifier.Text)
                                continue;
                            _baseClassMap[classDecl.Identifier.Text] = baseTypeName;
                            break;
                        }
                    }
                    // Check if class has a parameterless instance ToString()
                    if (classDecl.Members.OfType<MethodDeclarationSyntax>().Any(m =>
                        m.Identifier.Text == "ToString"
                        && !m.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && m.ParameterList.Parameters.Count == 0))
                    {
                        _typesWithToString.Add(classDecl.Identifier.Text);
                    }
                    // Recurse into nested types
                    ScanBaseClasses(classDecl.Members);
                    break;
                case StructDeclarationSyntax structDecl:
                    _structTypes.Add(structDecl.Identifier.Text);
                    if (structDecl.BaseList != null)
                    {
                        foreach (var bt in structDecl.BaseList.Types)
                        {
                            var baseTypeName = bt.Type.ToString();
                            if (baseTypeName.Contains('.'))
                                baseTypeName = baseTypeName.Substring(baseTypeName.LastIndexOf('.') + 1);
                            if (baseTypeName.Contains('<'))
                                baseTypeName = baseTypeName.Substring(0, baseTypeName.IndexOf('<'));
                            if (baseTypeName.Length > 1 && baseTypeName[0] == 'I' && char.IsUpper(baseTypeName[1]))
                                continue;
                            _baseClassMap[structDecl.Identifier.Text] = baseTypeName;
                            break;
                        }
                    }
                    break;
            }
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

        // Count constructor occurrences
        int constructorCount = 0;

        foreach (var member in members)
        {
            if (member is MethodDeclarationSyntax m)
            {
                // Skip explicit interface implementations (not emitted in Luau)
                if (m.ExplicitInterfaceSpecifier != null) continue;
                var name = m.Identifier.Text;
                methodNameCounts[name] = methodNameCounts.GetValueOrDefault(name) + 1;
            }
            else if (member is ConstructorDeclarationSyntax ctorMember)
            {
                // Skip static constructors — they don't emit as Luau constructors
                if (!ctorMember.Modifiers.Any(SyntaxKind.StaticKeyword))
                    constructorCount++;
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

        // Track emitted names to detect duplicates and apply __2, __3 suffixes
        var emitNameUsage = new Dictionary<string, int>();

        foreach (var member in members)
        {
            if (member is not MethodDeclarationSyntax method) continue;
            // Skip explicit interface implementations
            if (method.ExplicitInterfaceSpecifier != null) continue;

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

            // Apply dedup suffix if this emit name was already used (e.g., two overloads
            // with same first param type but different param counts: ToString(float) and
            // ToString(float, FloatFormatHandling, char, bool) both get emitName "ToString_float")
            var count = emitNameUsage.GetValueOrDefault(emitName);
            emitNameUsage[emitName] = count + 1;
            if (count > 0)
            {
                emitName = $"{emitName}__{count + 1}";
            }

            // Use first param type in the key to disambiguate same-count overloads
            // Match the emitter's nullable convention: strip ?, then append _nullable
            var firstParamType = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
            var isNullableFpt = firstParamType.EndsWith("?");
            firstParamType = firstParamType.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
            if (isNullableFpt) firstParamType += "_nullable";
            _globalOverloadMap[(typeName, name, paramCount, firstParamType)] = emitName;

            // Also store in full-signature map for precise resolution
            var allParamTypes = string.Join(",", method.ParameterList.Parameters.Select(p =>
            {
                var pt = p.Type?.ToString() ?? "";
                var isNullable = pt.EndsWith("?");
                pt = pt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_")
                    .Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullable) pt += "_nullable";
                return pt;
            }));
            _fullSignatureMap[(typeName, name, allParamTypes)] = emitName;
        }

        // Register constructor overloads in global map
        if (constructorCount > 1)
        {
            int ctorIndex = 0;
            var ctorNamesUsed = new HashSet<string> { "new" };
            foreach (var member in members)
            {
                if (member is not ConstructorDeclarationSyntax ctor) continue;
                // Skip static constructors — not emitted as Luau constructors
                if (ctor.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
                string ctorEmitName = "new";
                if (ctorIndex > 0)
                {
                    var firstParam = ctor.ParameterList.Parameters.FirstOrDefault();
                    var suffix = firstParam?.Type?.ToString() ?? $"_{ctorIndex}";
                    var isNullableCtorSuffix = suffix.EndsWith("?");
                    suffix = suffix.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                    if (isNullableCtorSuffix) suffix += "_nullable";
                    ctorEmitName = $"new_{suffix}";
                    if (ctorNamesUsed.Contains(ctorEmitName))
                    {
                        int counter = 2;
                        while (ctorNamesUsed.Contains($"{ctorEmitName}__{counter}"))
                            counter++;
                        ctorEmitName = $"{ctorEmitName}__{counter}";
                    }
                }
                ctorNamesUsed.Add(ctorEmitName);
                var paramCount = ctor.ParameterList.Parameters.Count;
                var fpt = ctor.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() ?? "";
                var isNullableFpt = fpt.EndsWith("?");
                fpt = fpt.Replace("?", "").Replace("[]", "_Array").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "").Replace(" ", "");
                if (isNullableFpt) fpt += "_nullable";
                _globalOverloadMap[(typeName, "new", paramCount, fpt)] = ctorEmitName;
                ctorIndex++;
            }
        }
    }

    public TranspileResult Transpile(string sourceCode, string fileName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, GetParseOptions(), path: fileName);
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
        emitter.FullSignatureOverloadMap = _fullSignatureMap;
        emitter.GlobalBaseClassMap = _baseClassMap;
        emitter.GlobalStructTypes = _structTypes;
        emitter.GlobalTypesWithToString = _typesWithToString;
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
                case InterfaceDeclarationSyntax ifaceDecl:
                    emitter.EmitInterface(ifaceDecl);
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
        else if (emitter.EmittedTopLevelTypes.Count == 0)
        {
            // Interface-only files have no runtime values but still need a return for require()
            emitter.AppendLine("return {}");
        }

        // Collect type names defined in this file to exclude from requires
        var localTypeNames = CollectTypeNames(root.Members);

        // Post-process: insert require() statements for referenced modules
        var currentModuleName = Path.GetFileNameWithoutExtension(fileName);
        InsertRequires(emitter, localTypeNames, currentModuleName);

        // Insert runtime require if needed
        if (emitter.NeedsRuntime)
        {
            emitter.InsertAfterHeader("local __rt = require(script.Parent.LUSharpRuntime)\n\n");
        }

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
                case InterfaceDeclarationSyntax ifaceDecl:
                    emitter.EmitInterface(ifaceDecl);
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
                case InterfaceDeclarationSyntax:
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
                case InterfaceDeclarationSyntax ifaceDecl:
                    names.Add(ifaceDecl.Identifier.Text);
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
                case InterfaceDeclarationSyntax ifaceDecl:
                    names.Add(ifaceDecl.Identifier.Text);
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
    private void InsertRequires(LuauEmitter emitter, HashSet<string>? localTypeNames = null, string? currentModuleName = null)
    {
        if (emitter.ReferencedModules.Count == 0) return;

        // Filter out modules that are known not to need requires or are defined in the same file
        var typesToRequire = emitter.ReferencedModules
            .Where(m => !IsIgnoredModule(m))
            .Where(m => localTypeNames == null || !localTypeNames.Contains(m))
            .OrderBy(m => m)
            .ToList();

        if (typesToRequire.Count == 0) return;

        // Get the emitted body to check which types are actually used at runtime
        var body = emitter.GetOutput();

        // Filter to only types that appear in the emitted body (not just in type annotations)
        typesToRequire = typesToRequire
            .Where(m => body.Contains(m))
            .ToList();

        if (typesToRequire.Count == 0) return;

        // Group types by their source module (using the type-to-module map)
        // Only include types that are actually in the project (in _typeToModuleMap)
        var moduleToTypes = new Dictionary<string, List<string>>();
        foreach (var typeName in typesToRequire)
        {
            // Skip external .NET types not in the project
            if (!_typeToModuleMap.ContainsKey(typeName))
                continue;

            var moduleName = _typeToModuleMap[typeName];

            // Resolve name collisions: if multiple modules export the same type name,
            // prefer the one in the current module's base class chain.
            // E.g., JsonTextReader extends JsonReader; both JsonReader and JsonWriter export "State"
            // → prefer JsonReader's State since it's in the inheritance chain.
            if (_typeToModuleCollisions.TryGetValue(typeName, out var alternatives) && currentModuleName != null)
            {
                var allCandidates = new HashSet<string>(alternatives) { moduleName };
                // Walk the base class chain of the main class (same name as module)
                var cur = currentModuleName;
                while (_baseClassMap.TryGetValue(cur, out var baseClass))
                {
                    if (_typeToModuleMap.TryGetValue(baseClass, out var baseMod) && allCandidates.Contains(baseMod))
                    {
                        moduleName = baseMod;
                        break;
                    }
                    cur = baseClass;
                }
            }

            if (!moduleToTypes.ContainsKey(moduleName))
                moduleToTypes[moduleName] = new List<string>();
            moduleToTypes[moduleName].Add(typeName);
        }

        // Remove self-references (nested types mapped to the same module file)
        if (currentModuleName != null)
            moduleToTypes.Remove(currentModuleName);

        if (moduleToTypes.Count == 0) return;

        // Cycle detection is handled upfront by DetectCycles() during PreScan.
        // _lazyRequireEdges is already populated with all cycle-breaking edges.

        var requireBlock = new System.Text.StringBuilder();
        var lazyBlock = new System.Text.StringBuilder();
        foreach (var (moduleName, types) in moduleToTypes.OrderBy(kv => kv.Key))
        {
            bool isLazy = currentModuleName != null && _lazyRequireEdges.Contains((currentModuleName, moduleName));

            if (isLazy)
            {
                // Emit lazy require via metatable proxy.
                // Accessing any key triggers the require on demand.
                // By the time user code calls into these, the owning module has fully returned,
                // so the circular require resolves (the target can now require us from cache).
                requireBlock.AppendLine($"local _{moduleName}");
                foreach (var typeName in types.OrderBy(t => t))
                {
                    // Each type gets a proxy that lazy-resolves on first key access
                    requireBlock.AppendLine($"local {typeName} = setmetatable({{}}, {{__index = function(_, k)");
                    requireBlock.AppendLine($"\tif not _{moduleName} then _{moduleName} = require(script.Parent.{moduleName}) end");
                    requireBlock.AppendLine($"\treturn _{moduleName}.{typeName}[k]");
                    requireBlock.AppendLine($"end}})");
                    requireBlock.AppendLine($"type {typeName} = any");
                }
            }
            else
            {
                requireBlock.AppendLine($"local _{moduleName} = require(script.Parent.{moduleName})");
                foreach (var typeName in types.OrderBy(t => t))
                {
                    requireBlock.AppendLine($"local {typeName} = _{moduleName}.{typeName}");
                }
            }

            // Emit type aliases so --!strict mode recognizes imported types in annotations.
            foreach (var typeName in types.OrderBy(t => t))
            {
                // Only alias names that look like type names (PascalCase)
                if (typeName.Length > 0 && char.IsUpper(typeName[0]))
                {
                    if (isLazy)
                    {
                        // Lazy imports are nil at declaration time — use 'any' as placeholder type
                        requireBlock.AppendLine($"type {typeName} = any");
                    }
                    else
                    {
                        requireBlock.AppendLine($"type {typeName} = typeof({typeName})");
                    }
                }
            }
        }
        requireBlock.AppendLine();

        // Append lazy initializers after the regular requires
        if (lazyBlock.Length > 0)
        {
            requireBlock.Append(lazyBlock);
            requireBlock.AppendLine();
        }

        // Emit type aliases for external .NET types not in the project.
        // These are referenced in type annotations but have no Luau equivalent.
        var importedTypes = new HashSet<string>(moduleToTypes.Values.SelectMany(v => v));
        var externalTypes = typesToRequire
            .Where(t => !_typeToModuleMap.ContainsKey(t))
            .Where(t => t.Length > 0 && char.IsUpper(t[0]))
            .Where(t => !importedTypes.Contains(t))
            .OrderBy(t => t)
            .ToList();
        if (externalTypes.Count > 0)
        {
            foreach (var typeName in externalTypes)
            {
                requireBlock.AppendLine($"type {typeName} = any");
            }
            requireBlock.AppendLine();
        }

        // Scan emitted output for type names used in annotations but never declared.
        // This catches .NET framework types (TextReader, DateTime, etc.) that MapComplexType
        // passes through unchanged and TrackTypeReferences doesn't track.
        var output = emitter.GetOutput();
        var declaredTypes = new HashSet<string>();

        // Collect all type declarations in the output
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimStart();
            // Match: type X = ..., export type X = ...
            if (trimmed.StartsWith("type ") || trimmed.StartsWith("export type "))
            {
                var afterType = trimmed.StartsWith("export type ") ? trimmed.Substring(12) : trimmed.Substring(5);
                var eqIdx = afterType.IndexOf(' ');
                if (eqIdx > 0)
                    declaredTypes.Add(afterType.Substring(0, eqIdx));
            }
        }

        // Add imported value names as declared (they're usable as types via typeof)
        foreach (var types in moduleToTypes.Values)
            foreach (var t in types)
                declaredTypes.Add(t);

        // Add current class names
        if (localTypeNames != null)
            foreach (var t in localTypeNames)
                declaredTypes.Add(t);

        // Add external types already emitted as stubs above
        foreach (var t in externalTypes)
            declaredTypes.Add(t);

        // Luau built-in types
        var luauBuiltins = new HashSet<string> {
            "string", "number", "boolean", "any", "nil", "never", "unknown", "thread", "buffer"
        };

        // Find undefined type names in function signatures and field annotations
        var undefinedTypes = new HashSet<string>();
        var typeRefRegex = new System.Text.RegularExpressions.Regex(@":\s*([A-Z][A-Za-z0-9_]*)(?:\s*[,\)\?]|\s*$)");
        var selfTypeRegex = new System.Text.RegularExpressions.Regex(@":\s*\{\s*([A-Z][A-Za-z0-9_]*)");
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("function ") || trimmed.Contains(":: ") || trimmed.Contains(": "))
            {
                foreach (System.Text.RegularExpressions.Match match in typeRefRegex.Matches(line))
                {
                    var typeName = match.Groups[1].Value;
                    if (!luauBuiltins.Contains(typeName) && !declaredTypes.Contains(typeName))
                        undefinedTypes.Add(typeName);
                }
                foreach (System.Text.RegularExpressions.Match match in selfTypeRegex.Matches(line))
                {
                    var typeName = match.Groups[1].Value;
                    if (!luauBuiltins.Contains(typeName) && !declaredTypes.Contains(typeName))
                        undefinedTypes.Add(typeName);
                }
            }
        }

        if (undefinedTypes.Count > 0)
        {
            var stubBlock = new System.Text.StringBuilder();
            foreach (var typeName in undefinedTypes.OrderBy(t => t))
            {
                stubBlock.AppendLine($"type {typeName} = any");
            }
            stubBlock.AppendLine();
            requireBlock.Append(stubBlock);
        }

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
            or "Char" or "String" or "Int32" or "Int64" or "UInt32"
            or "Double" or "Single" or "Convert" or "Byte" or "SByte"
            or "Array" or "Enumerable" or "Object" or "Task"
            or "StringBuilder" or "Environment" or "Interlocked"
            or "StringComparison" or "TypeCode" or "DateTimeKind"
            or "MemberTypes" or "BindingFlags"
            or "DateTimeStyles" or "RegexOptions" or "CultureInfo"
            or "TraceLevel" or "Activator" or "Volatile"
            or "Attribute" or "Assembly"
            or "StringWriter" or "StringReader" or "KeyValuePair"
            or "ArgumentException" or "ArgumentNullException"
            or "ArgumentOutOfRangeException" or "InvalidOperationException"
            or "NotSupportedException" or "NotImplementedException"
            or "FormatException" or "OverflowException" or "IndexOutOfRangeException"
            or "Regex" or "Uri" or "Version" or "Tuple"
            or "CancellationToken" or "DateTime" or "DateTimeOffset"
            or "TimeSpan" or "BigInteger" or "Guid" or "Collection"
            or "ReadOnlyCollection" or "KeyedCollection" or "NameTable"
            or "ConcurrentDictionary" or "PropertyDescriptorCollection"
            or "NotifyCollectionChangedEventArgs" or "ListChangedEventArgs"
            or "PropertyChangedEventArgs" or "PropertyChangingEventArgs"
            or "AddingNewEventArgs" or "StreamingContext" or "SerializationInfo"
            or "DictionaryEntry" or "FormatterConverter"
            or "TraceEventType" or "TraceEventCache" or "ExpressionType"
            or "UTF8Encoding" or "ExpandoObject" or "BigInteger"
            or "GC" or "EqualityComparer" or "StringComparer"
            or "EventDescriptorCollection" or "Comparer"
            or "Encoding" or "Expression" or "DynamicMethod"
            or "StringReader" or "StringWriter" or "TextReader" or "TextWriter";
    }
}

public class TranspileResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = "";
    public string LuauSource { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}
