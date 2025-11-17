namespace LUSharpTranspiler.Frontend.IR
{
    public class IRClass
    {
        public string Name { get; set; }
        public bool IsStatic { get; set; }
        public List<IRField> Fields { get; set; } = new();
        public List<IRMethod> Methods { get; set; } = new();
        public IRConstructor? Constructor { get; set; }
        public List<IRAttribute> Attributes { get; set; } = new();
    }

}