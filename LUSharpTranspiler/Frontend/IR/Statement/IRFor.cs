using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR.Statement
{
    public class IRFor : IRStatement
    {
        public IRStatement Init { get; set; }
        public IRExpression Condition { get; set; }
        public IRStatement Increment { get; set; }
        public List<IRStatement> Body { get; set; } = new();
    }
}
