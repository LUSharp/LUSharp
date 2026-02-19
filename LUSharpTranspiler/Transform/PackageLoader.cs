using System.Reflection;
using LUSharpTranspiler.Transform.Attributes;

namespace LUSharpTranspiler.Transform;

public record PackageMapping(string ClassName, string MethodName, string LuaExpr, string? RequirePath);

public class PackageLoader
{
    private readonly SymbolTable _symbols;
    private readonly List<PackageMapping> _mappings = new();

    public PackageLoader(SymbolTable symbols) => _symbols = symbols;

    public IReadOnlyList<PackageMapping> Mappings => _mappings;

    /// <summary>
    /// Load a package assembly and register its [LuaMapping] methods.
    /// </summary>
    public void LoadFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var pkg = type.GetCustomAttribute<LuaPackageAttribute>();
            var svc = type.GetCustomAttribute<LuaServiceAttribute>();
            var isGlobal = type.GetCustomAttribute<LuaGlobalAttribute>() != null;

            string? requirePath = pkg != null
                ? $"game.ReplicatedStorage.Runtime.{pkg.PackageName}"
                : svc != null
                    ? $"game:GetService(\"{svc.ServiceName}\")"
                    : null;

            foreach (var method in type.GetMethods())
            {
                var mapping = method.GetCustomAttribute<LuaMappingAttribute>();
                if (mapping != null)
                    _mappings.Add(new PackageMapping(type.Name, method.Name, mapping.LuaExpr, requirePath));
            }
        }
    }
}
