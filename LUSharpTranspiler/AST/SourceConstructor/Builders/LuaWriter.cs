using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST.SourceConstructor.Builders
{
    public class LuaWriter
    {
        private readonly StringBuilder _sb = new();
        private int _indent;
        private const string Indent = "    ";

        public void IndentMore() => _indent++;
        public void IndentLess() => _indent--;

        public void WriteLine(string text = "")
        {
            if (text.Length > 0)
                _sb.Append(new string(' ', _indent * Indent.Length)).AppendLine(text);
            else
                _sb.AppendLine();
        }

        public void WriteInline(string text) => _sb.Append(text);

        public void WriteIndent() => _sb.Append(new string(' ', _indent * Indent.Length));

        public override string ToString() => _sb.ToString();
    }
}
