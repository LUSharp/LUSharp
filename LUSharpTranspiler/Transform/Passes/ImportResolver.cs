using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;

namespace LUSharpTranspiler.Transform.Passes;

/// <summary>
/// Analyzes LuaModules for references to classes defined in other modules
/// and populates LuaModule.Requires with appropriate require() paths.
/// </summary>
public static class ImportResolver
{
    public static void Resolve(List<LuaModule> modules, SymbolTable symbols)
    {
        // Build a map: className → module that defines it
        var classToModule = new Dictionary<string, LuaModule>();
        foreach (var module in modules)
        {
            foreach (var cls in module.Classes)
                classToModule[cls.Name] = module;
        }

        foreach (var module in modules)
        {
            // Collect all class names referenced via LuaNew in this module
            var referencedClasses = new HashSet<string>();
            foreach (var cls in module.Classes)
                CollectReferences(cls, referencedClasses);
            CollectReferencesFromBlock(module.EntryBody, referencedClasses);

            // For each referenced class not defined in this module, add a require
            var requires = new List<LuaRequire>();
            foreach (var name in referencedClasses.OrderBy(n => n))
            {
                // Skip classes defined in this module
                if (module.Classes.Any(c => c.Name == name))
                    continue;

                if (classToModule.TryGetValue(name, out var sourceModule))
                {
                    var requirePath = BuildRequirePath(module, sourceModule);
                    requires.Add(new LuaRequire(name, requirePath));
                }
            }

            if (requires.Count > 0)
            {
                // LuaModule.Requires is init-only with a default list, so we add to it
                foreach (var req in requires)
                    module.Requires.Add(req);
            }
        }
    }

    private static string BuildRequirePath(LuaModule from, LuaModule target)
    {
        // In Roblox, requires use script hierarchy paths.
        // Output paths are like "client/Main.lua", "shared/Utils.lua"
        // We generate: require(script.Parent.Parent.Shared.Utils) style paths
        // For now, use a relative-style require that BuildCommand / Rojo can resolve:
        //   require(game.ReplicatedStorage.Shared.ModuleName)
        //   require(script.Parent.ModuleName)
        var targetDir = Path.GetDirectoryName(target.OutputPath)?.Replace('\\', '/') ?? "";
        var targetName = Path.GetFileNameWithoutExtension(target.OutputPath);
        var fromDir = Path.GetDirectoryName(from.OutputPath)?.Replace('\\', '/') ?? "";

        // Same directory: require(script.Parent.ModuleName)
        if (targetDir == fromDir)
            return $"require(script.Parent.{targetName})";

        // Map output directories to Roblox service paths
        var robloxPath = targetDir switch
        {
            "shared" => $"game.ReplicatedStorage.Shared.{targetName}",
            "client" => $"game.StarterPlayer.StarterPlayerScripts.{targetName}",
            "server" => $"game.ServerScriptService.{targetName}",
            _ => $"script.Parent.{targetName}"
        };

        return $"require({robloxPath})";
    }

    private static void CollectReferences(LuaClassDef cls, HashSet<string> refs)
    {
        if (cls.Constructor != null)
            CollectReferencesFromBlock(cls.Constructor.Body, refs);
        foreach (var method in cls.Methods)
            CollectReferencesFromBlock(method.Body, refs);
        foreach (var field in cls.StaticFields)
            if (field.Value != null) CollectReferencesFromExpr(field.Value, refs);
        foreach (var field in cls.InstanceFields)
            if (field.Value != null) CollectReferencesFromExpr(field.Value, refs);
    }

    private static void CollectReferencesFromBlock(List<ILuaStatement> block, HashSet<string> refs)
    {
        foreach (var stmt in block)
            CollectReferencesFromStmt(stmt, refs);
    }

    private static void CollectReferencesFromStmt(ILuaStatement stmt, HashSet<string> refs)
    {
        switch (stmt)
        {
            case LuaLocal loc:
                if (loc.Value != null) CollectReferencesFromExpr(loc.Value, refs);
                break;
            case LuaAssign a:
                CollectReferencesFromExpr(a.Target, refs);
                CollectReferencesFromExpr(a.Value, refs);
                break;
            case LuaReturn ret:
                if (ret.Value != null) CollectReferencesFromExpr(ret.Value, refs);
                break;
            case LuaExprStatement e:
                CollectReferencesFromExpr(e.Expression, refs);
                break;
            case LuaConnect c:
                CollectReferencesFromExpr(c.Event, refs);
                CollectReferencesFromExpr(c.Handler, refs);
                break;
            case LuaIf ifS:
                CollectReferencesFromExpr(ifS.Condition, refs);
                CollectReferencesFromBlock(ifS.Then, refs);
                foreach (var ei in ifS.ElseIfs)
                {
                    CollectReferencesFromExpr(ei.Condition, refs);
                    CollectReferencesFromBlock(ei.Body, refs);
                }
                if (ifS.Else != null) CollectReferencesFromBlock(ifS.Else, refs);
                break;
            case LuaWhile wh:
                CollectReferencesFromExpr(wh.Condition, refs);
                CollectReferencesFromBlock(wh.Body, refs);
                break;
            case LuaRepeat rep:
                CollectReferencesFromBlock(rep.Body, refs);
                CollectReferencesFromExpr(rep.Condition, refs);
                break;
            case LuaForNum fn:
                CollectReferencesFromExpr(fn.Start, refs);
                CollectReferencesFromExpr(fn.Limit, refs);
                if (fn.Step != null) CollectReferencesFromExpr(fn.Step, refs);
                CollectReferencesFromBlock(fn.Body, refs);
                break;
            case LuaForIn fi:
                CollectReferencesFromExpr(fi.Iterator, refs);
                CollectReferencesFromBlock(fi.Body, refs);
                break;
            case LuaPCall pc:
                CollectReferencesFromBlock(pc.TryBody, refs);
                CollectReferencesFromBlock(pc.CatchBody, refs);
                CollectReferencesFromBlock(pc.FinallyBody, refs);
                break;
            case LuaTaskSpawn ts:
                CollectReferencesFromBlock(ts.Body, refs);
                break;
            case LuaError err:
                CollectReferencesFromExpr(err.Message, refs);
                break;
            case LuaMultiAssign ma:
                foreach (var v in ma.Values) CollectReferencesFromExpr(v, refs);
                break;
        }
    }

    private static void CollectReferencesFromExpr(ILuaExpression expr, HashSet<string> refs)
    {
        switch (expr)
        {
            case LuaNew n:
                refs.Add(n.ClassName);
                foreach (var a in n.Args) CollectReferencesFromExpr(a, refs);
                break;
            case LuaCall call:
                CollectReferencesFromExpr(call.Function, refs);
                foreach (var a in call.Args) CollectReferencesFromExpr(a, refs);
                break;
            case LuaMethodCall mc:
                CollectReferencesFromExpr(mc.Object, refs);
                foreach (var a in mc.Args) CollectReferencesFromExpr(a, refs);
                break;
            case LuaMember m:
                CollectReferencesFromExpr(m.Object, refs);
                break;
            case LuaIndex ix:
                CollectReferencesFromExpr(ix.Object, refs);
                CollectReferencesFromExpr(ix.Key, refs);
                break;
            case LuaBinary b:
                CollectReferencesFromExpr(b.Left, refs);
                CollectReferencesFromExpr(b.Right, refs);
                break;
            case LuaUnary u:
                CollectReferencesFromExpr(u.Operand, refs);
                break;
            case LuaConcat c:
                foreach (var p in c.Parts) CollectReferencesFromExpr(p, refs);
                break;
            case LuaTernary t:
                CollectReferencesFromExpr(t.Condition, refs);
                CollectReferencesFromExpr(t.Then, refs);
                CollectReferencesFromExpr(t.Else, refs);
                break;
            case LuaNullSafe ns:
                CollectReferencesFromExpr(ns.Object, refs);
                break;
            case LuaCoalesce co:
                CollectReferencesFromExpr(co.Left, refs);
                CollectReferencesFromExpr(co.Right, refs);
                break;
            case LuaTable tbl:
                foreach (var e in tbl.Entries) CollectReferencesFromExpr(e.Value, refs);
                break;
            case LuaLambda lam:
                CollectReferencesFromBlock(lam.Body, refs);
                break;
            case LuaSpread sp:
                CollectReferencesFromExpr(sp.Table, refs);
                break;
        }
    }
}
