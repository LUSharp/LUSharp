using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTranspiler.AST.SourceConstructor
{
    public class LuaAssignment : ILuaRenderable
    {
        public string Left { get; }
        public string Right { get; }

        public LuaAssignment(string left, string right)
        {
            Left = left;
            Right = right;
        }

        public void Render(LuaWriter writer)
            => writer.WriteLine($"{Left} = {Right}");
    }

}
