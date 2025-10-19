using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Transpiler
{
    internal class LuaBuilder
    {
        private StringBuilder builder = new();
        private int indentLevel = 0;

        internal void AppendLine(string line)
        {
            builder.AppendLine(new string(' ', indentLevel * 4) + line);
        }

        internal void Append(string text)
        {
            builder.Append(new string(' ', indentLevel * 4) + text);
        }

        internal void AppendNoIndent(string text)
        {
            builder.Append(text);
        }

        internal void AppendLineNoIndent(string text)
        {
            builder.AppendLine(text);
        }

        internal void IncreaseIndent() => indentLevel++;
        internal void DecreaseIndent() => indentLevel--;

        







        public override string ToString() => builder.ToString();
    }
}
