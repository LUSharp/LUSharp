using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTranspiler.AST.SourceConstructor
{
    public class LuaClass : ILuaRenderable
    {
        private readonly string _name;
        private readonly List<ILuaRenderable> _members;

        public LuaClass(string name, List<ILuaRenderable> members)
        {
            _name = name;
            _members = members;
        }

        public void Render(LuaWriter writer)
        {
            writer.WriteLine($"local {_name} = {{}}");
            writer.WriteLine($"{_name}.__index = {_name}");
            writer.WriteLine();
            foreach (var m in _members)
                m.Render(writer);
            writer.WriteLine($"return {_name}");
        }
    }
}
