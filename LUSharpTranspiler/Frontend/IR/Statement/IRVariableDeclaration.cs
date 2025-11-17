using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR.Statement
{
    public class IRVariableDeclaration : IRStatement
    {
        public string Name { get; set; }
        public IRType Type { get; set; }
        public IRExpression? Initializer { get; set; }
    }
}
