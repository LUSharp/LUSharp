using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR.Statement
{
    public class IRIf : IRStatement
    {
        public IRExpression Condition { get; set; }
        public List<IRStatement> Then { get; set; } = new();
        public List<IRStatement> Else { get; set; } = new();
    }
}
