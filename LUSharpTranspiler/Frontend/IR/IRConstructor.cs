using LUSharpTranspiler.Frontend.IR.Statement;

namespace LUSharpTranspiler.Frontend.IR
{
    public class IRConstructor
    {
        public List<IRParameter> Parameters { get; set; } = new();
        public List<IRStatement> Body { get; set; } = new();
    }
}