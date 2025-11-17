namespace LUSharpTranspiler.Frontend.IR
{
    public class IRAttribute
    {
        public string Name { get; set; }
        public List<object> Arguments { get; set; } = new();
    }

}