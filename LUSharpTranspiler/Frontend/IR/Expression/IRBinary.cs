using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRBinary : IRExpression
    {
        public string Operator { get; set; }  // "+", "-", "==", etc.
        public IRExpression Left { get; set; }
        public IRExpression Right { get; set; }
    }
}
