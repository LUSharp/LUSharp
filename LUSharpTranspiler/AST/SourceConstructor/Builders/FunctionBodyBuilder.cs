using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class FunctionBodyBuilder
    {
        private readonly List<ILuaRenderable> _stmts = new();

        public FunctionBodyBuilder Assign(string left, string right)
        {
            _stmts.Add(new LuaAssignment(left, right));
            return this;
        }

        public FunctionBodyBuilder Return(string expr)
        {
            _stmts.Add(new LuaReturn(expr));
            return this;
        }

        public List<ILuaRenderable> Build() => _stmts;
    }
}
