using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR.Statement
{
    public class IRExpressionStatement : IRStatement
    {
        public IRExpression Expression { get; set; }
    }
}
