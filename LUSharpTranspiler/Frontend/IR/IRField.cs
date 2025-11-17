using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR
{
    public class IRField
    {
        public string Name { get; set; }
        public IRType Type { get; set; }
        public IRExpression? Initializer { get; set; }
        public bool IsStatic { get; set; }
    }
}