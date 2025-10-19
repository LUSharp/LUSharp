using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor.Builders;

namespace LUSharpTranspiler.AST.SourceConstructor
{
    public static class SourceConstructor
    {
        public class ClassBuilder : ILuaRenderable
        {
            public string Name { get; private set; }
            private LuaFunction? _constructor;
            private readonly List<LuaFunction> _methods = new();
            private LuaTableBuilder _staticFields;

            private ClassBuilder(string name)
            {
                Name = name;
                _staticFields = new(name);
            }

            public static ClassBuilder Create(string name) => new(name);

            public ClassBuilder WithConstructor(Action<ConstructorBuilder> build)
            {
                var cb = new ConstructorBuilder(Name);
                build(cb);
                _constructor = cb.Build();
                return this;
            }

            public ClassBuilder WithMethod(string name, Action<FunctionBuilder> build)
            {
                var fb = new FunctionBuilder($"{Name}.{name}");
                build(fb);
                _methods.Add(fb.Build());
                return this;
            }

            public ClassBuilder WithField(string key, object value)
            {
                _staticFields.AddField(key, value);
                return this;
            }

            public string Build()
            {
                var writer = new LuaWriter();
                Render(writer);
                return writer.ToString();
            }

            public void Render(LuaWriter writer)
            {
                writer.WriteLine($"local {_staticFields.Build()}");
                writer.WriteLine();

                _constructor?.Render(writer);

                foreach (var m in _methods)
                    m.Render(writer);

                writer.WriteLine($"return {Name}");
            }
        }

    }
}
