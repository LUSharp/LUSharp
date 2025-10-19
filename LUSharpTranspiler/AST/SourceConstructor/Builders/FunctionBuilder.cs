using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class FunctionBuilder
    {
        private readonly LuaFunction _func;
        private readonly List<ILuaRenderable> _stmts = new();

        public FunctionBuilder(string name)
        {
            _func = new LuaFunction(name);
        }

        public FunctionBuilder WithParameter(string name)
        {
            _func.Parameters.Add(name);
            return this;
        }

        public FunctionBuilder Assign(string left, string right)
        {
            _stmts.Add(new LuaAssignment(left, right));
            return this;
        }

        public FunctionBuilder Return(string expr)
        {
            _stmts.Add(new LuaReturn(expr));
            return this;
        }

        public LuaFunction Build()
        {
            _func.Body.AddRange(_stmts);
            return _func;
        }
    }
}
