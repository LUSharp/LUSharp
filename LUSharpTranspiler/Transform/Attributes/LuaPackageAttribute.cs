namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class LuaPackageAttribute(string packageName) : Attribute
{
    public string PackageName { get; } = packageName;
}
