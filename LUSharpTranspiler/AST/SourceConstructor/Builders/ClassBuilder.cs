using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    using System;
    using System.Collections.Generic;

    public class ClassBuilder : ILuaRenderable
    {
        public string Name { get; private set; }
        private LuaFunction? _constructor;
        private readonly List<LuaFunction> _methods = new();

        private ClassBuilder(string name) => Name = name;

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

        public ClassBuilder Build()
        {
            var writer = new LuaWriter();
            Render(writer);
            Console.WriteLine(writer.ToString());
            return this;
        }

        public void Render(LuaWriter writer)
        {
            writer.WriteLine($"local {Name} = {{}}");
            writer.WriteLine();

            _constructor?.Render(writer);

            foreach (var m in _methods)
                m.Render(writer);

            writer.WriteLine($"return {Name}");
        }
    }

}
