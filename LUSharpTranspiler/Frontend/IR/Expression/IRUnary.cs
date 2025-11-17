using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRUnary : IRExpression
    {
        public string Operator { get; set; }  // "!", "-" etc.
        public IRExpression Operand { get; set; }
    }
}
