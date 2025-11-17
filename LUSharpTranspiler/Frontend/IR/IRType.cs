namespace LUSharpTranspiler.Frontend.IR
{
    public class IRType
    {
        public string Name { get; set; }   // "number", "string", "Vector3", custom class
        public bool IsArray { get; set; }
        public bool IsNullable { get; set; }
    }
}