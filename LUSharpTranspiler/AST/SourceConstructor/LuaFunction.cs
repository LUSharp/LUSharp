using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTranspiler.AST.SourceConstructor
{
    public class LuaFunction : ILuaRenderable
    {
        public string Name { get; }
        public List<string> Parameters { get; } = new();
        public List<ILuaRenderable> Body { get; } = new();

        public LuaFunction(string name)
        {
            Name = name;
        }

        public void Render(LuaWriter writer)
        {
            writer.WriteLine($"function {Name}({string.Join(", ", Parameters)})");
            writer.IndentMore();
            foreach (var stmt in Body)
                stmt.Render(writer);
            writer.IndentLess();
            writer.WriteLine("end");
            writer.WriteLine();
        }
    }
}
