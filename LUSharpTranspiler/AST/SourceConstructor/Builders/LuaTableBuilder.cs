using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class LuaTableBuilder : ILuaRenderable
    {
        private readonly string _name;
        private readonly List<(string Key, object Value)> _fields = new();

        public LuaTableBuilder(string name = null)
        {
            _name = name;
        }

        /// <summary>Add a key-value pair. Value can be string, number, bool, LuaTableBuilder, or arbitrary Lua expression.</summary>
        public LuaTableBuilder AddField(string key, object value)
        {
            _fields.Add(($"[\"{key}\"]", value));
            return this;
        }
        public LuaTableBuilder AddField(int key, object value)
        {
            _fields.Add(($"[{key}]", value));
            return this;
        }

        public void Render(LuaWriter writer)
        {
            string prefix = _name != null ? $"{_name} = " : "";
            writer.WriteLine($"{prefix}{{");
            writer.IndentMore();

            foreach (var (key, value) in _fields)
            {
                if (value is LuaTableBuilder nestedTable)
                {
                    // Render nested table inline with key
                    writer.WriteLine($"{key} = {{");
                    nestedTable.RenderFields(writer);
                    writer.WriteLine("},");
                }
                else if (value is string s)
                {
                    writer.WriteLine($"{key} = {s},");
                }
                else if (value is bool b)
                {
                    writer.WriteLine($"{key} = {(b ? "true" : "false")},");
                }
                else
                {
                    writer.WriteLine($"{key} = {value},");
                }
            }

            writer.IndentLess();
            writer.WriteLine("}");
        }

        // Helper for rendering a nested table without variable name
        private void RenderFields(LuaWriter writer)
        {
            foreach (var (key, value) in _fields)
            {
                writer.IndentMore();

                if (value is LuaTableBuilder nested)
                {
                    writer.WriteLine($"{key} = {{");
                    nested.RenderFields(writer);
                    writer.WriteLine("},");
                }
                else if (value is string s)
                {
                    writer.WriteLine($"{key} = {s},");
                }
                else if (value is bool b)
                {
                    writer.WriteLine($"{key} = {(b ? "true" : "false")},");
                }
                else
                {
                    writer.WriteLine($"{key} = {value},");
                }
                writer.IndentLess();
            }
        }

        public string Build()
        {
            var writer = new LuaWriter();
            Render(writer);
            return writer.ToString();
        }
    }
}
