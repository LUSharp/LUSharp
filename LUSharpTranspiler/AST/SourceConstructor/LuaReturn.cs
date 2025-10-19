using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTranspiler.AST.SourceConstructor
{
    public class LuaReturn : ILuaRenderable
    {
        public string Expression { get; }
        public LuaReturn(string expr) => Expression = expr;
        public void Render(LuaWriter writer) => writer.WriteLine($"return {Expression}");
    }
}
