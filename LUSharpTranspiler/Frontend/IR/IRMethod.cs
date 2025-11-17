using LUSharpTranspiler.Frontend.IR.Statement;

namespace LUSharpTranspiler.Frontend.IR
{
    public class IRMethod
    {
        public string Name { get; set; }
        public IRType ReturnType { get; set; }
        public bool IsStatic { get; set; }
        public List<IRParameter> Parameters { get; set; } = new();
        public List<IRStatement> Body { get; set; } = new();
        public List<IRAttribute> Attributes { get; set; } = new();
    }
}