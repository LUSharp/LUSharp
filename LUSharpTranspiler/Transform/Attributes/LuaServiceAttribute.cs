namespace LUSharpTranspiler.Transform.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class LuaServiceAttribute(string serviceName) : Attribute
{
    public string ServiceName { get; } = serviceName;
}
