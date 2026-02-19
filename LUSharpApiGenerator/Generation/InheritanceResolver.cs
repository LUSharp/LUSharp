using LUSharpApiGenerator.Models;

namespace LUSharpApiGenerator.Generation;

public class InheritanceResolver
{
    private readonly Dictionary<string, string> _parentMap = new();
    private readonly Dictionary<string, List<string>> _childrenMap = new();

    public void BuildTree(List<ApiClass> classes)
    {
        foreach (var cls in classes)
        {
            string parent = cls.Superclass;

            // Normalize root references
            if (parent is "<<<ROOT>>>" or "")
                parent = "";
            else if (parent == "Object")
                parent = "RObject";

            _parentMap[cls.Name] = parent;

            if (!string.IsNullOrEmpty(parent))
            {
                if (!_childrenMap.ContainsKey(parent))
                    _childrenMap[parent] = new List<string>();
                _childrenMap[parent].Add(cls.Name);
            }
        }
    }

    public string? GetBaseClass(string className)
    {
        if (_parentMap.TryGetValue(className, out var parent) && !string.IsNullOrEmpty(parent))
            return parent;
        return null;
    }

    public List<string> GetChildren(string className)
    {
        return _childrenMap.TryGetValue(className, out var children) ? children : new List<string>();
    }
}
