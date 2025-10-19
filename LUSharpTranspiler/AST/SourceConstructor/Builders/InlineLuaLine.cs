using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class InlineLuaLine : ILuaRenderable
    {
        private readonly string _line;
        public InlineLuaLine(string line) => _line = line;
        public void Render(LuaWriter writer) => writer.WriteLine(_line);
    }
}
