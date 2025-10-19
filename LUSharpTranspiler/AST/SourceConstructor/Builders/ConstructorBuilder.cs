using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class ConstructorBuilder
    {
        private readonly string _className;
        private readonly List<string> _parameters = new();
        private readonly List<(string key, string value)> _privateFields = new();

        public ConstructorBuilder(string className)
        {
            _className = className;
        }

        public ConstructorBuilder WithParameter(string name)
        {
            _parameters.Add(name);
            return this;
        }

        public ConstructorBuilder WithPrivateField(string key, string value)
        {
            _privateFields.Add((key, value));
            return this;
        }

        public LuaFunction Build()
        {
            var func = new LuaFunction($"{_className}.new");
            func.Parameters.AddRange(_parameters);

            func.Body.Add(new LuaAssignment("local members", "{"));
            foreach (var (key, value) in _privateFields)
            {
                func.Body.Add(new InlineLuaLine($"    {key} = {value},"));
            }
            func.Body.Add(new InlineLuaLine("}"));
            func.Body.Add(new LuaReturn("members"));
            return func;
        }
    }

}
